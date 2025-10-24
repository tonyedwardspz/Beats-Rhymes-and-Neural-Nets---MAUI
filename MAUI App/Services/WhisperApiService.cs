
namespace MAUI_App.Services;

/// <summary>
/// Service for communicating with the Whisper API
/// </summary>
public class WhisperApiService : IWhisperApiService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WhisperApiService> _logger;
    private readonly ApiConfiguration _config;
    private readonly JsonSerializerOptions _jsonOptions;

    public WhisperApiService(
        HttpClient httpClient, 
        ILogger<WhisperApiService> logger,
        IOptions<ApiConfiguration> config)
    {
        _httpClient = httpClient;
        _logger = logger;
        _config = config.Value;
        
        // Configure HttpClient
        _httpClient.BaseAddress = new Uri(_config.WhisperBaseURL);
        _httpClient.Timeout = _config.Timeout;
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    /// <inheritdoc />
    public async Task<ApiResult<string>> GetModelDetailsAsync()
    {
        try
        {
            _logger.LogInformation("Requesting model details from WhisperAPI");
            
            var response = await _httpClient.GetAsync("/api/whisper/modelDetails");
            
            if (response.IsSuccessStatusCode)
            {
                var modelDetails = await response.Content.ReadAsStringAsync();
                
                if (!string.IsNullOrEmpty(modelDetails))
                {
                    _logger.LogInformation("Successfully retrieved model details: {ModelDetails}", modelDetails);
                    return ApiResult<string>.Success(modelDetails);
                }
                
                return ApiResult<string>.Failure("Failed to get model details response");
            }
            
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Failed to get model details. Status: {StatusCode}, Content: {Content}", 
                response.StatusCode, errorContent);
            
            return ApiResult<string>.Failure(
                $"WhisperAPI request failed with status {response.StatusCode}", 
                (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while getting model details");
            return ApiResult<string>.Failure($"Unexpected error: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<ApiResult<TranscriptionResponse>> TranscribeWavAsync(
        Stream audioStream, 
        string fileName, 
        CancellationToken cancellationToken = default)
    {
        if (audioStream == null || audioStream.Length == 0)
        {
            return ApiResult<TranscriptionResponse>.Failure("Audio stream cannot be null or empty", 400);
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return ApiResult<TranscriptionResponse>.Failure("File name cannot be empty", 400);
        }

        try
        {
            _logger.LogInformation("Transcribing WAV file: {FileName} (size: {Size} bytes)", 
                fileName, audioStream.Length);
            
            // Create multipart form data
            using var formData = new MultipartFormDataContent();
            
            // Create a stream content for the audio file
            var audioContent = new StreamContent(audioStream);
            audioContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
            
            // Add the file to the form data
            formData.Add(audioContent, "File", fileName);
            
            var response = await _httpClient.PostAsync("/api/whisper/transcribe-wav", formData, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var transcriptionResult = await response.Content.ReadFromJsonAsync<TranscriptionResponse>(_jsonOptions, cancellationToken);
                
                if (transcriptionResult != null)
                {
                    _logger.LogInformation("Successfully transcribed audio file");
                    return ApiResult<TranscriptionResponse>.Success(transcriptionResult);
                }
                
                return ApiResult<TranscriptionResponse>.Failure("Failed to deserialize transcription response");
            }
            
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Failed to transcribe audio. Status: {StatusCode}, Content: {Content}", 
                response.StatusCode, errorContent);
            
            return ApiResult<TranscriptionResponse>.Failure(
                $"WhisperAPI transcription failed with status {response.StatusCode}", 
                (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while transcribing audio");
            return ApiResult<TranscriptionResponse>.Failure($"Unexpected error: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        GC.SuppressFinalize(this);
    }
}
