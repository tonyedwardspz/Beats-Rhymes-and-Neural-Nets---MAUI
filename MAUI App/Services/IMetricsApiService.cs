using MAUI_App.Models;

namespace MAUI_App.Services;

/// <summary>
/// Service for fetching transcription metrics from the WhisperAPI
/// </summary>
public interface IMetricsApiService
{
    /// <summary>
    /// Gets all transcription metrics from the API
    /// </summary>
    /// <returns>List of transcription metrics</returns>
    Task<ApiResult<List<TranscriptionMetrics>>> GetMetricsAsync();
}
