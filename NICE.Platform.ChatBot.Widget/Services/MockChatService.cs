namespace NICE.Platform.ChatBot.Widget.Services;

using Microsoft.Extensions.Options;
using NICE.Platform.ChatBot.Widget.Models;

/// <summary>Returns canned responses without hitting the real API. Enable via NassApi:UseMock=true.</summary>
public class MockChatService : IChatService
{
    private readonly NassApiOptions _opts;
    public MockChatService(IOptions<NassApiOptions> opts) => _opts = opts.Value;

    public string  AssistantName => _opts.AssistantName;
    public string? ApiAccessKey  => _opts.ApiAccessKey;
    public bool    IsMock        => true;

    public async Task<ChatApiResponse> GetReplyAsync(
        IEnumerable<ChatMessage> history,
        string                   userMessage,
        CancellationToken        ct = default)
    {
        await Task.Delay(400, ct);
        var (reply, cite) = PickReply(userMessage);
        bool firstTurn = !history.Any(m => m.Role == "assistant");
        var fullReply  = firstTurn ? $"[Mock mode — NASS API not called]\n\n{reply}" : reply;
        var citations  = cite ? (List<Citation>?) [.. FakeCitations] : null;
        return new ChatApiResponse(fullReply, citations);
    }

    public Task<string?> SummarizeAsync(IEnumerable<ChatMessage> history, CancellationToken ct = default)
        => Task.FromResult<string?>("Mock conversation");

    private static (string reply, bool cite) PickReply(string input)
    {
        var lower = input.ToLowerInvariant();
        if (lower.Contains("crop") || lower.Contains("corn") || lower.Contains("soy"))
            return ("According to NASS estimates, corn production was approximately 15 billion bushels last year, with top states being Iowa, Illinois, and Nebraska.", true);
        if (lower.Contains("survey") || lower.Contains("census"))
            return ("NASS conducts hundreds of surveys each year covering crops, livestock, labor, and more. The Census of Agriculture runs every five years.", true);
        if (lower.Contains("livestock") || lower.Contains("cattle") || lower.Contains("hog"))
            return ("The latest cattle inventory shows approximately 87 million head. Hog inventories are around 74 million head.", true);
        return ("I'm the READI Assistant. I can help you find information about NASS surveys, crop estimates, livestock data, and more. What would you like to know?", false);
    }

    private static readonly Citation[] FakeCitations =
    [
        new() { Index = 1, Title = "NASS Crop Production Report",   Url = "https://www.nass.usda.gov/Publications/", Excerpt = "Sample excerpt from crop production report." },
        new() { Index = 2, Title = "NASS Livestock Survey Summary", Url = "https://www.nass.usda.gov/Surveys/",      Excerpt = "Sample excerpt from livestock survey." }
    ];
}
