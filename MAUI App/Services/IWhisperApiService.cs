
using System.Threading;

namespace MAUI_App.Services;

/// <summary>
/// Interface for Whisper API service operations
/// </summary>
public interface IWhisperApiService
{
    /// <summary>
    /// Gets information about the loaded Whisper model
    /// </summary>
    /// <returns>Model information and status</returns>
    Task<ApiResult<string>> GetModelDetailsAsync();

    /// <summary>
    /// Transcribes a WAV audio file
    /// </summary>
    /// <param name="audioStream">The audio stream to transcribe</param>
    /// <param name="fileName">The name of the audio file</param>
    /// <param name="transcriptionType">The type of transcription (e.g., "Streaming", "File Upload")</param>
    /// <param name="sessionId">Session ID for grouping related transcriptions</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Transcription results</returns>
    Task<ApiResult<TranscriptionResponse>> TranscribeWavAsync(
        Stream audioStream, 
        string fileName, 
        string? transcriptionType = null,
        string? sessionId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Transcribes an audio file from the file system
    /// </summary>
    /// <param name="filePath">The path to the audio file to transcribe</param>
    /// <param name="transcriptionType">The type of transcription (e.g., "File Upload")</param>
    /// <param name="sessionId">Session ID for grouping related transcriptions</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Transcription results</returns>
    Task<ApiResult<TranscriptionResponse>> TranscribeFileAsync(
        string filePath,
        string? transcriptionType = null,
        string? sessionId = null,
        CancellationToken cancellationToken = default);
}
