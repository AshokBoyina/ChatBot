namespace NICE.Platform.ChatBot.Widget.Services;

/// <summary>Strongly-typed binding for the "NassApi" appsettings section.</summary>
public class NassApiOptions
{
    public string  AssistantName   { get; set; } = "READI Assistant";
    public bool    UseMock         { get; set; }
    public string  BaseUrl         { get; set; } = string.Empty;
    public string  ChatPath        { get; set; } = "/v1/AI/Bot/chat";
    public string? ApiKey          { get; set; }
    public string? ApiAccessKey    { get; set; }
    public bool    SendChatHistory { get; set; } = true;
    public string  ResponseField   { get; set; } = "reply";
}
