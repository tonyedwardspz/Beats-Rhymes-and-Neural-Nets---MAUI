using SharedLibrary.Models;

namespace WhisperAPI.Services;

/// <summary>
/// Service for collecting and storing transcription performance metrics
/// </summary>
public interface IMetricsService
{
    /// <summary>
    /// Records a new transcription metric
    /// </summary>
    /// <param name="metrics">The metrics to record</param>
    Task RecordMetricsAsync(TranscriptionMetrics metrics);
    
    /// <summary>
    /// Gets all recorded metrics
    /// </summary>
    /// <returns>All recorded metrics</returns>
    Task<List<TranscriptionMetrics>> GetAllMetricsAsync();
    
    /// <summary>
    /// Exports metrics to JSON file
    /// </summary>
    /// <param name="filePath">Optional file path, defaults to ./Metrics/transcription-metrics.json</param>
    Task ExportMetricsAsync(string? filePath = null);
    
    /// <summary>
    /// Clears all transcription metrics
    /// </summary>
    /// <returns>Task representing the async operation</returns>
    Task ClearMetricsAsync();
}
