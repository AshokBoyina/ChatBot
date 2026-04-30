namespace NICE.Platform.ChatBot.Widget;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NICE.Platform.ChatBot.Widget.Services;

public static class ChatWidgetServiceExtensions
{
    /// <summary>
    /// Registers the NASS Chat Widget services.
    /// Reads configuration from the "NassApi" section of appsettings.json.
    /// </summary>
    public static IServiceCollection AddChatWidget(
        this IServiceCollection services,
        IConfiguration          configuration)
    {
        services.Configure<NassApiOptions>(configuration.GetSection("NassApi"));

        bool useMock = configuration.GetValue<bool>("NassApi:UseMock");

        if (useMock)
        {
            services.AddScoped<IChatService, MockChatService>();
        }
        else
        {
            services.AddHttpClient<NassApiService>(client =>
                client.Timeout = TimeSpan.FromSeconds(120));
            services.AddScoped<IChatService, NassApiService>();
        }

        return services;
    }
}
