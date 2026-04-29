namespace ChatBot.Web.Services;

using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using ChatBot.Web.Models;

/// <summary>
/// Calls the NASS AI Bot REST API.
///
/// Request shape (POST /v1/AI/Bot/chat):
/// {
///   "message": "current user question",
///   "conversationHistory": [          // full in-memory session — appended each turn
///     { "role": "user",      "content": "..." },
///     { "role": "assistant", "content": "..." }
///   ]
/// }
///
/// The full history is sent on every call so the API has complete context.
/// The response text is streamed word-by-word to the UI for a smooth UX even
/// though the underlying HTTP call is a standard request/response (not SSE).
/// </summary>
public class NassApiService : IChatService
{
    private readonly HttpClient          _http;
    private readonly NassApiOptions      _opts;
    private readonly IReadOnlyList<RagApplication> _apps;

    private static readonly JsonSerializerOptions SerialiseOpts = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    public NassApiService(HttpClient http, IOptions<NassApiOptions> opts)
    {
        _http = http;
        _opts = opts.Value;

        _apps = _opts.Applications
            .Select(a => new RagApplication(
                a.Id, a.Name, a.Description, a.DeploymentName, a.SearchIndexName))
            .ToList()
            .AsReadOnly();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public string AssistantName => _opts.AssistantName;

    public IReadOnlyList<RagApplication> GetApplications() => _apps;

    public async IAsyncEnumerable<ChatChunk> StreamChatAsync(
        RagApplication? app,
        IEnumerable<ChatMessage> history,
        string userMessage,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // Guard: config not filled in yet
        if (string.IsNullOrWhiteSpace(_opts.BaseUrl) ||
            _opts.BaseUrl.StartsWith("REPLACE", StringComparison.OrdinalIgnoreCase))
        {
            yield return new ChatChunk
            {
                IsError = true, IsDone = true,
                Text    = "NassApi:BaseUrl is not configured. " +
                          "Please update appsettings.json."
            };
            yield break;
        }

        // All HTTP work is done in a regular async Task so try/catch is unrestricted
        var (replyText, citations, errorText) =
            await CallApiAsync(app, history, userMessage, ct);

        if (errorText is not null)
        {
            yield return new ChatChunk { IsError = true, IsDone = true, Text = errorText };
            yield break;
        }

        if (replyText is null) yield break; // cancelled

        // Stream the reply word-by-word so the UI feels responsive
        foreach (var chunk in Tokenise(replyText))
        {
            if (ct.IsCancellationRequested) yield break;
            yield return chunk;
            // Small delay for animation; remove if you prefer instant render
            await Task.Delay(18, ct);
        }

        yield return new ChatChunk { IsDone = true, Citations = citations };
    }

    public async Task<string?> SummarizeAsync(
        IEnumerable<ChatMessage> history,
        CancellationToken ct = default)
    {
        const string prompt =
            "Please summarise our conversation so far in 2-3 concise sentences, " +
            "capturing the key topics discussed and any important conclusions reached.";

        var (reply, _, error) = await CallApiAsync(null, history, prompt, ct);
        return error is not null ? null : reply;
    }

    // ── HTTP call (no yield — try/catch freely used) ──────────────────────────

    private async Task<(string? reply, List<Citation>? citations, string? error)>
        CallApiAsync(
            RagApplication? app,
            IEnumerable<ChatMessage> history,
            string userMessage,
            CancellationToken ct)
    {
        var url = $"{_opts.BaseUrl.TrimEnd('/')}{_opts.ChatPath}";

        // Build the message string — prepend conversation history so the API has full context
        var sb = new System.Text.StringBuilder();
        foreach (var m in history)
        {
            sb.Append(m.Role == "user" ? "User: " : "Assistant: ");
            sb.AppendLine(m.Content);
        }
        if (sb.Length > 0) sb.AppendLine(); // blank line before the new question
        sb.Append(userMessage);

        var body = new NassApiRequest { Message = sb.ToString() };
        var json = JsonSerializer.Serialize(body, SerialiseOpts);

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            if (!string.IsNullOrWhiteSpace(_opts.ApiKey))
                req.Headers.Add("X-Api-Key", _opts.ApiKey);

            using var response = await _http.SendAsync(req, ct);

            var raw = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
                return (null, null,
                    $"API error {(int)response.StatusCode}: {raw}");

            var (reply, citations) = ParseResponse(raw);
            return (reply, citations, null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // User closed/cleared the chat — silent cancellation, no error shown
            return (null, null, null);
        }
        catch (OperationCanceledException)
        {
            // HttpClient timeout — user needs to know
            return (null, null, "The request timed out. Please try again.");
        }
        catch (HttpRequestException ex)
        {
            return (null, null, $"Could not reach the NASS API: {ex.Message}");
        }
        catch (Exception ex)
        {
            return (null, null, $"Unexpected error: {ex.Message}");
        }
    }

    // ── Response parsing ──────────────────────────────────────────────────────

    /// <summary>
    /// Tries to extract the reply text from the JSON response.
    /// Checks the configured ResponseField first, then a list of common field
    /// names, then falls back to treating the body as plain text.
    /// </summary>
    private (string reply, List<Citation>? citations) ParseResponse(string raw)
    {
        raw = raw.Trim();

        // Try to parse as JSON
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            // Try configured field name first, then common fallbacks
            var candidates = new[] { _opts.ResponseField, "reply", "message",
                                     "response", "answer", "text", "content" };

            foreach (var field in candidates.Where(f => !string.IsNullOrWhiteSpace(f)).Distinct())
            {
                if (root.TryGetProperty(field, out var el) &&
                    el.ValueKind == JsonValueKind.String)
                {
                    var text = el.GetString() ?? string.Empty;
                    var cits = TryParseCitations(root);
                    return (text, cits);
                }
            }

            // If root is just a string value
            if (root.ValueKind == JsonValueKind.String)
                return (root.GetString() ?? raw, null);
        }
        catch (JsonException) { /* not JSON — fall through */ }

        // Treat as plain text
        return (raw, null);
    }

