namespace NICE.Platform.ChatBot.Widget.Services;

using NICE.Platform.ChatBot.Widget.Models;

public interface IChatService
{
    string  AssistantName { get; }
    string? ApiAccessKey  { get; }

    Task<ChatApiResponse> GetReplyAsync(
        IEnumerable<ChatMessage> history,
        string                   userMessage,
        CancellationToken        ct = default);
}
