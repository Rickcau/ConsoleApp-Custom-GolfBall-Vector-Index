// Program.cs
using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Search.Documents.Indexes;
using Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Azure.Search.Documents;
using ConfigurationTool;
using ConsoleApp_Custom_GolfBall_Vector_Index.Helper;
using ConsoleApp_Custom_GolfBall_Vector_Index.Models;
using Configuration = ConfigurationTool.Configuration;
using Microsoft.Extensions.Options;

// Create builder and determine environment
var builder = Host.CreateApplicationBuilder(args);

// Allow environment to be passed as command line argument or default to Development
var environment = builder.Configuration["Environment"] ?? "Development";

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile($"appsettings.{environment}.json", optional: true) // Environment specific
    .AddJsonFile("appsettings.Local.json", optional: true) // Local development settings (highest priority)
    .AddEnvironmentVariables(); // Standard environment variables

// Add services
builder.Services.AddOptions<Configuration>()
    .Bind(builder.Configuration.GetSection("Azure"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton(sp =>
{
    var config = new Configuration();
    builder.Configuration.GetSection("Azure").Bind(config);
    return config;
});

builder.Services.AddSingleton<GolfBallSearchHelper>();

builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<Configuration>();
    var logger = sp.GetRequiredService<ILogger<Program>>();

    logger.LogInformation("Initializing Search Index Client with endpoint: {Endpoint}", config.SearchServiceEndpoint);
    return new SearchIndexClient(
        new Uri(config.SearchServiceEndpoint!),
        new AzureKeyCredential(config.SearchAdminKey!));
});

builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<Configuration>();
    var indexClient = sp.GetRequiredService<SearchIndexClient>();
    return indexClient.GetSearchClient(config.IndexName);
});

builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<Configuration>();
    var logger = sp.GetRequiredService<ILogger<Program>>();

    logger.LogInformation("Initializing OpenAI Client with endpoint: {Endpoint}", config.AzureOpenAIEndpoint);
    return new AzureOpenAIClient(
        new Uri(config.AzureOpenAIEndpoint!),
        new AzureKeyCredential(config.AzureOpenAIApiKey!));
});

var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Application starting in {Environment} environment", environment);

try
{
    // Get required services
    var configuration = host.Services.GetRequiredService<Configuration>();
    var searchHelper = host.Services.GetRequiredService<GolfBallSearchHelper>();
    var searchIndexClient = host.Services.GetRequiredService<SearchIndexClient>();
    var searchClient = host.Services.GetRequiredService<SearchClient>();
    var openAIClient = host.Services.GetRequiredService<AzureOpenAIClient>();

    // Log configuration values (excluding sensitive data)
    logger.LogInformation("Using Search Service Endpoint: {Endpoint}", configuration.SearchServiceEndpoint);
    logger.LogInformation("Using OpenAI Embedding Model: {Model}", configuration.AzureOpenAIEmbeddingModel);
    logger.LogInformation("Using Index Name: {IndexName}", configuration.IndexName);

    // Create or update search index
    logger.LogInformation("Creating search index...");
    await searchHelper.SetupIndexAsync(configuration, searchIndexClient);
    logger.LogInformation("Search index created successfully.");


    // Get CSV file path - try multiple possible locations
    string? csvPath = null;
    var possiblePaths = new[]
    {
    // Try the data folder relative to current directory
    Path.Combine(Directory.GetCurrentDirectory(), "data", "sample-data-b1.csv"),
    // Try the data folder relative to base directory
    Path.Combine(AppContext.BaseDirectory, "data", "sample-data.csv"),
    // Try current directory
    Path.Combine(Directory.GetCurrentDirectory(), "sample-data.csv"),
    // Try base directory
    Path.Combine(AppContext.BaseDirectory, "sample-data.csv")
    };

    foreach (var path in possiblePaths)
    {
        if (File.Exists(path))
        {
            csvPath = path;
            logger.LogInformation("Found CSV file at: {Path}", path);
            break;
        }
    }

    if (csvPath == null)
    {
        logger.LogError("CSV file not found. Searched locations:");
        foreach (var path in possiblePaths)
        {
            logger.LogError("- {Path}", path);
        }
        throw new FileNotFoundException("Sample data CSV file not found. Please ensure it exists in the data folder and is set to copy to output directory.");
    }

    // Upload golf ball data
    logger.LogInformation("Uploading golf ball data from {Path}", csvPath);
    await searchHelper.UploadGolfBallDataAsync(configuration, openAIClient, searchClient, csvPath);
    logger.LogInformation("Data uploaded successfully.");

    // Demonstrate search functionality
    logger.LogInformation("\nPerforming sample searches...");

    var searches = new[]
    {
        "Find golf balls with similar markings to Titleist Pro V1",
        "Show me white golf balls with arrow markings",
        "Find golf balls with high spin characteristics"
    };

    foreach (var searchQuery in searches)
    {
        logger.LogInformation("Search Query: {Query}", searchQuery);
        var results = await searchHelper.Search(searchClient, searchQuery);

        if (!results.Any())
        {
            logger.LogWarning("No results found for query: {Query}", searchQuery);
            continue;
        }

        logger.LogInformation("Results:");
        foreach (var result in results)
        {
            Console.WriteLine(new string('-', 40));
            Console.WriteLine($"Manufacturer: {result.Manufacturer}");
            Console.WriteLine($"Pole Marking: {result.Pole_Marking}");
            Console.WriteLine($"Color: {result.Colour}");
            Console.WriteLine($"Seam Marking: {result.Seam_Marking}");
        }
    }

    logger.LogInformation("Sample searches completed successfully.");
}
catch (OptionsValidationException ex)
{
    logger.LogError("Configuration validation failed: {Errors}", string.Join(", ", ex.Failures));
    Environment.Exit(1);
}
catch (Exception ex)
{
    logger.LogError(ex, "An unexpected error occurred");
    Environment.Exit(1);
}

logger.LogInformation("Application completed successfully.");