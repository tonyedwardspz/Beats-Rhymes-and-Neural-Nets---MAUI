using SharedLibrary.Models;
using System.Threading;

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
        string? transcriptionType = null,
        string? sessionId = null,
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
            
            // Add transcription type and session ID if provided
            if (!string.IsNullOrEmpty(transcriptionType))
            {
                formData.Add(new StringContent(transcriptionType), "TranscriptionType");
            }
            
            if (!string.IsNullOrEmpty(sessionId))
            {
                formData.Add(new StringContent(sessionId), "SessionId");
            }
            
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

    /// <inheritdoc />
    public async Task<ApiResult<TranscriptionResponse>> TranscribeFileAsync(
        string filePath,
        string? transcriptionType = null,
        string? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return ApiResult<TranscriptionResponse>.Failure("File path cannot be null or empty", 400);
        }

        if (!File.Exists(filePath))
        {
            return ApiResult<TranscriptionResponse>.Failure($"File not found: {filePath}", 404);
        }

        try
        {
            _logger.LogInformation("Transcribing file: {FilePath} (size: {Size} bytes)", 
                filePath, new FileInfo(filePath).Length);
            
            // Create multipart form data
            using var formData = new MultipartFormDataContent();
            
            // Read the file and create stream content
            using var fileStream = File.OpenRead(filePath);
            var fileContent = new StreamContent(fileStream);
            
            // Set content type based on file extension
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            var contentType = extension switch
            {
                ".wav" => "audio/wav",
                ".mp3" => "audio/mpeg",
                ".m4a" => "audio/mp4",
                ".aac" => "audio/aac",
                ".ogg" => "audio/ogg",
                ".flac" => "audio/flac",
                _ => "audio/wav" // Default to wav
            };
            
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
            
            // Add the file to the form data
            formData.Add(fileContent, "File", Path.GetFileName(filePath));
            
            // Add transcription type and session ID if provided
            if (!string.IsNullOrEmpty(transcriptionType))
            {
                formData.Add(new StringContent(transcriptionType), "TranscriptionType");
            }
            
            if (!string.IsNullOrEmpty(sessionId))
            {
                formData.Add(new StringContent(sessionId), "SessionId");
            }
            
            var response = await _httpClient.PostAsync("/api/whisper/transcribe-wav", formData, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var transcriptionResult = await response.Content.ReadFromJsonAsync<TranscriptionResponse>(_jsonOptions, cancellationToken);
                
                if (transcriptionResult != null)
                {
                    _logger.LogInformation("Successfully transcribed file: {FilePath}", filePath);
                    return ApiResult<TranscriptionResponse>.Success(transcriptionResult);
                }
                
                return ApiResult<TranscriptionResponse>.Failure("Failed to deserialize transcription response");
            }
            
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Failed to transcribe file. Status: {StatusCode}, Content: {Content}", 
                response.StatusCode, errorContent);
            
            return ApiResult<TranscriptionResponse>.Failure(
                $"WhisperAPI transcription failed with status {response.StatusCode}", 
                (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while transcribing file: {FilePath}", filePath);
            return ApiResult<TranscriptionResponse>.Failure($"Unexpected error: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<ApiResult<List<WhisperModel>>> GetAvailableModelsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Requesting available models from WhisperAPI");
            
            var response = await _httpClient.GetAsync("/api/whisper/models", cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var models = await response.Content.ReadFromJsonAsync<List<WhisperModel>>(_jsonOptions, cancellationToken);
                
                if (models != null)
                {
                    _logger.LogInformation("Successfully retrieved {Count} available models", models.Count);
                    return ApiResult<List<WhisperModel>>.Success(models);
                }
                
                return ApiResult<List<WhisperModel>>.Failure("Failed to deserialize models response");
            }
            
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Failed to get available models. Status: {StatusCode}, Content: {Content}", 
                response.StatusCode, errorContent);
            
            return ApiResult<List<WhisperModel>>.Failure(
                $"WhisperAPI request failed with status {response.StatusCode}", 
                (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while getting available models");
            return ApiResult<List<WhisperModel>>.Failure($"Unexpected error: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<ApiResult<string>> SwitchModelAsync(string modelName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modelName))
        {
            return ApiResult<string>.Failure("Model name cannot be empty", 400);
        }

        try
        {
            _logger.LogInformation("Switching to model: {ModelName}", modelName);
            
            var request = new { ModelName = modelName };
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync("/api/whisper/models/switch", content, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogInformation("Successfully switched to model: {ModelName}", modelName);
                return ApiResult<string>.Success($"Successfully switched to model: {modelName}");
            }
            
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Failed to switch model. Status: {StatusCode}, Content: {Content}", 
                response.StatusCode, errorContent);
            
            return ApiResult<string>.Failure(
                $"WhisperAPI model switch failed with status {response.StatusCode}", 
                (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while switching model: {ModelName}", modelName);
            return ApiResult<string>.Failure($"Unexpected error: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        GC.SuppressFinalize(this);
    }
}
