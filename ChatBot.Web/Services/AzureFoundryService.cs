namespace ChatBot.Web.Services;

using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ChatBot.Web.Models;

/// <summary>
/// Calls the Azure AI Foundry (Azure OpenAI) REST API with Server-Sent Events
/// streaming and extracts RAG citations from the response context field.
/// </summary>
public class AzureFoundryService : IAzureFoundryService
{
    private readonly HttpClient          _http;
    private readonly AzureFoundryOptions _opts;
    private readonly IReadOnlyList<RagApplication> _apps;

    private static readonly JsonSerializerOptions _jsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    public AzureFoundryService(HttpClient http, IOptions<AzureFoundryOptions> opts)
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

    public IReadOnlyList<RagApplication> GetApplications() => _apps;

    public async IAsyncEnumerable<ChatChunk> StreamChatAsync(
        RagApplication app,
        IEnumerable<ChatMessage> history,
        string userMessage,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // Guard: config not yet filled in
        if (string.IsNullOrWhiteSpace(_opts.Endpoint) ||
            _opts.Endpoint.StartsWith("REPLACE", StringComparison.OrdinalIgnoreCase))
        {
            yield return new ChatChunk
            {
                IsError = true,
                IsDone  = true,
                Text    = "Azure AI Foundry is not yet configured. " +
                          "Please fill in AzureFoundry:Endpoint and AzureFoundry:ApiKey in appsettings.json."
            };
            yield break;
        }

        // ── HTTP request lives in a normal async method so try/catch is allowed ──
        var (response, errorChunk) = await SendRequestAsync(app, history, userMessage, ct);

        if (errorChunk is not null)
        {
            yield return errorChunk;
            yield break;
        }

        if (response is null) yield break; // cancelled

        // ── Parse the SSE stream ──────────────────────────────────────────────
        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        List<Citation>? pendingCitations = null;

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            // Use a flag instead of yielding inside the catch clause
            string? line = null;
            bool streamCancelled = false;
            try
            {
                line = await reader.ReadLineAsync(ct);
            }
            catch (OperationCanceledException)
            {
                streamCancelled = true;
            }

            if (streamCancelled) yield break;   // yield is safely outside the catch
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (!line.StartsWith("data: ", StringComparison.Ordinal)) continue;

            var data = line[6..];
            if (data == "[DONE]")
            {
                yield return new ChatChunk { IsDone = true, Citations = pendingCitations };
                yield break;
            }

            // Parse JSON — no yield inside this try/catch, so it is fine
            ChatChunk? chunk = null;
            try
            {
                using var doc = JsonDocument.Parse(data);
                chunk = ParseDelta(doc, ref pendingCitations);
            }
            catch (JsonException) { continue; }

            if (chunk is not null)
                yield return chunk;   // safely outside every try/catch
        }

        yield return new ChatChunk { IsDone = true, Citations = pendingCitations };
    }

    /// <summary>
    /// Performs the HTTP POST to Azure AI Foundry.
    /// Kept as a regular async method so try/catch blocks are unrestricted.
    /// Returns (response, null) on success or (null, errorChunk) on failure.
    /// A (null, null) tuple signals the request was cancelled.
    /// </summary>
    private async Task<(HttpResponseMessage? response, ChatChunk? error)> SendRequestAsync(
        RagApplication app,
        IEnumerable<ChatMessage> history,
        string userMessage,
        CancellationToken ct)
    {
        var requestBody = BuildRequest(app, history, userMessage);
        var url         = BuildUrl(app.DeploymentName);
        var json        = JsonSerializer.Serialize(requestBody);

        try
        {
            var httpReq = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            httpReq.Headers.Add("api-key", _opts.ApiKey);

            var response = await _http.SendAsync(
                httpReq, HttpCompletionOption.ResponseHeadersRead, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                response.Dispose();
                return (null, new ChatChunk
                {
                    IsError = true,
                    IsDone  = true,
                    Text    = $"Azure AI Foundry error {(int)response.StatusCode}: {body}"
                });
            }

            return (response, null);
        }
        catch (OperationCanceledException)
        {
            return (null, null);
        }
        catch (Exception ex)
        {
            return (null, new ChatChunk
            {
                IsError = true,
                IsDone  = true,
                Text    = $"Connection failed: {ex.Message}"
            });
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private string BuildUrl(string deploymentName) =>
        $"{_opts.Endpoint.TrimEnd('/')}/openai/deployments/{deploymentName}" +
        $"/chat/completions?api-version={_opts.ApiVersion}";

    private FoundryRequest BuildRequest(
        RagApplication app,
        IEnumerable<ChatMessage> history,
        string userMessage)
    {
        var messages = history
            .Select(m => new FoundryMessage { role = m.Role, content = m.Content })
            .Append(new FoundryMessage { role = "user", content = userMessage })
            .ToList();

        var req = new FoundryRequest
        {
            model       = app.DeploymentName,
            messages    = messages,
            stream      = true,
            max_tokens  = 1024,
            temperature = 0.7f
        };

        // Add Azure AI Search RAG data source if configured for this app
        if (!string.IsNullOrEmpty(app.SearchIndexName)   &&
            !string.IsNullOrEmpty(_opts.SearchEndpoint)  &&
            !string.IsNullOrEmpty(_opts.SearchApiKey))
        {
            req.data_sources =
            [
                new DataSource
                {
                    parameters = new DataSourceParameters
                    {
                        endpoint        = _opts.SearchEndpoint,
                        index_name      = app.SearchIndexName,
                        authentication  = new DataSourceAuth { key = _opts.SearchApiKey },
                        top_n_documents = 5,
                        query_type      = "semantic",
                        in_scope        = true
                    }
                }
            ];
        }

        return req;
    }

    /// <summary>
    /// Parses one SSE data chunk. Populates pendingCitations when a context
    /// payload arrives (typically an early chunk before text starts).
    /// </summary>
    private static ChatChunk? ParseDelta(JsonDocument doc, ref List<Citation>? pendingCitations)
    {
        if (!doc.RootElement.TryGetProperty("choices", out var choices)) return null;
        if (choices.GetArrayLength() == 0) return null;

        var choice = choices[0];
        if (!choice.TryGetProperty("delta", out var delta)) return null;

        // Citations arrive in delta.context.citations (Azure AI Search RAG)
        if (delta.TryGetProperty("context", out var ctx) &&
            ctx.TryGetProperty("citations", out var citArray))
        {
            pendingCitations = [];
            int idx = 1;
            foreach (var c in citArray.EnumerateArray())
            {
                pendingCitations.Add(new Citation
                {
                    Index    = idx++,
                    Title    = c.TryGetProperty("title",    out var t) ? t.GetString() ?? "Source" : "Source",
                    Url      = c.TryGetProperty("url",      out var u) ? u.GetString() : null,
                    FilePath = c.TryGetProperty("filepath", out var f) ? f.GetString() : null,
                    Excerpt  = c.TryGetProperty("content",  out var x) ? Truncate(x.GetString(), 250) : null
                });
            }
        }

        // Text content
        if (delta.TryGetProperty("content", out var contentEl) &&
            contentEl.ValueKind == JsonValueKind.String)
        {
            var text = contentEl.GetString();
            if (!string.IsNullOrEmpty(text))
                return new ChatChunk { Text = text };
        }

        return null;
    }

    private static string? Truncate(string? s, int max) =>
        s is null ? null : s.Length <= max ? s : s[..max] + "…";
}
