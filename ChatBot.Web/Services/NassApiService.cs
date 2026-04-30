namespace NICE.Platform.ChatBot.Web.Services;

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using NICE.Platform.ChatBot.Web.Models;

/// <summary>
/// Calls the NASS AI Bot REST API.
///
/// Request shape (POST /v1/AI/Bot/chat):
/// {
///   "message": "current user question (optionally prepended with conversation history)"
/// }
///
/// GetReplyAsync is called by the minimal-API endpoint POST /api/chat and
/// returns the full reply immediately — no per-word streaming delays server-side.
/// The browser JS animates the text word-by-word on the client.
/// </summary>
public class NassApiService : IChatService
{
    private readonly HttpClient          _http;
    private readonly NassApiOptions      _opts;
    private readonly ILogger<NassApiService> _log;
    private readonly IReadOnlyList<RagApplication> _apps;

    private static readonly JsonSerializerOptions SerialiseOpts = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    public NassApiService(HttpClient http, IOptions<NassApiOptions> opts, ILogger<NassApiService> log)
    {
        _http = http;
        _opts = opts.Value;
        _log  = log;

        _apps = _opts.Applications
            .Select(a => new RagApplication(
                a.Id, a.Name, a.Description, a.DeploymentName, a.SearchIndexName))
            .ToList()
            .AsReadOnly();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public string  AssistantName => _opts.AssistantName;
    public string? ApiAccessKey  => _opts.ApiAccessKey;

    public IReadOnlyList<RagApplication> GetApplications() => _apps;

    public async Task<ChatApiResponse> GetReplyAsync(
        RagApplication? app,
        IEnumerable<ChatMessage> history,
        string userMessage,
        CancellationToken ct = default)
    {
        // Guard: config not filled in yet
        if (string.IsNullOrWhiteSpace(_opts.BaseUrl) ||
            _opts.BaseUrl.StartsWith("REPLACE", StringComparison.OrdinalIgnoreCase))
        {
            return new ChatApiResponse(null, null,
                "NassApi:BaseUrl is not configured. Please update appsettings.json.");
        }

        var (reply, citations, error) = await CallApiAsync(app, history, userMessage, ct);
        return new ChatApiResponse(reply, citations, error);
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

        // Build the message string.
        // When SendChatHistory is true, prepend all prior turns so the API has full context.
        // When false, send only the current user message (use this if the API manages its own session).
        string message;
        if (_opts.SendChatHistory)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var m in history)
            {
                sb.Append(m.Role == "user" ? "User: " : "Assistant: ");
                sb.AppendLine(m.Content);
            }
            if (sb.Length > 0) sb.AppendLine(); // blank line before the new question
            sb.Append(userMessage);
            message = sb.ToString();
        }
        else
        {
            message = userMessage;
        }

        var body = new NassApiRequest { Message = message };
        var json = JsonSerializer.Serialize(body, SerialiseOpts);

        _log.LogInformation("NASS API ► POST {Url}", url);
        _log.LogDebug("NASS API ► Request body: {Body}", json);

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            if (!string.IsNullOrWhiteSpace(_opts.ApiKey))
                req.Headers.Add("X-Api-Key", _opts.ApiKey);

            if (!string.IsNullOrWhiteSpace(_opts.ApiAccessKey))
                req.Headers.Add("X-API-Access-Key", _opts.ApiAccessKey);

            using var response = await _http.SendAsync(req, ct);

            var raw = await response.Content.ReadAsStringAsync(ct);

            _log.LogInformation("NASS API ◄ {StatusCode} ({Length} chars)",
                (int)response.StatusCode, raw.Length);
            _log.LogDebug("NASS API ◄ Response body: {Body}", raw);

            if (!response.IsSuccessStatusCode)
            {
                _log.LogWarning("NASS API error {StatusCode}: {Body}", (int)response.StatusCode, raw);
                return (null, null, $"API error {(int)response.StatusCode}: {raw}");
            }

            var (reply, citations) = ParseResponse(raw);
            return (reply, citations, null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _log.LogInformation("NASS API ◄ Request cancelled by user");
            return (null, null, null);
        }
        catch (OperationCanceledException)
        {
            _log.LogWarning("NASS API ◄ Request timed out ({Url})", url);
            return (null, null, "The request timed out. Please try again.");
        }
        catch (HttpRequestException ex)
        {
            _log.LogError(ex, "NASS API ◄ Network error reaching {Url}", url);
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

}

// ── Internal request models ───────────────────────────────────────────────────

file class NassApiRequest
{
    public string Message { get; set; } = string.Empty;
}
