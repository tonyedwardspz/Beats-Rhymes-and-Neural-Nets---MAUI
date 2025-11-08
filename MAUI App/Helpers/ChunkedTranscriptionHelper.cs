using SharedLibrary.Models;

namespace MAUI_App.Helpers;

/// <summary>
/// Helper class for managing chunked transcription state and operations
/// </summary>
public class ChunkedTranscriptionHelper
{
    private readonly Dictionary<int, string> _chunkResults = new();
    private int _nextDisplayIndex = 0;

    /// <summary>
    /// Clears all chunk results and resets the display index
    /// </summary>
    public void Reset()
    {
        _chunkResults.Clear();
        _nextDisplayIndex = 0;
    }

    /// <summary>
    /// Stores a transcription result for a specific chunk index
    /// </summary>
    public void StoreChunkResult(int chunkIndex, string transcriptionText)
    {
        _chunkResults[chunkIndex] = transcriptionText;
    }

    /// <summary>
    /// Updates the transcription display by building a string from chunks in order
    /// </summary>
    /// <returns>The formatted transcription text with all chunks in order</returns>
    public string BuildTranscriptionDisplay()
    {
        var resultBuilder = new System.Text.StringBuilder();
        
        // Display chunks in order, starting from nextDisplayIndex
        while (_chunkResults.ContainsKey(_nextDisplayIndex))
        {
            resultBuilder.AppendLine(_chunkResults[_nextDisplayIndex]);
            _nextDisplayIndex++;
        }
        
        return resultBuilder.ToString();
    }

    /// <summary>
    /// Processes a transcription API result and extracts the transcription text
    /// </summary>
    /// <param name="result">The API result from transcription</param>
    /// <returns>The cleaned transcription text, or "[No speech detected]" if empty</returns>
    public static string ExtractTranscriptionText(ApiResult<TranscriptionResponse> result)
    {
        if (result.IsSuccess && result.Data != null && result.Data.Results.Length > 0)
        {
            var transcription = CleanTranscriptionText(string.Join(" ", result.Data.Results));
            if (transcription != "[BLANK_AUDIO]")
            {
                return transcription;
            }
        }
        
        return "[No speech detected]";
    }

    /// <summary>
    /// Cleans transcription text by removing timestamps and formatting artifacts
    /// </summary>
    /// <param name="text">The raw transcription text</param>
    /// <returns>The cleaned transcription text</returns>
    public static string CleanTranscriptionText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        // Use regex to remove timestamps like "00:00:00->00:00:02: " or "00:00:01.400000->00:00:02.500000: " from the beginning of the text
        var timestampPattern = @"^\d{2}:\d{2}:\d{2}(?:\.\d+)?->\d{2}:\d{2}:\d{2}(?:\.\d+)?:\s*";
        var cleanedText = System.Text.RegularExpressions.Regex.Replace(text, timestampPattern, "");
        
        // Remove leading "- " if present (some transcription formats add this)
        if (cleanedText.Length > 2 && cleanedText.StartsWith('-'))
        {
            cleanedText = cleanedText.Substring(2);
        }
        
        return cleanedText.Trim();
    }
}

