namespace NICE.Platform.ChatBot.Widget.Services;

using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using NICE.Platform.ChatBot.Widget.Models;

public class NassApiService : IChatService
{
    private readonly HttpClient     _http;
    private readonly NassApiOptions _opts;

    public NassApiService(HttpClient http, IOptions<NassApiOptions> opts)
    {
        _http = http;
        _opts = opts.Value;
        _http.BaseAddress = new Uri(_opts.BaseUrl.TrimEnd('/') + "/");
    }

    public string  AssistantName => _opts.AssistantName;
    public string? ApiAccessKey  => _opts.ApiAccessKey;

    public IReadOnlyList<RagApplication> GetApplications() =>
        _opts.Applications
             .Select(a => new RagApplication(
                 a.Id, a.Name, a.Description, a.DeploymentName, a.SearchIndexName))
             .ToList();

    public async Task<ChatApiResponse> GetReplyAsync(
        RagApplication?          app,
        IEnumerable<ChatMessage> history,
        string                   userMessage,
        CancellationToken        ct = default)
    {
        try
        {
            var payload = BuildPayload(app, history, userMessage);
            var reply   = await CallApiAsync(payload, ct);
            return new ChatApiResponse(reply, null);
        }
        catch (OperationCanceledException) { return new ChatApiResponse(null, null, "Request cancelled."); }
        catch (Exception ex)               { return new ChatApiResponse(null, null, $"API error: {ex.Message}"); }
    }

    public Task<string?> SummarizeAsync(IEnumerable<ChatMessage> history, CancellationToken ct = default)
        => Task.FromResult<string?>(null);

    // ── helpers ───────────────────────────────────────────────────────────────

    private object BuildPayload(RagApplication? app, IEnumerable<ChatMessage> history, string userMessage)
    {
        var messages = new List<object>();
        if (_opts.SendChatHistory)
            foreach (var m in history)
                messages.Add(new { role = m.Role, content = m.Content });
        messages.Add(new { role = "user", content = userMessage });
        return new
        {
            messages,
            deployment_name   = app?.DeploymentName   ?? "default",
            search_index_name = app?.SearchIndexName  ?? string.Empty
        };
    }

    private async Task<string> CallApiAsync(object payload, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, _opts.ChatPath);
        req.Content = JsonContent.Create(payload);

        if (!string.IsNullOrWhiteSpace(_opts.ApiKey))
            req.Headers.Add("X-Api-Key", _opts.ApiKey);
        if (!string.IsNullOrWhiteSpace(_opts.ApiAccessKey))
            req.Headers.Add("X-API-Access-Key", _opts.ApiAccessKey);

        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadAsStringAsync(ct);
        var node = JsonNode.Parse(body);
        return node?[_opts.ResponseField]?.GetValue<string>()
               ?? node?.ToString()
               ?? string.Empty;
    }
}
