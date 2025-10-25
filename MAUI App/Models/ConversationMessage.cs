namespace MAUI_App.Models;

/// <summary>
/// Represents a message in a conversation
/// </summary>
public class ConversationMessage
{
    public string Content { get; set; } = string.Empty;
    public bool IsUser { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
