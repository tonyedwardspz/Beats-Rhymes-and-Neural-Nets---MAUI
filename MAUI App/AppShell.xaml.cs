
namespace MAUI_App
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            
            // Register routes for navigation
            Routing.RegisterRoute(nameof(LLMPage), typeof(LLMPage));
            Routing.RegisterRoute(nameof(WhisperPage), typeof(WhisperPage));
            Routing.RegisterRoute(nameof(MetricsPage), typeof(MetricsPage));
            Routing.RegisterRoute(nameof(RapModePage), typeof(RapModePage));
            Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
            Routing.RegisterRoute(nameof(AboutPage), typeof(AboutPage));
        }
    }
}
