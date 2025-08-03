using Microsoft.Extensions.Logging;
using MAUI_App.Models;
using MAUI_App.Services;
using MAUI_App.ViewModels;
using MAUI_App.Views;

namespace MAUI_App
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            // Configure API settings
            builder.Services.Configure<ApiConfiguration>(config =>
            {
                config.BaseUrl = "http://localhost:5038";
                config.Timeout = TimeSpan.FromMinutes(5);
            });

            // Register HTTP client and API service
            builder.Services.AddHttpClient<ILLMApiService, LLMApiService>();
            
            // Register ViewModels
            builder.Services.AddTransient<LLMViewModel>();
            
            // Register Pages
            builder.Services.AddTransient<LLMPage>();
            builder.Services.AddTransient<SettingsPage>();
            builder.Services.AddTransient<AboutPage>();

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
