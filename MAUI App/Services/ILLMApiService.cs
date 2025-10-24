
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
}

/// <summary>
/// Result wrapper for LLMAPI operations
/// </summary>
/// <typeparam name="T">The type of the result data</typeparam>
public class ApiResult<T>
{
    public bool IsSuccess { get; set; }
    public T? Data { get; set; }
    public string? ErrorMessage { get; set; }
    public int StatusCode { get; set; }

    public static ApiResult<T> Success(T data, int statusCode = 200)
    {
        return new ApiResult<T>
        {
            IsSuccess = true,
            Data = data,
            StatusCode = statusCode
        };
    }

    public static ApiResult<T> Failure(string errorMessage, int statusCode = 500)
    {
        return new ApiResult<T>
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            StatusCode = statusCode
        };
    }
}
