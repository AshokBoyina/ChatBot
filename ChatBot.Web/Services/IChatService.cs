namespace ChatBot.Web.Services;

using ChatBot.Web.Models;

/// <summary>
/// Abstraction over any chat back-end (NASS API, mock, etc.).
/// The widget only depends on this interface.
/// </summary>
public interface IChatService
{
    /// <summary>Display name shown in the chat header and above the floating bubble.</summary>
    string AssistantName { get; }

    /// <summary>Returns the list of RAG applications shown in the selector.</summary>
    IReadOnlyList<RagApplication> GetApplications();

    /// <summary>
    /// Sends a user message (plus the full in-memory session history) to the
    /// back-end and yields response text chunks for real-time streaming display.
    /// The final chunk has IsDone = true and may carry RAG citations.
    /// </summary>
    IAsyncEnumerable<ChatChunk> StreamChatAsync(
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
