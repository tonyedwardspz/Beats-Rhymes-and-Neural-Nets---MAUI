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
/// Error response model
/// </summary>
public record ErrorResponse(string Error);

/// <summary>
/// Response model for transcription results
/// </summary>
public record TranscriptionResponse(string[] Results);

/// <summary>
/// Configuration for LLMAPI settings
/// </summary>
public class ApiConfiguration
{
    public string BaseUrl { get; set; } = "http://localhost:5087";
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
    
    public string WhisperBaseURL { get; set; } = "http://localhost:5087";
}
