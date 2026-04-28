using ChatBot.Web.Components;
using ChatBot.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Blazor Server with interactive server-side rendering
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Bind AzureFoundry config section early so we can read UseMock
builder.Services.Configure<AzureFoundryOptions>(
    builder.Configuration.GetSection("AzureFoundry"));

bool useMock = builder.Configuration.GetValue<bool>("AzureFoundry:UseMock");

if (useMock)
{
    // Mock service — no HTTP client needed, no Azure credentials required
    builder.Services.AddScoped<IAzureFoundryService, MockAzureFoundryService>();
}
else
{
    // Real Azure AI Foundry service — register typed HttpClient
    builder.Services.AddHttpClient<AzureFoundryService>(client =>
    {
        client.Timeout = TimeSpan.FromMinutes(5); // allow long streaming responses
    });
    builder.Services.AddScoped<IAzureFoundryService, AzureFoundryService>();
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
