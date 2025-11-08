namespace MAUI_App.Models;

/// <summary>
/// Request model for generating LLM responses
/// </summary>
public record GenerateRequest(string Prompt);

/// <summary>
/// Response model for generated LLM responses
/// </summary>
public record GenerateResponse(string Prompt, string Response);

/// <summary>
/// Response model for model information
/// </summary>
public record ModelInfoResponse(string ModelInfo, bool IsReady);


/// <summary>
/// Configuration for LLMAPI settings
/// </summary>
public class ApiConfiguration
{
    public string BaseUrl { get; set; } = "http://localhost:5087";
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
    
    public string WhisperBaseURL { get; set; } = "http://localhost:5087";
}

/// <summary>
/// Response model for LLM configuration
/// </summary>
public record LLMConfigurationResponse(
    string ModelPath,
    int ContextSize,
    int GpuLayerCount,
    int BatchSize,
    int? Threads);

/// <summary>
/// Request model for updating LLM configuration
/// </summary>
public record LLMConfigurationUpdateRequest(
    int ContextSize,
    int GpuLayerCount,
    int BatchSize,
    int? Threads);

// Chat session DTOs
/// <summary>
/// Response model for creating a chat session
/// </summary>
public record CreateChatSessionResponse(string SessionId);

/// <summary>
/// Request model for sending a chat message
/// </summary>
public record ChatMessageRequest(string Message);

/// <summary>
/// Response model for chat message
/// </summary>
public record ChatMessageResponse(string SessionId, string Message, string Response);
