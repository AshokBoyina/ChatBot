namespace ChatBot.Web.Services;

public class AzureFoundryOptions
{
    /// <summary>
    /// When true, uses MockAzureFoundryService instead of the real Azure AI Foundry service.
    /// Set to false (or remove) when you have real Azure credentials to use.
    /// </summary>
    public bool UseMock { get; set; } = false;

    /// <summary>
    /// Azure AI Foundry / Azure OpenAI resource endpoint.
    /// Example: https://my-resource.openai.azure.com
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>API key for the Azure AI Foundry resource.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Azure OpenAI API version (e.g. 2024-08-01-preview).</summary>
    public string ApiVersion { get; set; } = "2024-08-01-preview";

    /// <summary>Azure AI Search endpoint — required for RAG data sources.</summary>
    public string? SearchEndpoint { get; set; }

    /// <summary>Azure AI Search API key — required for RAG data sources.</summary>
    public string? SearchApiKey { get; set; }

    /// <summary>The list of RAG applications shown in the app selector.</summary>
    public List<RagAppConfig> Applications { get; set; } = [];
}

public class RagAppConfig
{
    public string  Id              { get; set; } = string.Empty;
    public string  Name            { get; set; } = string.Empty;
    public string  Description     { get; set; } = string.Empty;
    public string  DeploymentName  { get; set; } = string.Empty;
    public string? SearchIndexName { get; set; }
}
