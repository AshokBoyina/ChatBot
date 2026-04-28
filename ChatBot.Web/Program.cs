using ChatBot.Web.Components;
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

app.Run();
