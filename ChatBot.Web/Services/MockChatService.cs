namespace ChatBot.Web.Services;

using Microsoft.Extensions.Options;
using ChatBot.Web.Models;

/// <summary>
/// Drop-in mock implementation of IChatService.
/// Streams canned responses word-by-word and returns fake RAG citations
/// so the full UI can be validated without a live API endpoint.
///
/// Toggle via appsettings.json:  "NassApi": { "UseMock": true }
/// </summary>
public class MockChatService : IChatService
{
    private readonly IReadOnlyList<RagApplication> _apps;

    // ── Canned Q&A bank ───────────────────────────────────────────────────────
    // ( trigger keywords, reply text, include fake citations? )
    private static readonly (string[] Keys, string Reply, bool Cite)[] Bank =
    [
        (
            ["hello", "hi", "hey"],
            "Hello! I am your NASS AI assistant running in mock mode.\n\n" +
            "I am ready to answer questions about the NASS knowledge base. What would you like to know?",
            false
        ),
        (
            ["nass", "national agricultural", "usda"],
            "NASS stands for the National Agricultural Statistics Service. It is an agency of the USDA " +
            "that provides timely, accurate, and useful statistics in service to U.S. agriculture.\n\n" +
            "NASS conducts hundreds of surveys every year and prepares reports covering virtually " +
            "every aspect of U.S. agriculture, including production and supplies of food and fiber, " +
            "prices paid and received by farmers, farm labour and wages, farm finances, chemical use, " +
            "and changes in the demographic structure of U.S. farms.",
            true
        ),
        (
            ["survey", "census", "data collection"],
            "NASS conducts the Census of Agriculture every five years and numerous annual, monthly, " +
            "and weekly surveys.\n\nKey surveys include:\n" +
            "- Crop Production surveys (corn, soybeans, wheat, cotton, etc.)\n" +
            "- Livestock surveys (cattle, hogs, poultry)\n" +
            "- Agricultural Prices survey\n" +
            "- Farm Labor survey\n\n" +
            "All data collection follows strict confidentiality standards under Title 7 USC Section 2204(g).",
            true
        ),
        (
            ["crop", "production", "yield", "harvest", "grain"],
            "NASS releases crop production estimates on a monthly basis during the growing season. " +
            "The reports cover planted and harvested acreage, yield per acre, and total production " +
            "for major field crops.\n\nKey releases:\n" +
            "- Crop Production (monthly, March through November)\n" +
            "- Grain Stocks (quarterly)\n" +
            "- Small Grains Summary (annual)\n\n" +
            "Reports are published at 12:00 noon Eastern Time on the scheduled release date.",
            true
        ),
        (
            ["help", "what can", "capabilit"],
            "I can help you find information about:\n\n" +
            "- NASS surveys and censuses\n" +
            "- Crop and livestock production data\n" +
            "- Agricultural prices and economics\n" +
            "- Data release schedules and methodology\n" +
            "- How to access NASS datasets and APIs\n\n" +
            "Just type your question and I will do my best to find the answer.",
            false
        ),
        (
            ["thank", "thanks", "great", "perfect", "cheers"],
            "You're welcome! Let me know if there is anything else I can help you with.",
            false
        )
    ];

    private static readonly string DefaultReply =
        "Based on the NASS knowledge base, here is what I found:\n\n" +
        "NASS provides a wide range of agricultural statistics covering crop production, " +
        "livestock inventories, prices, farm economics, and the Census of Agriculture. " +
        "The most relevant information for your query can be found in the source documents listed below.\n\n" +
        "If you need more specific information, please refine your question or contact your " +
        "local NASS Regional Field Office for assistance.";

    // ── Fake citations ────────────────────────────────────────────────────────
    private static readonly Citation[] FakeCitations =
    [
        new Citation
        {
            Index    = 1,
            Title    = "NASS Mission and Programs Overview",
            Url      = "https://www.nass.usda.gov/About_NASS/index.php",
            FilePath = "docs/nass-overview.pdf",
            Excerpt  = "NASS is committed to providing timely, accurate, and useful statistics."
        },
        new Citation
        {
            Index    = 2,
            Title    = "2022 Census of Agriculture",
            Url      = "https://www.nass.usda.gov/AgCensus/",
            FilePath = "docs/ag-census-2022.pdf",
            Excerpt  = "The Census of Agriculture is conducted every five years."
        }
    ];

    // ─────────────────────────────────────────────────────────────────────────

    public MockChatService(IOptions<NassApiOptions> opts)
    {
        AssistantName = opts.Value.AssistantName;
        _apps = opts.Value.Applications
            .Select(a => new RagApplication(
                a.Id, a.Name, a.Description, a.DeploymentName, a.SearchIndexName))
            .ToList()
            .AsReadOnly();
    }

    public string AssistantName { get; }

    public IReadOnlyList<RagApplication> GetApplications() => _apps;

    public Task<string?> SummarizeAsync(
        IEnumerable<ChatMessage> history,
        CancellationToken ct = default)
    {
        var turns = history.Count();
        var result = (string?)
            $"Earlier in this conversation ({turns} messages), the user asked questions about " +
            "the NASS knowledge base including topics such as surveys, crop production data, " +
            "and agricultural statistics. Key points were discussed and relevant sources were referenced.";
        return Task.FromResult(result);
    }

    public async Task<ChatApiResponse> GetReplyAsync(
        RagApplication? app,
        IEnumerable<ChatMessage> history,
        string userMessage,
        CancellationToken ct = default)
    {
        await Task.Delay(400, ct); // simulate network round-trip

        var (reply, cite) = Pick(userMessage);

        // Prefix first-turn notice
        bool firstTurn = !history.Any(m => m.Role == "assistant");
        var fullReply  = firstTurn ? $"[Mock mode — NASS API not called]\n\n{reply}" : reply;

        bool hasIndex  = !string.IsNullOrEmpty(app?.SearchIndexName);
        var citations  = cite && hasIndex ? [.. FakeCitations] : (List<Citation>?)null;

        return new ChatApiResponse(fullReply, citations, null);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (string Reply, bool Cite) Pick(string msg)
    {
        var lower = msg.ToLowerInvariant();
        foreach (var (keys, reply, cite) in Bank)
            if (keys.Any(k => lower.Contains(k)))
                return (reply, cite);
        return (DefaultReply, true);
    }
}
