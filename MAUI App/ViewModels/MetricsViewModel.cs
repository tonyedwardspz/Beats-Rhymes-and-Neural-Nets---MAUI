using MAUI_App.Models;
using MAUI_App.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace MAUI_App.ViewModels;

public class MetricsViewModel : INotifyPropertyChanged
{
    private readonly IMetricsApiService _metricsApiService;
    private readonly ILogger<MetricsViewModel> _logger;
    
    private ObservableCollection<TranscriptionMetrics> _metrics = new();
    private bool _isLoading;
    private string _sortColumn = "Timestamp";
    private bool _sortAscending = false;

    public MetricsViewModel(IMetricsApiService metricsApiService, ILogger<MetricsViewModel> logger)
    {
        _metricsApiService = metricsApiService;
        _logger = logger;
        
        LoadMetricsCommand = new Command(async () => await LoadMetricsAsync());
        SortCommand = new Command<string>(SortByColumn);
    }

    public ObservableCollection<TranscriptionMetrics> Metrics
    {
        get => _metrics;
        set
        {
            _metrics = value;
            OnPropertyChanged();
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            _isLoading = value;
            OnPropertyChanged();
        }
    }

    public string SortColumn
    {
        get => _sortColumn;
        set
        {
            _sortColumn = value;
            OnPropertyChanged();
        }
    }

    public bool SortAscending
    {
        get => _sortAscending;
        set
        {
            _sortAscending = value;
            OnPropertyChanged();
        }
    }

    public ICommand LoadMetricsCommand { get; }
    public ICommand SortCommand { get; }