    /// <summary>Attempts to parse a "citations" or "sources" array from the root.</summary>
    private static List<Citation>? TryParseCitations(JsonElement root)
    {
        foreach (var field in new[] { "citations", "sources", "references" })
        {
            if (!root.TryGetProperty(field, out var arr) ||
                arr.ValueKind != JsonValueKind.Array) continue;

            var list = new List<Citation>();
            int idx = 1;
            foreach (var c in arr.EnumerateArray())
            {
                list.Add(new Citation
                {
                    Index    = idx++,
                    Title    = GetStr(c, "title", "name", "filename") ?? "Source",
                    Url      = GetStr(c, "url", "link"),
                    FilePath = GetStr(c, "filepath", "path", "file"),
                    Excerpt  = Truncate(GetStr(c, "content", "excerpt", "snippet"), 250)
                });
            }
            return list.Count > 0 ? list : null;
        }
        return null;
    }

    private static string? GetStr(JsonElement el, params string[] fields)
    {
        foreach (var f in fields)
            if (el.TryGetProperty(f, out var v) && v.ValueKind == JsonValueKind.String)
                return v.GetString();
        return null;
    }

    private static string? Truncate(string? s, int max) =>
        s is null ? null : s.Length <= max ? s : s[..max] + "…";

    // ── Streaming tokeniser ───────────────────────────────────────────────────

    /// <summary>Splits reply text into word-sized ChatChunks for streaming display.</summary>
    private static IEnumerable<ChatChunk> Tokenise(string text)
    {
        foreach (var seg in text.Replace("\n", " \n ").Split(' '))
        {
            if (string.IsNullOrEmpty(seg)) continue;
            yield return new ChatChunk { Text = seg == "\n" ? "\n" : seg + " " };
        }
    }
}

// ── Internal request models ───────────────────────────────────────────────────

file class NassApiRequest
{
    public string Message { get; set; } = string.Empty;
}
