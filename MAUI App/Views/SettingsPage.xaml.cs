using Microsoft.Maui.Storage;

namespace MAUI_App.Views;

public partial class SettingsPage : ContentPage
{
    public SettingsPage()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        // Load saved settings from preferences
        ApiUrlEntry.Text = Preferences.Get("ApiUrl", "http://localhost:5038");
        ThemePicker.SelectedItem = Preferences.Get("Theme", "System");
        TimeoutSlider.Value = Preferences.Get("TimeoutMinutes", 5.0);
        AutoSaveSwitch.IsToggled = Preferences.Get("AutoSave", false);
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
