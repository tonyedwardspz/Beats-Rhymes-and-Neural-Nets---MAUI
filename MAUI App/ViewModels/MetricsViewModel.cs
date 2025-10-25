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
                foreach (var metric in result.Data)
                {
                    Metrics.Add(metric);
                }
                
                _logger.LogInformation("Loaded {Count} metrics", result.Data.Count);
                
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
            "FileName" => Metrics.OrderBy(m => m.FileName),
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
            "FileName" => Metrics.OrderByDescending(m => m.FileName),
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
