namespace SharedLibrary.Models;

/// <summary>
/// Model for Whisper model information
/// </summary>
public class WhisperModel
{
    public string Name { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string SizeFormatted { get; set; } = string.Empty;
    public string ModelType { get; set; } = string.Empty;
    public string QuantizationLevel { get; set; } = string.Empty;
    public bool IsCurrent { get; set; }
}

