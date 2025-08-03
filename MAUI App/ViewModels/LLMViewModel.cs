using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MAUI_App.Models;
using MAUI_App.Services;
using Microsoft.Extensions.Logging;

namespace MAUI_App.ViewModels;

/// <summary>
/// Example ViewModel demonstrating how to use the LLM API service
/// </summary>
public class LLMViewModel : INotifyPropertyChanged
{
    private readonly ILLMApiService _llmApiService;
    private readonly ILogger<LLMViewModel> _logger;
    private string _prompt = string.Empty;
    private string _response = string.Empty;
    private string _modelInfo = string.Empty;
    private bool _isGenerating = false;
    private bool _isModelReady = false;
    private string _statusMessage = string.Empty;

    public LLMViewModel(ILLMApiService llmApiService, ILogger<LLMViewModel> logger)
    {
        _llmApiService = llmApiService;
        _logger = logger;
        
        // Initialize commands
        GenerateCommand = new Command(async () => await GenerateResponseAsync(), () => !IsGenerating && !string.IsNullOrWhiteSpace(Prompt));
        GenerateStreamingCommand = new Command(async () => await GenerateStreamingResponseAsync(), () => !IsGenerating && !string.IsNullOrWhiteSpace(Prompt));
        CheckModelInfoCommand = new Command(async () => await CheckModelInfoAsync());
        
        // Load model info on startup
        _ = Task.Run(async () => await CheckModelInfoAsync());
    }

    public string Prompt
    {
        get => _prompt;
        set
        {
            if (SetProperty(ref _prompt, value))
            {
                ((Command)GenerateCommand).ChangeCanExecute();
                ((Command)GenerateStreamingCommand).ChangeCanExecute();
            }
        }
    }

    public string Response
    {
        get => _response;
        set => SetProperty(ref _response, value);
    }

    public string ModelInfo
    {
        get => _modelInfo;
        set => SetProperty(ref _modelInfo, value);
    }

    public bool IsGenerating
    {
        get => _isGenerating;
        set
        {
            if (SetProperty(ref _isGenerating, value))
            {
                ((Command)GenerateCommand).ChangeCanExecute();
                ((Command)GenerateStreamingCommand).ChangeCanExecute();
            }
        }
    }

    public bool IsModelReady
    {
        get => _isModelReady;
        set => SetProperty(ref _isModelReady, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ICommand GenerateCommand { get; }
    public ICommand GenerateStreamingCommand { get; }
    public ICommand CheckModelInfoCommand { get; }

    /// <summary>
    /// Generates a complete response (non-streaming)
    /// </summary>
    private async Task GenerateResponseAsync()
    {
        if (string.IsNullOrWhiteSpace(Prompt))
            return;

        IsGenerating = true;
        StatusMessage = "Generating response...";
        Response = string.Empty;

        try
        {
            var result = await _llmApiService.GenerateResponseAsync(Prompt);
            
            if (result.IsSuccess && result.Data != null)
            {
                Response = result.Data.Response;
                StatusMessage = "Response generated successfully";
            }
            else
            {
                StatusMessage = $"Error: {result.ErrorMessage}";
                _logger.LogError("Failed to generate response: {Error}", result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Unexpected error: {ex.Message}";
            _logger.LogError(ex, "Unexpected error during response generation");
        }
        finally
        {
            IsGenerating = false;
        }
    }

    /// <summary>
    /// Generates a streaming response
    /// </summary>
    private async Task GenerateStreamingResponseAsync()
    {
        if (string.IsNullOrWhiteSpace(Prompt))
            return;

        IsGenerating = true;
        StatusMessage = "Generating streaming response...";
        Response = string.Empty;

        try
        {
            var result = await _llmApiService.GenerateStreamingResponseAsync(
                Prompt,
                token =>
                {
                    // This callback is called for each token received
                    // Update UI on the main thread
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        Response += token;
                    });
                });
            
            if (result.IsSuccess)
            {
                StatusMessage = "Streaming response completed successfully";
            }
            else
            {
                StatusMessage = $"Error: {result.ErrorMessage}";
                _logger.LogError("Failed to generate streaming response: {Error}", result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Unexpected error: {ex.Message}";
            _logger.LogError(ex, "Unexpected error during streaming response generation");
        }
        finally
        {
            IsGenerating = false;
        }
    }

    /// <summary>
    /// Checks model information and status
    /// </summary>
    private async Task CheckModelInfoAsync()
    {
        StatusMessage = "Checking model status...";

        try
        {
            var result = await _llmApiService.GetModelInfoAsync();
            
            if (result.IsSuccess && result.Data != null)
            {
                ModelInfo = result.Data.ModelInfo;
                IsModelReady = result.Data.IsReady;
                StatusMessage = IsModelReady ? "Model is ready" : "Model is not ready";
            }
            else
            {
                ModelInfo = "Failed to get model info";
                IsModelReady = false;
                StatusMessage = $"Error: {result.ErrorMessage}";
                _logger.LogError("Failed to get model info: {Error}", result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            ModelInfo = "Error checking model";
            IsModelReady = false;
            StatusMessage = $"Unexpected error: {ex.Message}";
            _logger.LogError(ex, "Unexpected error while checking model info");
        }
    }

    #region INotifyPropertyChanged Implementation

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(backingStore, value))
            return false;

        backingStore = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    #endregion
}
