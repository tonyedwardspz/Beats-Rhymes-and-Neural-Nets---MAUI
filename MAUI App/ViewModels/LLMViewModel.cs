
using Microsoft.Maui.ApplicationModel;
using System.Text.Json;
using System.Collections.ObjectModel;
using MAUI_App.Models;

namespace MAUI_App.ViewModels;

/// <summary>
/// Example ViewModel demonstrating how to use the LLM LLMAPI service
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
    private ObservableCollection<ConversationMessage> _conversation = new();

    public LLMViewModel(ILLMApiService llmApiService, ILogger<LLMViewModel> logger)
    {
        _llmApiService = llmApiService;
        _logger = logger;
        
        // Initialize commands
        GenerateCommand = new Command(async () => await GenerateResponseAsync(), () => !IsGenerating && !string.IsNullOrWhiteSpace(Prompt));
        GenerateStreamingCommand = new Command(async () => await GenerateStreamingResponseAsync(), () => !IsGenerating && !string.IsNullOrWhiteSpace(Prompt));
        CheckModelInfoCommand = new Command(async () => await CheckModelInfoAsync());
        ClearConversationCommand = new Command(() => ClearConversation());
        
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
    public ICommand ClearConversationCommand { get; }

    public ObservableCollection<ConversationMessage> Conversation
    {
        get => _conversation;
        set => SetProperty(ref _conversation, value);
    }

    /// <summary>
    /// Generates a complete response (non-streaming)
    /// </summary>
    private async Task GenerateResponseAsync()
    {
        if (string.IsNullOrWhiteSpace(Prompt))
            return;

        // Add user prompt to conversation
        var userMessage = new ConversationMessage
        {
            Content = Prompt,
            IsUser = true
        };
        Conversation.Add(userMessage);

        // Clear the prompt
        var currentPrompt = Prompt;
        Prompt = string.Empty;

        IsGenerating = true;
        StatusMessage = "Generating response...";

        try
        {
            var result = await _llmApiService.GenerateResponseAsync(currentPrompt);
            
            if (result.IsSuccess && result.Data != null)
            {
                // Add AI response to conversation
                var aiMessage = new ConversationMessage
                {
                    Content = result.Data.Response,
                    IsUser = false
                };
                Conversation.Add(aiMessage);
                
                StatusMessage = "Response generated successfully";
            }
            else
            {
                // Add error message to conversation
                var errorMessage = new ConversationMessage
                {
                    Content = $"Error: {result.ErrorMessage}",
                    IsUser = false
                };
                Conversation.Add(errorMessage);
                
                StatusMessage = $"Error: {result.ErrorMessage}";
                _logger.LogError("Failed to generate response: {Error}", result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            // Add error message to conversation
            var errorMessage = new ConversationMessage
            {
                Content = $"Unexpected error: {ex.Message}",
                IsUser = false
            };
            Conversation.Add(errorMessage);
            
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

        // Add user prompt to conversation
        var userMessage = new ConversationMessage
        {
            Content = Prompt,
            IsUser = true
        };
        Conversation.Add(userMessage);

        // Clear the prompt
        var currentPrompt = Prompt;
        Prompt = string.Empty;

        IsGenerating = true;
        StatusMessage = "Generating streaming response...";

        // Create AI message for streaming
        var aiMessage = new ConversationMessage
        {
            Content = string.Empty,
            IsUser = false
        };
        Conversation.Add(aiMessage);

        try
        {
            var result = await _llmApiService.GenerateStreamingResponseAsync(
                currentPrompt,
                token =>
                {
                    // This callback is called for each token received
                    // Update UI on the main thread
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        aiMessage.Content += token;
                        // Trigger property change for the conversation
                        OnPropertyChanged(nameof(Conversation));
                    });
                });
            
            if (result.IsSuccess)
            {
                StatusMessage = "Streaming response completed successfully";
            }
            else
            {
                aiMessage.Content = $"Error: {result.ErrorMessage}";
                StatusMessage = $"Error: {result.ErrorMessage}";
                _logger.LogError("Failed to generate streaming response: {Error}", result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            aiMessage.Content = $"Unexpected error: {ex.Message}";
            StatusMessage = $"Unexpected error: {ex.Message}";
            _logger.LogError(ex, "Unexpected error during streaming response generation");
        }
        finally
        {
            IsGenerating = false;
        }
    }

    /// <summary>
    /// Clears the conversation history
    /// </summary>
    private void ClearConversation()
    {
        Conversation.Clear();
        StatusMessage = "Conversation cleared";
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
                ModelInfo = FormatLLMModelInfo(result.Data.ModelInfo);
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

    private string FormatLLMModelInfo(string jsonInfo)
    {
        try
        {
            var jsonDocument = JsonDocument.Parse(jsonInfo);
            var formattedInfo = new List<string>();
            
            // Check if this is the nested LLM response structure
            if (jsonDocument.RootElement.TryGetProperty("modelInfo", out var modelInfoElement))
            {
                // Parse the nested JSON string
                var nestedJson = modelInfoElement.GetString();
                if (!string.IsNullOrEmpty(nestedJson))
                {
                    var nestedDocument = JsonDocument.Parse(nestedJson);
                    foreach (var property in nestedDocument.RootElement.EnumerateObject())
                    {
                        var value = property.Value.ValueKind switch
                        {
                            JsonValueKind.String => property.Value.GetString(),
                            JsonValueKind.Number => FormatNumberValue(property.Value),
                            JsonValueKind.True => "True",
                            JsonValueKind.False => "False",
                            _ => property.Value.ToString()
                        };
                        
                        formattedInfo.Add($"{property.Name}: {value}");
                    }
                }
                
                // Add the isReady status
                if (jsonDocument.RootElement.TryGetProperty("isReady", out var isReadyElement))
                {
                    formattedInfo.Add($"IsReady: {isReadyElement.GetBoolean()}");
                }
            }
            else
            {
                // Fallback to regular formatting
                return FormatModelInfo(jsonInfo);
            }
            
            return string.Join("\n", formattedInfo);
        }
        catch
        {
            return jsonInfo;
        }
    }

    private string FormatModelInfo(string jsonInfo)
    {
        try
        {
            var jsonDocument = JsonDocument.Parse(jsonInfo);
            var formattedInfo = new List<string>();
            
            foreach (var property in jsonDocument.RootElement.EnumerateObject())
            {
                var value = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString(),
                    JsonValueKind.Number => FormatNumberValue(property.Value),
                    JsonValueKind.True => "True",
                    JsonValueKind.False => "False",
                    _ => property.Value.ToString()
                };
                
                formattedInfo.Add($"{property.Name}: {value}");
            }
            
            return string.Join("\n", formattedInfo);
        }
        catch
        {
            return jsonInfo;
        }
    }

    private string FormatNumberValue(JsonElement element)
    {
        if (element.TryGetInt64(out var intValue))
        {
            // Format large numbers (like file sizes) in human-readable format
            if (intValue > 1024 * 1024) // > 1MB
            {
                return $"{intValue:N0} ({FormatFileSize(intValue)})";
            }
            return intValue.ToString("N0");
        }
        
        if (element.TryGetDouble(out var doubleValue))
        {
            return doubleValue.ToString("N2");
        }
        
        return element.ToString();
    }

    private string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
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
