namespace MAUI_App
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
        }

        private async void OnStartChattingClicked(object? sender, EventArgs e)
        {
            try
            {
                // Navigate to the LLM Chat page
                await Shell.Current.GoToAsync("//LLMPage");
                StatusLabel.Text = "Navigating to LLM Chat...";
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Navigation error: {ex.Message}";
            }
        }
    }
}