    public async Task LoadMetricsAsync()
    {
        IsLoading = true;
        try
        {
            _logger.LogInformation("Loading transcription metrics");
            
            var result = await _metricsApiService.GetMetricsAsync();
            
            if (result.IsSuccess && result.Data != null)
            {
                Metrics.Clear();
                
                // Process and aggregate metrics by session ID
                var aggregatedMetrics = AggregateMetricsBySession(result.Data);
                
                foreach (var metric in aggregatedMetrics)
                {
                    Metrics.Add(metric);
                }
                
                _logger.LogInformation("Loaded {Count} raw metrics, aggregated to {AggregatedCount} metrics", 
                    result.Data.Count, aggregatedMetrics.Count);
                
                // Apply current sort
                SortByColumn(_sortColumn);
            }
            else
            {
                _logger.LogError("Failed to load metrics: {Error}", result.ErrorMessage);
                await Application.Current.MainPage.DisplayAlert("Error", 
                    $"Failed to load metrics: {result.ErrorMessage}", "OK");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while loading metrics");
            await Application.Current.MainPage.DisplayAlert("Error", 
                $"Exception occurred: {ex.Message}", "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private List<TranscriptionMetrics> AggregateMetricsBySession(List<TranscriptionMetrics> rawMetrics)
    {
        var aggregatedMetrics = new List<TranscriptionMetrics>();
        
        // Group metrics by session ID
        var sessionGroups = rawMetrics
            .Where(m => !string.IsNullOrEmpty(m.SessionId))
            .GroupBy(m => m.SessionId)
            .ToList();
        
        // Process each session group
        foreach (var sessionGroup in sessionGroups)
        {
            var sessionMetrics = sessionGroup.ToList();
            
            if (sessionMetrics.Count == 1)
            {
                // Single metric - no aggregation needed
                aggregatedMetrics.Add(sessionMetrics.First());
            }
            else
            {
                // Multiple metrics with same session ID - aggregate them
                var aggregatedMetric = AggregateSessionMetrics(sessionMetrics);
                aggregatedMetrics.Add(aggregatedMetric);
                
                _logger.LogInformation("Aggregated {Count} metrics for session {SessionId} into single entry", 
                    sessionMetrics.Count, sessionGroup.Key);
            }
        }
        
        // Add any metrics without session IDs (shouldn't happen with new implementation, but for safety)
        var metricsWithoutSession = rawMetrics.Where(m => string.IsNullOrEmpty(m.SessionId)).ToList();
        aggregatedMetrics.AddRange(metricsWithoutSession);
        
        return aggregatedMetrics;
    }
    
    private TranscriptionMetrics AggregateSessionMetrics(List<TranscriptionMetrics> sessionMetrics)
    {
        if (sessionMetrics.Count == 0)
            throw new ArgumentException("Cannot aggregate empty session metrics");
        
        // Use the first metric as the base
        var baseMetric = sessionMetrics.First();
        
        // Aggregate the values
        var aggregatedMetric = new TranscriptionMetrics
        {
            Timestamp = sessionMetrics.Min(m => m.Timestamp), // Use earliest timestamp
            ModelName = baseMetric.ModelName,
            TranscriptionType = baseMetric.TranscriptionType,
            SessionId = baseMetric.SessionId,
            FileSizeBytes = sessionMetrics.Sum(m => m.FileSizeBytes), // Sum file sizes
            AudioDurationSeconds = sessionMetrics.Sum(m => m.AudioDurationSeconds ?? 0), // Sum durations
            TotalTimeMs = sessionMetrics.Sum(m => m.TotalTimeMs), // Sum total times
            PreprocessingTimeMs = sessionMetrics.Sum(m => m.PreprocessingTimeMs), // Sum preprocessing times
            TranscriptionTimeMs = sessionMetrics.Sum(m => m.TranscriptionTimeMs), // Sum transcription times
            Success = sessionMetrics.All(m => m.Success), // Success only if all succeeded
            ErrorMessage = sessionMetrics.Any(m => !string.IsNullOrEmpty(m.ErrorMessage)) 
                ? string.Join("; ", sessionMetrics.Where(m => !string.IsNullOrEmpty(m.ErrorMessage)).Select(m => m.ErrorMessage))
                : null
        };
        
        return aggregatedMetric;
    }

    private void SortByColumn(string columnName)
    {
        if (string.IsNullOrEmpty(columnName) || Metrics.Count == 0)
            return;

        // Toggle sort direction if clicking the same column
        if (_sortColumn == columnName)
        {
            _sortAscending = !_sortAscending;
        }
        else
        {
            _sortColumn = columnName;
            _sortAscending = true;
        }

        OnPropertyChanged(nameof(SortColumn));
        OnPropertyChanged(nameof(SortAscending));

        var sortedMetrics = _sortAscending
            ? SortMetricsAscending(columnName)
            : SortMetricsDescending(columnName);

        // Update the observable collection by replacing the entire collection
        var newMetrics = new ObservableCollection<TranscriptionMetrics>(sortedMetrics);
        Metrics = newMetrics;

        _logger.LogInformation("Sorted metrics by {Column} ({Direction})", 
            columnName, _sortAscending ? "Ascending" : "Descending");
    }

    private IEnumerable<TranscriptionMetrics> SortMetricsAscending(string columnName)
    {
        return columnName switch
        {
            "Timestamp" => Metrics.OrderBy(m => m.Timestamp),
            "ModelName" => Metrics.OrderBy(m => m.ModelName),
            "TranscriptionType" => Metrics.OrderBy(m => m.TranscriptionType),
            "FileSizeBytes" => Metrics.OrderBy(m => m.FileSizeBytes),
            "AudioDurationSeconds" => Metrics.OrderBy(m => m.AudioDurationSeconds ?? 0),
            "TotalTimeMs" => Metrics.OrderBy(m => m.TotalTimeMs),
            "PreprocessingTimeMs" => Metrics.OrderBy(m => m.PreprocessingTimeMs),
            "TranscriptionTimeMs" => Metrics.OrderBy(m => m.TranscriptionTimeMs),
            "Success" => Metrics.OrderBy(m => m.Success),
            _ => Metrics
        };
    }

    private IEnumerable<TranscriptionMetrics> SortMetricsDescending(string columnName)
    {
        return columnName switch
        {
            "Timestamp" => Metrics.OrderByDescending(m => m.Timestamp),
            "ModelName" => Metrics.OrderByDescending(m => m.ModelName),
            "TranscriptionType" => Metrics.OrderByDescending(m => m.TranscriptionType),
            "FileSizeBytes" => Metrics.OrderByDescending(m => m.FileSizeBytes),
            "AudioDurationSeconds" => Metrics.OrderByDescending(m => m.AudioDurationSeconds ?? 0),
            "TotalTimeMs" => Metrics.OrderByDescending(m => m.TotalTimeMs),
            "PreprocessingTimeMs" => Metrics.OrderByDescending(m => m.PreprocessingTimeMs),
            "TranscriptionTimeMs" => Metrics.OrderByDescending(m => m.TranscriptionTimeMs),
            "Success" => Metrics.OrderByDescending(m => m.Success),
            _ => Metrics
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
