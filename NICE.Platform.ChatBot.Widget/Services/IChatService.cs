namespace NICE.Platform.ChatBot.Widget.Services;

using NICE.Platform.ChatBot.Widget.Models;

public interface IChatService
{
    string  AssistantName { get; }
    string? ApiAccessKey  { get; }
    bool    IsMock        { get; }

    Task<ChatApiResponse> GetReplyAsync(
        IEnumerable<ChatMessage> history,
        string                   userMessage,
        CancellationToken        ct = default);

    Task<string?> SummarizeAsync(
        IEnumerable<ChatMessage> history,
        CancellationToken        ct = default);
}
