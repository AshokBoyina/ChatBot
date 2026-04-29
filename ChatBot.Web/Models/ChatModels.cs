namespace ChatBot.Web.Models;

/// <summary>A configured RAG application / knowledge base selectable from the UI.</summary>
public record RagApplication(
    string Id,
    string Name,
    string Description,
    string DeploymentName,
    string? SearchIndexName);

/// <summary>A single turn in the conversation.</summary>
public class ChatMessage
{
    public string Id        { get; init; } = Guid.NewGuid().ToString("N");
    public string Role      { get; set; }  = "user";        // "user" | "assistant"
    public string Content   { get; set; }  = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
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

/// <summary>A single streamed text chunk from the service layer.</summary>
public class ChatChunk
{
    public string          Text      { get; set; } = string.Empty;
    public List<Citation>? Citations { get; set; }
    public bool            IsDone   { get; set; }
    public bool            IsError  { get; set; }
}

