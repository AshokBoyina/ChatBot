namespace NICE.Platform.ChatBot.Widget.Models;

/// <summary>A single turn in the conversation.</summary>
public class ChatMessage
{
    public string   Id        { get; init; }  = Guid.NewGuid().ToString("N");
    public string   Role      { get; set; }   = "user";   // "user" | "assistant"
    public string   Content   { get; set; }   = string.Empty;
    public DateTime Timestamp { get; init; }  = DateTime.UtcNow;
    public List<Citation> Citations { get; set; } = [];
}

/// <summary>A RAG source citation returned alongside an answer.</summary>
public class Citation
{
    public int     Index    { get; set; }
    public string  Title    { get; set; } = "Source";
    public string? Url      { get; set; }
    public string? FilePath { get; set; }
    public string? Excerpt  { get; set; }
}

public record ChatApiResponse(
    string?         Reply,
    List<Citation>? Citations,
    string?         Error = null);
