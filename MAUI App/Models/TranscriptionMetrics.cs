namespace MAUI_App.Models;

/// <summary>
/// Model for transcription performance metrics
/// </summary>
public class TranscriptionMetrics
{
    public DateTime Timestamp { get; set; }
    public string ModelName { get; set; } = string.Empty;
    public string TranscriptionType { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public double? AudioDurationSeconds { get; set; }
    public long TotalTimeMs { get; set; }
    public long PreprocessingTimeMs { get; set; }
    public long TranscriptionTimeMs { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string TranscribedText { get; set; } = string.Empty;

    // Computed properties for display
    public string FileSizeDisplay => FormatFileSize(FileSizeBytes);
    public string DurationDisplay => FormatDuration(AudioDurationSeconds);
    public string TotalTimeDisplay => $"{TotalTimeMs}ms";
    public string PreprocessingTimeDisplay => $"{PreprocessingTimeMs}ms";
    public string TranscriptionTimeDisplay => $"{TranscriptionTimeMs / 1000.0:F2}s";
    public string SuccessDisplay => Success ? "✅" : "❌";
    public string TimestampDisplay => Timestamp.ToString("MM/dd HH:mm:ss");
    public string TranscribedTextDisplay => string.IsNullOrEmpty(TranscribedText) ? "N/A" : 
        (TranscribedText.Length > 50 ? TranscribedText.Substring(0, 50) + "..." : TranscribedText);

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }

    private static string FormatDuration(double? seconds)
    {
        if (seconds == null) return "N/A";
        
        var totalSeconds = (int)seconds.Value;
        var minutes = totalSeconds / 60;
        var remainingSeconds = totalSeconds % 60;
        
        return $"{minutes}:{remainingSeconds:D2}";
    }
}
