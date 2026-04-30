namespace NICE.Platform.ChatBot.Widget.Services;

using NICE.Platform.ChatBot.Widget.Models;

public interface IChatService
{
    string  AssistantName { get; }
    string? ApiAccessKey  { get; }

    IReadOnlyList<RagApplication> GetApplications();

    Task<ChatApiResponse> GetReplyAsync(
        RagApplication?          app,
        IEnumerable<ChatMessage> history,
        string                   userMessage,
        CancellationToken        ct = default);

    Task<string?> SummarizeAsync(
        IEnumerable<ChatMessage> history,
        CancellationToken        ct = default);
}
