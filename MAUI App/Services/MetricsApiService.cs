using MAUI_App.Models;
using SharedLibrary.Models;
using System.Text.Json;

namespace MAUI_App.Services;

/// <summary>
/// Service for fetching transcription metrics from the WhisperAPI
/// </summary>
public class MetricsApiService : IMetricsApiService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MetricsApiService> _logger;
    private readonly ApiConfiguration _config;
    private readonly JsonSerializerOptions _jsonOptions;

    public MetricsApiService(
        HttpClient httpClient, 
        ILogger<MetricsApiService> logger,
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
    public async Task<ApiResult<List<MAUI_App.Models.TranscriptionMetrics>>> GetMetricsAsync()
    {
        try
        {
            _logger.LogInformation("Fetching transcription metrics from WhisperAPI");
            
            var response = await _httpClient.GetAsync("/api/whisper/metrics");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var metricsContainer = JsonSerializer.Deserialize<SharedLibrary.Models.MetricsContainer>(content, _jsonOptions);
                
                if (metricsContainer?.TranscriptionMetrics != null)
                {
                    _logger.LogInformation("Successfully fetched {Count} metrics", metricsContainer.TranscriptionMetrics.Count);
                    // Convert from SharedLibrary.Models.TranscriptionMetrics to MAUI_App.Models.TranscriptionMetrics
                    var mauiMetrics = metricsContainer.TranscriptionMetrics
                        .Select(m => new MAUI_App.Models.TranscriptionMetrics
                        {
                            Timestamp = m.Timestamp,
                            ModelName = m.ModelName,
                            TranscriptionType = m.TranscriptionType,
                            SessionId = m.SessionId,
                            FileSizeBytes = m.FileSizeBytes,
                            AudioDurationSeconds = m.AudioDurationSeconds,
                            TotalTimeMs = m.TotalTimeMs,
                            PreprocessingTimeMs = m.PreprocessingTimeMs,
                            TranscriptionTimeMs = m.TranscriptionTimeMs,
                            Success = m.Success,
                            ErrorMessage = m.ErrorMessage,
                            TranscribedText = m.TranscribedText
                        })
                        .ToList();
                    return ApiResult<List<MAUI_App.Models.TranscriptionMetrics>>.Success(mauiMetrics);
                }
                
                return ApiResult<List<MAUI_App.Models.TranscriptionMetrics>>.Failure("Failed to deserialize metrics response");
            }
            
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Failed to fetch metrics. Status: {StatusCode}, Content: {Content}", 
                response.StatusCode, errorContent);
            
            return ApiResult<List<MAUI_App.Models.TranscriptionMetrics>>.Failure(
                $"Metrics API request failed with status {response.StatusCode}", 
                (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while fetching metrics");
            return ApiResult<List<MAUI_App.Models.TranscriptionMetrics>>.Failure($"Exception: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<ApiResult<bool>> ClearMetricsAsync()
    {
        try
        {
            _logger.LogInformation("Clearing all transcription metrics");
            
            var response = await _httpClient.DeleteAsync("/api/whisper/metrics");
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully cleared all metrics");
                return ApiResult<bool>.Success(true);
            }
            
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Failed to clear metrics. Status: {StatusCode}, Content: {Content}", 
                response.StatusCode, errorContent);
            
            return ApiResult<bool>.Failure(
                $"Clear metrics API request failed with status {response.StatusCode}", 
                (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while clearing metrics");
            return ApiResult<bool>.Failure($"Exception: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

