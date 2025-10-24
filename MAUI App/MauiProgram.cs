
namespace MAUI_App
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .AddAudio(playbackOptions =>
                    {
#if IOS || MACCATALYST
                        playbackOptions.Category = AVFoundation.AVAudioSessionCategory.Playback;
#endif
#if ANDROID
					playbackOptions.AudioContentType = Android.Media.AudioContentType.Music;
					playbackOptions.AudioUsageKind = Android.Media.AudioUsageKind.Media;
#endif
                    },
                    recordingOptions =>
                    {
#if IOS || MACCATALYST
                        recordingOptions.Category = AVFoundation.AVAudioSessionCategory.Record;
                        recordingOptions.Mode = AVFoundation.AVAudioSessionMode.Default;
#endif
                    },
                    streamerOptions =>
                    {
#if IOS || MACCATALYST
                        streamerOptions.Category = AVFoundation.AVAudioSessionCategory.Record;
                        streamerOptions.Mode = AVFoundation.AVAudioSessionMode.Default;
#endif
                    })
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            // Configure API settings
            builder.Services.Configure<ApiConfiguration>(config =>
            {
                config.BaseUrl = "http://localhost:5038"; // LLMAPI
                config.Timeout = TimeSpan.FromMinutes(5);
                config.WhisperBaseURL = "http://localhost:5087";
            });

            // Register HTTP client and services
            builder.Services.AddHttpClient<ILLMApiService, LLMApiService>();
            builder.Services.AddHttpClient<IWhisperApiService, WhisperApiService>(client =>
            {
                client.BaseAddress = new Uri("http://localhost:5087"); // WhisperAPI
                client.Timeout = TimeSpan.FromMinutes(5);
            });
            
            // Register ViewModels
            builder.Services.AddTransient<LLMViewModel>();
            builder.Services.AddTransient<WhisperPageViewModel>();
            
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
