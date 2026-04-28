namespace ChatBot.Web.Services;

using System.Runtime.CompilerServices;
using Microsoft.Extensions.Options;
using ChatBot.Web.Models;

/// <summary>
/// Drop-in mock implementation of IAzureFoundryService.
/// Streams canned responses word-by-word and returns fake RAG citations,
/// so the full UI can be validated without any Azure credentials.
///
/// Switch between real and mock via appsettings.json:
///   "AzureFoundry": { "UseMock": true }
/// </summary>
public class MockAzureFoundryService : IAzureFoundryService
{
    private readonly IReadOnlyList<RagApplication> _apps;

    // ── Canned responses ──────────────────────────────────────────────────────
    // Each entry: (trigger keywords, reply text, include fake citations?)
    private static readonly (string[] Keywords, string Reply, bool Cite)[] Responses =
    [
        (
            ["hello", "hi", "hey", "greet"],
            "Hello! 👋 I'm your AI assistant running in **mock mode**.\n\nI'm ready to answer questions about your selected knowledge base. What would you like to know?",
            false
        ),
        (
            ["help", "what can you", "capabilities", "feature"],
            "Here's what I can help you with:\n\n• Search and summarise documents from the knowledge base\n• Answer questions about policies, procedures and guidelines\n• Provide step-by-step instructions\n• Compare information across multiple sources\n\nJust type your question and I'll do my best to answer it.",
            false
        ),
        (
            ["policy", "policies", "procedure", "rule", "guideline"],
            "Based on the knowledge base, here are the key policy highlights:\n\n**Remote Work Policy**\nEmployees may work remotely up to 3 days per week subject to manager approval. A stable internet connection and a secure workspace are required.\n\n**Expense Policy**\nAll expenses exceeding £50 require prior written approval. Receipts must be submitted within 30 days of incurring the expense.\n\n**Leave Policy**\nAnnual leave entitlement is 25 days per calendar year. Up to 5 days may be carried forward to the following year with line-manager sign-off.\n\nFor full details please consult the HR portal or speak to your HR business partner.",
            true
        ),
        (
            ["technical", "api", "integration", "endpoint", "sdk", "code"],
            "Here is a summary of the technical documentation:\n\n**API Authentication**\nAll API calls must include a valid Bearer token in the Authorization header. Tokens expire after 60 minutes and can be refreshed using the `/auth/refresh` endpoint.\n\n**Rate Limits**\nThe API enforces a limit of 100 requests per minute per tenant. Requests exceeding this limit receive a `429 Too Many Requests` response.\n\n**Pagination**\nList endpoints support cursor-based pagination via the `cursor` query parameter. A maximum of 100 records may be returned per page.\n\nSee the full API reference in the developer portal for endpoint-level details.",
            true
        ),
        (
            ["error", "issue", "problem", "fail", "broken", "bug"],
            "Here are the most common troubleshooting steps:\n\n1. **Clear browser cache** – Many display issues resolve after a hard refresh (Ctrl + Shift + R).\n2. **Check service status** – Visit the status page to see if there are any active incidents.\n3. **Verify credentials** – Ensure your API key or token has not expired.\n4. **Review logs** – Application logs are available under Settings → Diagnostics.\n5. **Contact support** – If the issue persists, raise a ticket via the support portal and include the correlation ID from the error response.\n\nWould you like more detail on any of these steps?",
            true
        ),
        (
            ["summarise", "summarize", "summary", "overview", "brief"],
            "Here is a high-level summary of the available documentation:\n\nThe knowledge base covers three main areas:\n\n**1. Human Resources** — Policies on leave, expenses, remote working, performance reviews and onboarding.\n\n**2. Technical Reference** — API documentation, integration guides, architecture diagrams and SDK examples.\n\n**3. Operations** — Incident management, change control procedures, SLA definitions and escalation paths.\n\nEach section is updated quarterly. You can refine this summary by asking about a specific area.",
            true
        ),
        (
            ["thank", "thanks", "cheers", "great", "perfect"],
            "You're welcome! 😊 Let me know if there's anything else I can help you with.",
            false
        )
    ];

    private static readonly string DefaultReply =
        "That's a great question. Based on the knowledge base I have access to, here is what I found:\n\n" +
        "The documentation covers this topic across several sections. The most relevant guidance " +
        "indicates that the recommended approach depends on your specific context and requirements. " +
        "Key factors to consider include your current configuration, the version in use, and any " +
        "organisation-specific customisations that may have been applied.\n\n" +
        "I recommend reviewing the linked source documents below for full details, and reaching out " +
        "to the relevant team if you need clarification on how this applies to your situation.";

