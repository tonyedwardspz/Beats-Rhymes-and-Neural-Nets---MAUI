using SharedLibrary.Models;

namespace MAUI_App.Services;

/// <summary>
/// Interface for LLM LLMAPI service operations
/// </summary>
public interface ILLMApiService
{
    /// <summary>
    /// Gets information about the loaded LLM model
    /// </summary>
    /// <returns>Model information and status</returns>
    Task<ApiResult<ModelInfoResponse>> GetModelInfoAsync();

    /// <summary>
    /// Generates a complete response for the given prompt
    /// </summary>
    /// <param name="prompt">The prompt to generate a response for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Generated response</returns>
    Task<ApiResult<GenerateResponse>> GenerateResponseAsync(string prompt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a streaming response for the given prompt
    /// </summary>
    /// <param name="prompt">The prompt to generate a response for</param>
    /// <param name="onTokenReceived">Callback for each token received</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the streaming operation</returns>
    Task<ApiResult<string>> GenerateStreamingResponseAsync(
        string prompt, 
        Action<string> onTokenReceived, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current LLM configuration
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Current LLM configuration</returns>
    Task<ApiResult<LLMConfigurationResponse>> GetConfigurationAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the LLM configuration
    /// </summary>
    /// <param name="request">Configuration update request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated LLM configuration</returns>
    Task<ApiResult<LLMConfigurationResponse>> UpdateConfigurationAsync(
        LLMConfigurationUpdateRequest request, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new chat session
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Session ID</returns>
    Task<ApiResult<CreateChatSessionResponse>> CreateChatSessionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a message to a chat session
    /// </summary>
    /// <param name="sessionId">The chat session ID</param>
    /// <param name="message">The message to send</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Chat response</returns>
    Task<ApiResult<ChatMessageResponse>> SendChatMessageAsync(
        string sessionId, 
        string message, 
        CancellationToken cancellationToken = default);
}
