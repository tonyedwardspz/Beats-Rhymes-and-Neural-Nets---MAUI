using LLMAPI.Services;

namespace LLMAPI.Interfaces;

/// <summary>
/// Service for managing interactive chat sessions with conversation context
/// </summary>
public interface ILLMChatSessionService
{
    /// <summary>
    /// Creates a new chat session and returns its ID
    /// </summary>
    Task<string> CreateSessionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a message to a chat session and returns the response
    /// </summary>
    Task<string> ChatAsync(string sessionId, string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a message to a chat session and streams the response
    /// </summary>
    IAsyncEnumerable<string> ChatStreamAsync(string sessionId, string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the conversation history for a session
    /// </summary>
    Task<IEnumerable<ChatHistoryItem>> GetHistoryAsync(string sessionId);

    /// <summary>
    /// Deletes a chat session and cleans up its resources
    /// </summary>
    Task<bool> DeleteSessionAsync(string sessionId);

    /// <summary>
    /// Clears the conversation history for a session but keeps the session alive
    /// </summary>
    Task<bool> ClearSessionAsync(string sessionId);

    /// <summary>
    /// Checks if a session exists
    /// </summary>
    bool SessionExists(string sessionId);
}

/// <summary>
/// Represents a message in the chat history
/// </summary>
public record ChatHistoryItem(string Role, string Content);

