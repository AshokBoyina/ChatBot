namespace ChatBot.Web.Services;

public class NassApiOptions
{
    /// <summary>
    /// Display name shown in the chat window header and above the floating bubble.
    /// Example: "NASS Assistant", "HR Helpdesk", "IT Support Bot"
    /// </summary>
    public string AssistantName { get; set; } = "AI Assistant";

    /// <summary>
    /// Set to true to use MockChatService instead of the real NASS API.
    /// Defaults to true in Development, false in Production.
    /// </summary>
    public bool UseMock { get; set; } = false;

    /// <summary>
    /// Base URL of the NASS AI Bot API.
    /// Example: https://nicedev.nass.usda.gov
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Path of the chat endpoint, relative to BaseUrl.
    /// Defaults to /v1/AI/Bot/chat
    /// </summary>
    public string ChatPath { get; set; } = "/v1/AI/Bot/chat";

    /// <summary>
    /// Optional API key sent as X-Api-Key header to the upstream NASS API.
    /// Leave empty if the API uses network-level auth.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Optional key that the browser must include as the X-API-Access-Key header
    /// when calling the local POST /api/chat endpoint.
    /// When set, requests without a matching header are rejected with 401 Unauthorized.
    /// Leave empty to allow unauthenticated access (e.g. intranet-only deployments).
    /// </summary>
    public string? ApiAccessKey { get; set; }

    /// <summary>
    /// When true (default), the full conversation history of the current session
    /// is prepended to every outgoing message so the API has context.
    /// Set to false if the API maintains its own session state, or if you want
    /// each message to be sent in isolation.
    /// </summary>
    public bool SendChatHistory { get; set; } = true;

    /// <summary>
    /// JSON field name in the API response that contains the reply text.
    /// Common values: "reply", "message", "response", "answer".
    /// If empty or the field is not found, the raw response body is used.
    /// </summary>
    public string ResponseField { get; set; } = "reply";

    /// <summary>
    /// RAG application / knowledge-base list shown in the selector drop-down.
    /// </summary>
    public List<RagAppConfig> Applications { get; set; } = [];
}

/// <summary>Maps one entry under NassApi:Applications in appsettings.json.</summary>
public class RagAppConfig
{
    public string  Id              { get; set; } = string.Empty;
    public string  Name            { get; set; } = string.Empty;
    public string  Description     { get; set; } = string.Empty;
    public string  DeploymentName  { get; set; } = string.Empty;
    public string? SearchIndexName { get; set; }
}
