using MAUI_App.Models;

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
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Transcription results</returns>
    Task<ApiResult<TranscriptionResponse>> TranscribeWavAsync(
        Stream audioStream, 
        string fileName, 
        CancellationToken cancellationToken = default);
}
