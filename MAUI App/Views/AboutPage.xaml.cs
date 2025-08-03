namespace MAUI_App.Views;

public partial class AboutPage : ContentPage
{
    public AboutPage()
    {
        InitializeComponent();
        LoadSystemInfo();
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

    private async void OnViewSourceClicked(object sender, EventArgs e)
    {
        try
        {
            // Open the GitHub repository (replace with your actual repo URL)
            await Browser.OpenAsync("https://github.com/tonyedwardspz/Beats-Rhymes-and-Neural-Nets---MAUI", 
                BrowserLaunchMode.SystemPreferred);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Could not open browser: {ex.Message}", "OK");
        }
    }
}