    // ── Fake citations used when Cite = true ──────────────────────────────────
    private static readonly Citation[] FakeCitations =
    [
        new Citation { Index = 1, Title = "Company Policy Handbook v3.2",  Url = "https://example.com/docs/policy-handbook",   FilePath = "docs/policy-handbook.pdf",   Excerpt = "Section 4.1 — Remote Work and Flexible Arrangements" },
        new Citation { Index = 2, Title = "Technical Reference Guide",     Url = "https://example.com/docs/tech-reference",    FilePath = "docs/tech-reference.pdf",     Excerpt = "Chapter 7 — API Authentication and Rate Limiting" },
        new Citation { Index = 3, Title = "HR Quick Reference 2024",       Url = "https://example.com/docs/hr-quick-ref",      FilePath = "docs/hr-quick-ref.pdf",       Excerpt = "Leave entitlements and carry-over rules" },
    ];

    // ─────────────────────────────────────────────────────────────────────────

    public MockAzureFoundryService(IOptions<AzureFoundryOptions> opts)
    {
        _apps = opts.Value.Applications
            .Select(a => new RagApplication(
                a.Id, a.Name, a.Description, a.DeploymentName, a.SearchIndexName))
            .ToList()
            .AsReadOnly();
    }

    public IReadOnlyList<RagApplication> GetApplications() => _apps;

    public async IAsyncEnumerable<ChatChunk> StreamChatAsync(
        RagApplication app,
        IEnumerable<ChatMessage> history,
        string userMessage,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // Small initial pause to simulate network latency
        await Task.Delay(350, ct);

        var (reply, cite) = PickResponse(userMessage);

        // Emit a "mock mode" notice on first turn (no prior assistant messages)
        bool isFirstTurn = !history.Any(m => m.Role == "assistant");
        if (isFirstTurn)
        {
            const string notice = "*(Mock mode — no Azure connection required)*\n\n";
            foreach (var chunk in TokeniseText(notice))
            {
                if (ct.IsCancellationRequested) yield break;
                yield return chunk;
                await Task.Delay(18, ct);
            }
        }

        // Stream the reply word by word with variable timing for realism
        var random = new Random();
        foreach (var chunk in TokeniseText(reply))
        {
            if (ct.IsCancellationRequested) yield break;
            yield return chunk;

            // Vary delay: longer after punctuation, shorter mid-sentence
            var text = chunk.Text;
            int delay = text.EndsWith('.') || text.EndsWith('?') || text.EndsWith('!')
                ? random.Next(120, 220)
                : text.EndsWith(',') || text.EndsWith(':')
                    ? random.Next(60, 100)
                    : text.EndsWith('\n')
                        ? random.Next(80, 160)
                        : random.Next(25, 55);

            await Task.Delay(delay, ct);
        }

        // Return fake citations if this app has a search index and the response warrants it
        var citations = cite && !string.IsNullOrEmpty(app.SearchIndexName)
            ? FakeCitations.Take(2).ToList()
            : null;

        yield return new ChatChunk { IsDone = true, Citations = citations };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Picks the best canned response based on keywords in the user message.</summary>
    private static (string Reply, bool Cite) PickResponse(string userMessage)
    {
        var lower = userMessage.ToLowerInvariant();

        foreach (var (keywords, reply, cite) in Responses)
        {
            if (keywords.Any(k => lower.Contains(k)))
                return (reply, cite);
        }

        return (DefaultReply, true);
    }

    /// <summary>
    /// Splits text into small chunks that mimic a token stream —
    /// word by word, preserving newlines as separate tokens.
    /// </summary>
    private static IEnumerable<ChatChunk> TokeniseText(string text)
    {
        // Split on spaces but keep newline sequences as their own tokens
        var segments = text.Replace("\n", " \n ").Split(' ');

        foreach (var seg in segments)
        {
            if (string.IsNullOrEmpty(seg)) continue;

            // Yield newlines without a trailing space
            if (seg == "\n")
            {
                yield return new ChatChunk { Text = "\n" };
                continue;
            }

            yield return new ChatChunk { Text = seg + " " };
        }
    }
}
