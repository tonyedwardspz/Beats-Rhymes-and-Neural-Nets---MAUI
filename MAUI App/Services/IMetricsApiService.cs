using MAUI_App.Models;
using SharedLibrary.Models;

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
    Task<ApiResult<List<MAUI_App.Models.TranscriptionMetrics>>> GetMetricsAsync();
    
    /// <summary>
    /// Clears all transcription metrics from the API
    /// </summary>
    /// <returns>Success result</returns>
    Task<ApiResult<bool>> ClearMetricsAsync();
}
