
using System.Threading;
using SharedLibrary.Models;

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
    /// <param name="chunkIndex">Index of the chunk for tracking order in chunked transcriptions</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Transcription results</returns>
    Task<ApiResult<TranscriptionResponse>> TranscribeWavAsync(
        Stream audioStream, 
        string fileName, 
        string? transcriptionType = null,
        string? sessionId = null,
        int? chunkIndex = null,
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

    /// <summary>
    /// Gets the list of available Whisper models
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of available models</returns>
    Task<ApiResult<List<WhisperModel>>> GetAvailableModelsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Switches to a different Whisper model
    /// </summary>
    /// <param name="modelName">The name of the model to switch to</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success status</returns>
    Task<ApiResult<string>> SwitchModelAsync(string modelName, CancellationToken cancellationToken = default);
}
