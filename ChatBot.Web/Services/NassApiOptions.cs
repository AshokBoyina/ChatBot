namespace ChatBot.Web.Services;

public class NassApiOptions
{
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
    /// Optional Bearer token / API key sent as Authorization header.
    /// Leave empty if the API uses network-level auth.
    /// </summary>
    public string? ApiKey { get; set; }

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
