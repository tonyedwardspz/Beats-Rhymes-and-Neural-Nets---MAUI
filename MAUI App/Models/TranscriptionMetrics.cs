namespace MAUI_App.Models;

/// <summary>
/// Model for transcription performance metrics
/// </summary>
public class TranscriptionMetrics
{
    public DateTime Timestamp { get; set; }
    public string ModelName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public double? AudioDurationSeconds { get; set; }
    public long TotalTimeMs { get; set; }
    public long PreprocessingTimeMs { get; set; }
    public long TranscriptionTimeMs { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }

    // Computed properties for display
    public string FileSizeDisplay => FormatFileSize(FileSizeBytes);
    public string DurationDisplay => AudioDurationSeconds?.ToString("F2") + "s" ?? "N/A";
    public string TotalTimeDisplay => $"{TotalTimeMs}ms";
    public string PreprocessingTimeDisplay => $"{PreprocessingTimeMs}ms";
    public string TranscriptionTimeDisplay => $"{TranscriptionTimeMs}ms";
    public string SuccessDisplay => Success ? "✅" : "❌";
    public string TimestampDisplay => Timestamp.ToString("MM/dd HH:mm:ss");

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
}
