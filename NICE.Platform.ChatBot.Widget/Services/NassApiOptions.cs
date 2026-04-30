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
    public List<RagAppConfig> Applications { get; set; } = [];
}

public class RagAppConfig
{
    public string  Id              { get; set; } = string.Empty;
    public string  Name            { get; set; } = string.Empty;
    public string  Description     { get; set; } = string.Empty;
    public string  DeploymentName  { get; set; } = "default";
    public string? SearchIndexName { get; set; }
}
