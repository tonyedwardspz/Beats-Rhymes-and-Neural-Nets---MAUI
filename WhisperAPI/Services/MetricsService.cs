using System.Text.Json;
using WhisperAPI.Models;

namespace WhisperAPI.Services;

/// <summary>
/// Service for collecting and storing transcription performance metrics
/// </summary>
public class MetricsService : IMetricsService
{
    private readonly string _metricsFilePath;
    private readonly ILogger<MetricsService> _logger;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions;

    public MetricsService(ILogger<MetricsService> logger)
    {
        _logger = logger;
        _metricsFilePath = Path.Combine("Metrics", "transcription-metrics.json");
        
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        
        // Ensure metrics directory exists
        var metricsDir = Path.GetDirectoryName(_metricsFilePath);
        if (!string.IsNullOrEmpty(metricsDir) && !Directory.Exists(metricsDir))
        {
            Directory.CreateDirectory(metricsDir);
        }
    }

    public async Task RecordMetricsAsync(TranscriptionMetrics metrics)
    {
        await _fileLock.WaitAsync();
        try
        {
            _logger.LogInformation("Recording metrics for {ModelName} - Total: {TotalTime}ms, Success: {Success}", 
                metrics.ModelName, metrics.TotalTimeMs, metrics.Success);

            // Load existing metrics
            var container = await LoadMetricsAsync();
            
            // Add new metrics
            container.TranscriptionMetrics.Add(metrics);
            
            // Save back to file
            await SaveMetricsAsync(container);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record metrics");
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<List<TranscriptionMetrics>> GetAllMetricsAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            var container = await LoadMetricsAsync();
            return container.TranscriptionMetrics;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task ExportMetricsAsync(string? filePath = null)
    {
        var exportPath = filePath ?? _metricsFilePath;
        await _fileLock.WaitAsync();
        try
        {
            var container = await LoadMetricsAsync();
            var json = JsonSerializer.Serialize(container, _jsonOptions);
            await File.WriteAllTextAsync(exportPath, json);
            _logger.LogInformation("Metrics exported to {FilePath}", exportPath);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task<MetricsContainer> LoadMetricsAsync()
    {
        if (!File.Exists(_metricsFilePath))
        {
            return new MetricsContainer();
        }

        try
        {
            var json = await File.ReadAllTextAsync(_metricsFilePath);
            var container = JsonSerializer.Deserialize<MetricsContainer>(json, _jsonOptions);
            return container ?? new MetricsContainer();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load existing metrics, starting with empty container");
            return new MetricsContainer();
        }
    }

    private async Task SaveMetricsAsync(MetricsContainer container)
    {
        var json = JsonSerializer.Serialize(container, _jsonOptions);
        await File.WriteAllTextAsync(_metricsFilePath, json);
    }
}
