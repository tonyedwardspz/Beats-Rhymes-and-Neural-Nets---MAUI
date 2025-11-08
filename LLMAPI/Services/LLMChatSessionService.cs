using System.Collections.Concurrent;
using LLMAPI.Interfaces;

namespace LLMAPI.Services;

/// <summary>
/// Service for managing interactive chat sessions with conversation context
/// </summary>
public class LLMChatSessionService : ILLMChatSessionService, IDisposable
{
    private readonly ILLMModelService _modelService;
    private readonly ILogger<LLMChatSessionService> _logger;
    private readonly ConcurrentDictionary<string, ChatSessionWrapper> _sessions = new();
    private readonly SemaphoreSlim _initializationSemaphore = new(1, 1);

    public LLMChatSessionService(
        ILLMModelService modelService,
        ILogger<LLMChatSessionService> logger)
    {
        _modelService = modelService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new chat session and returns its ID
    /// </summary>
    public async Task<string> CreateSessionAsync(CancellationToken cancellationToken = default)
    {
        // Ensure model is initialized
        if (!_modelService.IsReady)
        {
            await _modelService.InitializeAsync();
        }

        var model = _modelService.GetModel();
        var parameters = _modelService.GetModelParameters();

        if (model == null || parameters == null)
        {
            throw new InvalidOperationException("Model is not initialized. Please ensure the model is loaded.");
        }

        var sessionId = Guid.NewGuid().ToString();
        
        try
        {
            // Create a new context from the shared model
            var context = model.CreateContext(parameters);
            var executor = new InteractiveExecutor(context);
            var chatHistory = new ChatHistory();
            var session = new ChatSession(executor, chatHistory);

            var wrapper = new ChatSessionWrapper
            {
                SessionId = sessionId,
                Context = context,
                Executor = executor,
                ChatSession = session,
                ChatHistory = chatHistory,
                CreatedAt = DateTime.UtcNow
            };

            _sessions.TryAdd(sessionId, wrapper);
            _logger.LogInformation("Created new chat session: {SessionId}", sessionId);

            return sessionId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create chat session");
            throw;
        }
    }

    /// <summary>
    /// Sends a message to a chat session and returns the response
    /// </summary>
    public async Task<string> ChatAsync(string sessionId, string message, CancellationToken cancellationToken = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var wrapper))
        {
            throw new KeyNotFoundException($"Session {sessionId} not found");
        }

        var response = new List<string>();
        
        try
        {
            var inferenceParams = new InferenceParams
            {
                AntiPrompts = new List<string> { "User:", "\n\nUser:" }
            };

            var userMessage = new ChatHistory.Message(AuthorRole.User, message);
            await foreach (var token in wrapper.ChatSession.ChatAsync(userMessage, inferenceParams, cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested)
                    break;
                    
                response.Add(token);
            }

            return string.Join("", response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during chat for session {SessionId}", sessionId);
            throw;
        }
    }

    /// <summary>
    /// Sends a message to a chat session and streams the response
    /// </summary>
    public async IAsyncEnumerable<string> ChatStreamAsync(
        string sessionId, 
        string message,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var wrapper))
        {
            throw new KeyNotFoundException($"Session {sessionId} not found");
        }

        var inferenceParams = new InferenceParams
        {
            AntiPrompts = new List<string> { "User:", "\n\nUser:" }
        };

        var userMessage = new ChatHistory.Message(AuthorRole.User, message);
        await foreach (var token in wrapper.ChatSession.ChatAsync(userMessage, inferenceParams, cancellationToken))
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;
                
            yield return token;
        }
    }

    /// <summary>
    /// Gets the conversation history for a session
    /// </summary>
    public Task<IEnumerable<ChatHistoryItem>> GetHistoryAsync(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var wrapper))
        {
            throw new KeyNotFoundException($"Session {sessionId} not found");
        }

        var history = wrapper.ChatHistory.Messages.Select(msg => 
            new ChatHistoryItem(
                msg.AuthorRole.ToString(),
                msg.Content
            ));

        return Task.FromResult<IEnumerable<ChatHistoryItem>>(history.ToList());
    }

    /// <summary>
    /// Deletes a chat session and cleans up its resources
    /// </summary>
    public Task<bool> DeleteSessionAsync(string sessionId)
    {
        if (!_sessions.TryRemove(sessionId, out var wrapper))
        {
            return Task.FromResult(false);
        }

        try
        {
            wrapper.Context?.Dispose();
            _logger.LogInformation("Deleted chat session: {SessionId}", sessionId);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing session {SessionId}", sessionId);
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Clears the conversation history for a session but keeps the session alive
    /// </summary>
    public Task<bool> ClearSessionAsync(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var wrapper))
        {
            return Task.FromResult(false);
        }

        try
        {
            wrapper.ChatHistory.Messages.Clear();
            _logger.LogInformation("Cleared history for chat session: {SessionId}", sessionId);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing session {SessionId}", sessionId);
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Checks if a session exists
    /// </summary>
    public bool SessionExists(string sessionId)
    {
        return _sessions.ContainsKey(sessionId);
    }

    public void Dispose()
    {
        // Dispose all sessions
        foreach (var session in _sessions.Values)
        {
            try
            {
                session.Context?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing session {SessionId}", session.SessionId);
            }
        }

        _sessions.Clear();
        _initializationSemaphore?.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Wrapper class to hold all components of a chat session
    /// </summary>
    private class ChatSessionWrapper
    {
        public string SessionId { get; set; } = string.Empty;
        public LLamaContext Context { get; set; } = null!;
        public InteractiveExecutor Executor { get; set; } = null!;
        public ChatSession ChatSession { get; set; } = null!;
        public ChatHistory ChatHistory { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }
}

