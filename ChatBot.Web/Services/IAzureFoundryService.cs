namespace ChatBot.Web.Services;

using ChatBot.Web.Models;

public interface IAzureFoundryService
{
    /// <summary>Returns all configured RAG applications from appsettings.</summary>
    IReadOnlyList<RagApplication> GetApplications();

    /// <summary>
    /// Streams a chat completion from Azure AI Foundry via SSE,
    /// yielding text chunks and (on the final chunk) any RAG citations.
    /// </summary>
    IAsyncEnumerable<ChatChunk> StreamChatAsync(
        RagApplication app,
        IEnumerable<ChatMessage> history,
        string userMessage,
        CancellationToken ct = default);
}
