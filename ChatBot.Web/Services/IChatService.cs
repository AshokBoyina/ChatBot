namespace NICE.Platform.ChatBot.Web.Services;

using NICE.Platform.ChatBot.Web.Models;

/// <summary>
/// Abstraction over any chat back-end (NASS API, mock, etc.).
/// The widget only depends on this interface.
/// </summary>
public interface IChatService
{
    /// <summary>Display name shown in the chat header and above the floating bubble.</summary>
    string AssistantName { get; }

    /// <summary>
    /// Key that the browser sends as the X-API-Access-Key request header when
    /// calling POST /api/chat.  Null/empty means the endpoint is unprotected.
    /// </summary>
    string? ApiAccessKey { get; }

    /// <summary>Returns the list of RAG applications shown in the selector.</summary>
    IReadOnlyList<RagApplication> GetApplications();

    /// <summary>
    /// Returns the full reply in one shot (no word-by-word delays).
    /// Called by the minimal-API endpoint POST /api/chat so the HTTP response
    /// is returned as soon as the back-end replies — the browser JS then
    /// animates the text word-by-word on the client side.
    /// </summary>
    Task<ChatApiResponse> GetReplyAsync(
        RagApplication? app,
        IEnumerable<ChatMessage> history,
        string userMessage,
        CancellationToken ct = default);

    /// <summary>
    /// Summarises a list of messages into a single concise paragraph.
    /// Returns null if the call fails or is cancelled.
    /// Used to trim long conversation histories once they exceed the threshold.
    /// </summary>
    Task<string?> SummarizeAsync(
        IEnumerable<ChatMessage> history,
        CancellationToken ct = default);
}
