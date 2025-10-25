namespace MAUI_App
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
        }

        private async void OnLLMChatClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//LLMPage");
        }

        private async void OnTranscriptionClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//WhisperPage");
        }

        private async void OnRapModeClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//RapModePage");
        }
    }
}
