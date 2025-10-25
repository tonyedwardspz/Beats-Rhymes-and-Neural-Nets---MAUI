using MAUI_App.Models;
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
    public async Task<ApiResult<List<TranscriptionMetrics>>> GetMetricsAsync()
    {
        try
        {
            _logger.LogInformation("Fetching transcription metrics from WhisperAPI");
            
            var response = await _httpClient.GetAsync("/api/whisper/metrics");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var metricsContainer = JsonSerializer.Deserialize<MetricsContainer>(content, _jsonOptions);
                
                if (metricsContainer?.TranscriptionMetrics != null)
                {
                    _logger.LogInformation("Successfully fetched {Count} metrics", metricsContainer.TranscriptionMetrics.Count);
                    return ApiResult<List<TranscriptionMetrics>>.Success(metricsContainer.TranscriptionMetrics);
                }
                
                return ApiResult<List<TranscriptionMetrics>>.Failure("Failed to deserialize metrics response");
            }
            
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Failed to fetch metrics. Status: {StatusCode}, Content: {Content}", 
                response.StatusCode, errorContent);
            
            return ApiResult<List<TranscriptionMetrics>>.Failure(
                $"Metrics API request failed with status {response.StatusCode}", 
                (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while fetching metrics");
            return ApiResult<List<TranscriptionMetrics>>.Failure($"Exception: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

/// <summary>
/// Container for metrics response from API
/// </summary>
public class MetricsContainer
{
    public List<TranscriptionMetrics> TranscriptionMetrics { get; set; } = new();
}
