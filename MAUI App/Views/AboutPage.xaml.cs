using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using System.Text.Json;
using MAUI_App.Models;

namespace MAUI_App.Views;

public partial class AboutPage : ContentPage
{
    private readonly HttpClient _httpClient;

    public AboutPage()
    {
        InitializeComponent();
        _httpClient = new HttpClient();
        LoadSystemInfo();
        LoadModelInformation();
    }

    private void LoadSystemInfo()
    {
        try
        {
            DeviceModelLabel.Text = DeviceInfo.Model;
            PlatformLabel.Text = DeviceInfo.Platform.ToString();
            OSVersionLabel.Text = DeviceInfo.VersionString;
            AppVersionLabel.Text = AppInfo.VersionString;
        }
        catch (Exception)
        {
            // Fallback values if system info cannot be retrieved
            DeviceModelLabel.Text = "Unknown";
            PlatformLabel.Text = "Unknown";
            OSVersionLabel.Text = "Unknown";
            AppVersionLabel.Text = "1.0.0";
        }
    }

    private async void LoadModelInformation()
    {
        await Task.WhenAll(
            LoadLLMInformation(),
            LoadWhisperInformation()
        );
    }

    private async Task LoadLLMInformation()
    {
        try
        {
            // Try HTTP first, then HTTPS as fallback
            var urls = new[] { "http://localhost:5038/api/llm/info", "https://localhost:7284/api/llm/info" };
            HttpResponseMessage? response = null;
            
            foreach (var url in urls)
            {
                try
                {
                    response = await _httpClient.GetAsync(url);
                    if (response.IsSuccessStatusCode) break;
                }
                catch
                {
                    // Try next URL
                }
            }
            
            if (response?.IsSuccessStatusCode == true)
            {
                var content = await response.Content.ReadAsStringAsync();
                
                LLMLoadingIndicator.IsRunning = false;
                LLMInfoLabel.Text = FormatLLMModelInfo(content);
            }
            else
            {
                LLMLoadingIndicator.IsRunning = false;
                LLMInfoLabel.Text = "Failed to load LLM information - API not reachable";
            }
        }
        catch (Exception ex)
        {
            LLMLoadingIndicator.IsRunning = false;
            LLMInfoLabel.Text = $"Error loading LLM information: {ex.Message}";
        }
    }

    private async Task LoadWhisperInformation()
    {
        try
        {
            // Try HTTP first, then HTTPS as fallback
            var urls = new[] { "http://localhost:5087/api/whisper/modelDetails", "https://localhost:7003/api/whisper/modelDetails" };
            HttpResponseMessage? response = null;
            
            foreach (var url in urls)
            {
                try
                {
                    response = await _httpClient.GetAsync(url);
                    if (response.IsSuccessStatusCode) break;
                }
                catch
                {
                    // Try next URL
                }
            }
            
            if (response?.IsSuccessStatusCode == true)
            {
                var content = await response.Content.ReadAsStringAsync();
                
                WhisperLoadingIndicator.IsRunning = false;
                WhisperInfoLabel.Text = FormatModelInfo(content);
            }
            else
            {
                WhisperLoadingIndicator.IsRunning = false;
                WhisperInfoLabel.Text = "Failed to load Whisper information - API not reachable";
            }
        }
        catch (Exception ex)
        {
            WhisperLoadingIndicator.IsRunning = false;
            WhisperInfoLabel.Text = $"Error loading Whisper information: {ex.Message}";
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

    private async void OnViewSourceClicked(object sender, EventArgs e)
    {
        try
        {
            await Browser.OpenAsync("https://github.com/tonyedwardspz/Beats-Rhymes-and-Neural-Nets---MAUI", 
                BrowserLaunchMode.SystemPreferred);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Could not open browser: {ex.Message}", "OK");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _httpClient?.Dispose();
    }
}
