namespace SharedLibrary.Models;

/// <summary>
/// Model for tracking transcription performance metrics
/// </summary>
public class TranscriptionMetrics
{
    public DateTime Timestamp { get; set; }
    public string ModelName { get; set; } = string.Empty;
    public string TranscriptionType { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty; // For grouping chunked transcriptions
    public long FileSizeBytes { get; set; }
    public double? AudioDurationSeconds { get; set; }
    public long TotalTimeMs { get; set; }
    public long PreprocessingTimeMs { get; set; }
    public long TranscriptionTimeMs { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string TranscribedText { get; set; } = string.Empty;
}

/// <summary>
/// Container for all transcription metrics
/// </summary>
public class MetricsContainer
{
    public List<TranscriptionMetrics> TranscriptionMetrics { get; set; } = new();
}

