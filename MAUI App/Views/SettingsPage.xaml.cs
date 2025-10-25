using Microsoft.Maui.Storage;
using MAUI_App.Services;
using MAUI_App.Models;

namespace MAUI_App.Views;

public partial class SettingsPage : ContentPage
{
    private readonly ILLMApiService _llmApiService;

    public SettingsPage(ILLMApiService llmApiService)
    {
        _llmApiService = llmApiService;
        InitializeComponent();
        LoadSettings();
    }

    private async void LoadSettings()
    {
        // Load saved settings from preferences
        ApiUrlEntry.Text = Preferences.Get("ApiUrl", "http://localhost:5038");
        ThemePicker.SelectedItem = Preferences.Get("Theme", "System");
        TimeoutSlider.Value = Preferences.Get("TimeoutMinutes", 5.0);
        AutoSaveSwitch.IsToggled = Preferences.Get("AutoSave", false);
        
        // Load LLM model configuration settings from preferences first
        ContextSizeSlider.Value = Preferences.Get("ContextSize", 2048.0);
        GpuLayersSlider.Value = Preferences.Get("GpuLayers", 0.0);
        BatchSizeEntry.Text = Preferences.Get("BatchSize", "512");
        ThreadCountEntry.Text = Preferences.Get("ThreadCount", "0");
        
        // Try to load current configuration from API
        try
        {
            var result = await _llmApiService.GetConfigurationAsync();
            if (result.IsSuccess && result.Data != null)
            {
                ContextSizeSlider.Value = result.Data.ContextSize;
                GpuLayersSlider.Value = result.Data.GpuLayerCount;
                BatchSizeEntry.Text = result.Data.BatchSize.ToString();
                ThreadCountEntry.Text = result.Data.Threads?.ToString() ?? "0";
            }
        }
        catch (Exception ex)
        {
            // If API call fails, use the preference values (already loaded above)
            System.Diagnostics.Debug.WriteLine($"Failed to load configuration from API: {ex.Message}");
        }
    }

    private async void OnSaveSettingsClicked(object sender, EventArgs e)
    {
        try
        {
            // Save settings to preferences
            Preferences.Set("ApiUrl", ApiUrlEntry.Text);
            Preferences.Set("Theme", ThemePicker.SelectedItem?.ToString() ?? "System");
            Preferences.Set("TimeoutMinutes", TimeoutSlider.Value);
            Preferences.Set("AutoSave", AutoSaveSwitch.IsToggled);
            
            // Save LLM model configuration settings to preferences
            Preferences.Set("ContextSize", ContextSizeSlider.Value);
            Preferences.Set("GpuLayers", GpuLayersSlider.Value);
            Preferences.Set("BatchSize", BatchSizeEntry.Text);
            Preferences.Set("ThreadCount", ThreadCountEntry.Text);

            // Update LLM configuration via API
            try
            {
                var updateRequest = new LLMConfigurationUpdateRequest(
                    (int)ContextSizeSlider.Value,
                    (int)GpuLayersSlider.Value,
                    int.Parse(BatchSizeEntry.Text),
                    string.IsNullOrEmpty(ThreadCountEntry.Text) || ThreadCountEntry.Text == "0" 
                        ? null 
                        : int.Parse(ThreadCountEntry.Text)
                );

                var result = await _llmApiService.UpdateConfigurationAsync(updateRequest);
                if (!result.IsSuccess)
                {
                    await DisplayAlert("Warning", 
                        $"Settings saved locally, but failed to update LLM API: {result.ErrorMessage}", 
                        "OK");
                    return;
                }
            }
            catch (Exception apiEx)
            {
                await DisplayAlert("Warning", 
                    $"Settings saved locally, but failed to update LLM API: {apiEx.Message}", 
                    "OK");
                return;
            }

            await DisplayAlert("Settings", "Settings saved successfully!", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to save settings: {ex.Message}", "OK");
        }
    }

    private async void OnResetSettingsClicked(object sender, EventArgs e)
    {
        bool confirm = await DisplayAlert("Reset Settings", 
            "Are you sure you want to reset all settings to default values?", 
            "Yes", "No");

        if (confirm)
        {
            // Clear all preferences
            Preferences.Clear();
            
            // Reload default values
            LoadSettings();
            
            await DisplayAlert("Settings", "Settings reset to default values!", "OK");
        }
    }
}
