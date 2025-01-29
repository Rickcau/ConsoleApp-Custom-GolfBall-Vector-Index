namespace ConfigurationTool
{
    public class Configuration
    {
        public string IndexName { get; set; } = string.Empty;
        public string? SearchServiceEndpoint { get; set; }
        public string? SearchAdminKey { get; set; }
        public string? AzureOpenAIEndpoint { get; set; }
        public string? AzureOpenAIApiKey { get; set; }
        public string? AzureOpenAIEmbeddingModel { get; set; }
        public string? AzureOpenAIEmbeddingDeployment { get; set; }
        public string? AzureOpenAIEmbeddingDimensions { get; set; }
    }
}