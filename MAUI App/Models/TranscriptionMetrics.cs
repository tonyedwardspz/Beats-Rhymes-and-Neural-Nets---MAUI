using SharedLibrary.Models;

namespace MAUI_App.Models;

/// <summary>
/// MAUI-specific display extension for TranscriptionMetrics
/// Uses inheritance to add display properties while maintaining compatibility with SharedLibrary model
/// </summary>
public class TranscriptionMetrics : SharedLibrary.Models.TranscriptionMetrics
{
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

