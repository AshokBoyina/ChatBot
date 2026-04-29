using ChatBot.Web.Components;
using ChatBot.Web.Models;
using ChatBot.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Blazor Server with interactive server-side rendering
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Bind NassApi config section
builder.Services.Configure<NassApiOptions>(
    builder.Configuration.GetSection("NassApi"));

bool useMock = builder.Configuration.GetValue<bool>("NassApi:UseMock");

if (useMock)
{
    // Mock service — no HTTP client, no real API endpoint needed
    builder.Services.AddScoped<IChatService, MockChatService>();
}
else
{
    // Real NASS AI Bot API service
    builder.Services.AddHttpClient<NassApiService>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(120);
    });
    builder.Services.AddScoped<IChatService, NassApiService>();
}

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// ── Chat API endpoint — browser fetch() calls this; visible in Network tab ──
app.MapPost("/api/chat", async (
    ChatApiRequest       req,
    IChatService         svc,
    HttpRequest          httpReq,
    CancellationToken    ct) =>
{
    // ── Access-key guard ──────────────────────────────────────────────────────
    var configuredKey = svc.ApiAccessKey;
    if (!string.IsNullOrWhiteSpace(configuredKey))
    {
        var provided = httpReq.Headers["X-API-Access-Key"].FirstOrDefault();
        if (provided != configuredKey)
            return Results.Unauthorized();
    }

    // Resolve the selected RAG application
    var apps = svc.GetApplications();
    var selectedApp = string.IsNullOrEmpty(req.AppId)
        ? apps.FirstOrDefault()
        : apps.FirstOrDefault(a => a.Id == req.AppId);

    // Convert lightweight history items to ChatMessage objects
    var history = req.History
        .Select(h => new ChatMessage { Role = h.Role, Content = h.Content })
        .ToList();

    var result = await svc.GetReplyAsync(selectedApp, history, req.Message, ct);
    return Results.Json(result);
})
.DisableAntiforgery();   // fetch() POSTs don't carry the antiforgery cookie

app.Run();
