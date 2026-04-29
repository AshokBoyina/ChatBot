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

// ── Minimal-API /api/chat shapes ──────────────────────────────────────────────

/// <summary>Request body posted from the browser to POST /api/chat.</summary>
public record ChatApiRequest(
    string              Message,
    string?             AppId,
    List<ChatHistoryItem> History);

/// <summary>A single history turn sent from the browser.</summary>
public record ChatHistoryItem(string Role, string Content);

/// <summary>Response body returned by POST /api/chat.</summary>
public record ChatApiResponse(
    string?        Reply,
    List<Citation>? Citations,
    string?        Error = null);

