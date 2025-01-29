using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Search.Documents.Indexes;
using Azure;
using Microsoft.Extensions.Configuration;
using ConfigurationTool;
using Azure.Search.Documents.Indexes.Models;
using ConsoleApp_Custom_GolfBall_Vector_Index.Models;
using Azure.Search.Documents;
using System.Text.Json;
using OpenAI.Embeddings;
using Azure.Search.Documents.Models;

namespace ConsoleApp_Custom_GolfBall_Vector_Index.Helper
{
    // GolfBallSearchHelper.cs
    public class GolfBallSearchHelper
    {
        public async Task SetupIndexAsync(ConfigurationTool.Configuration configuration, SearchIndexClient indexClient)
        {
            const string vectorSearchHnswProfile = "golf-vector-profile";
            const string vectorSearchHnswConfig = "golfHnsw";
            const string vectorSearchVectorizer = "golfOpenAIVectorizer";
            const string semanticSearchConfig = "golf-semantic-config";

            SearchIndex searchIndex = new(configuration.IndexName)
            {
                VectorSearch = new()
                {
                    Profiles =
                {
                    new VectorSearchProfile(vectorSearchHnswProfile, vectorSearchHnswConfig)
                    {
                        VectorizerName = vectorSearchVectorizer
                    }
                },
                    Algorithms =
                {
                    new HnswAlgorithmConfiguration(vectorSearchHnswConfig)
                    {
                        Parameters = new HnswParameters
                        {
                            M = 4,
                            EfConstruction = 400,
                            EfSearch = 500,
                            Metric = "cosine"
                        }
                    }
                },
                    Vectorizers =
                {
                    new AzureOpenAIVectorizer(vectorSearchVectorizer)
                    {
                        Parameters = new AzureOpenAIVectorizerParameters
                        {
                            ResourceUri = new Uri(configuration.AzureOpenAIEndpoint!),
                            ModelName = configuration.AzureOpenAIEmbeddingModel,
                            DeploymentName = configuration.AzureOpenAIEmbeddingDeployment,
                            ApiKey = configuration.AzureOpenAIApiKey
                        }
                    }
                }
                },
                SemanticSearch = new()
                {
                    Configurations =
                {
                    new SemanticConfiguration(semanticSearchConfig, new()
                    {
                        TitleField = new SemanticField("manufacturer"),
                        ContentFields =
                        {
                            new SemanticField("pole_marking"),
                            new SemanticField("seam_marking")
                        }
                    })
                }
                },
                Fields =
            {
                new SimpleField("id", SearchFieldDataType.String) { IsKey = true, IsFilterable = true },
                new SearchableField("manufacturer") { IsFilterable = true, IsSortable = true },
                new SearchableField("usga_lot_num") { IsFilterable = true },
                new SearchableField("pole_marking") { IsFilterable = true },
                new SearchableField("colour") { IsFilterable = true },
                new SearchableField("constCode") { IsFilterable = true },
                new SearchableField("ballSpecs") { IsFilterable = true },
                new SimpleField("dimples", SearchFieldDataType.Int32) { IsFilterable = true, IsSortable = true },
                new SearchableField("spin") { IsFilterable = true },
                new SearchableField("pole_2") { IsFilterable = true },
                new SearchableField("seam_marking") { IsFilterable = true },
                new SimpleField("imageUrl", SearchFieldDataType.String) { IsFilterable = false },
                new SearchField("vectorContent", SearchFieldDataType.Collection(SearchFieldDataType.Single))
                {
                    IsSearchable = true,
                    VectorSearchDimensions = int.Parse(configuration.AzureOpenAIEmbeddingDimensions!),
                    VectorSearchProfileName = vectorSearchHnswProfile
                }
            }
            };

            await indexClient.CreateOrUpdateIndexAsync(searchIndex);
        }

        public async Task UploadGolfBallDataAsync(ConfigurationTool.Configuration configuration, AzureOpenAIClient azureOpenAIClient,
            SearchClient searchClient, string csvFilePath)
        {
            var golfBalls = await LoadGolfBallsFromCsvAsync(csvFilePath);

            if (golfBalls == null || !golfBalls.Any())
            {
                throw new ArgumentException("No golf ball data found in CSV.");
            }

            var embeddingClient = azureOpenAIClient.GetEmbeddingClient(configuration.AzureOpenAIEmbeddingDeployment);

            foreach (var golfBall in golfBalls)
            {
                string textForEmbedding = $"Manufacturer: {golfBall.Manufacturer}, " +
                                        $"Pole Marking: {golfBall.Pole_Marking}, " +
                                        $"Color: {golfBall.Colour}, " +
                                        $"Seam Marking: {golfBall.Seam_Marking}";

                OpenAIEmbedding embedding = await embeddingClient.GenerateEmbeddingAsync(textForEmbedding);
                golfBall.VectorContent = embedding.ToFloats().ToArray().ToList();
            }

            var batch = IndexDocumentsBatch.Upload(golfBalls);
            var result = await searchClient.IndexDocumentsAsync(batch);
            Console.WriteLine($"Indexed {golfBalls.Count} golf balls.");
        }

        private async Task<List<GolfBallData>> LoadGolfBallsFromCsvAsync(string csvFilePath)
        {
            var golfBalls = new List<GolfBallData>();
            var lines = await File.ReadAllLinesAsync(csvFilePath);
            var headers = lines[0].Split(',');

            for (int i = 1; i < lines.Length; i++)
            {
                var values = lines[i].Split(',');
                var golfBall = new GolfBallData
                {
                    Manufacturer = values[1],
                    USGA_Lot_Num = values[2],
                    Pole_Marking = values[3],
                    Colour = values[4],
                    ConstCode = values[5],
                    BallSpecs = values[6],
                    Dimples = int.Parse(values[7]),
                    Spin = values[8],
                    Pole_2 = values[9],
                    Seam_Marking = values[10],
                    ImageUrl = values[11]
                };
                golfBalls.Add(golfBall);
            }

            return golfBalls;
        }

        public async Task<List<GolfBallData>> Search(SearchClient searchClient, string query,
            int k = 3, int top = 10, string? filter = null)
        {
            var searchOptions = new SearchOptions
            {
                Filter = filter,
                Size = top,
                Select = { "id", "manufacturer", "pole_marking", "colour", "seam_marking" },
                VectorSearch = new()
                {
                    Queries = {
                    new VectorizableTextQuery(text: query)
                    {
                        KNearestNeighborsCount = k,
                        Fields = { "vectorContent" }
                    }
                }
                }
            };

            SearchResults<SearchDocument> response = await searchClient.SearchAsync<SearchDocument>(null, searchOptions);
            var results = new List<GolfBallData>();

            await foreach (SearchResult<SearchDocument> result in response.GetResultsAsync())
            {
                results.Add(new GolfBallData
                {
                    Id = result.Document["id"].ToString(),
                    Manufacturer = result.Document["manufacturer"].ToString(),
                    Pole_Marking = result.Document["pole_marking"].ToString(),
                    Colour = result.Document["colour"].ToString(),
                    Seam_Marking = result.Document["seam_marking"].ToString()
                });
            }

            return results;
        }
    }
}
