
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Hosting;

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
                    
                    // LibreFranklin font family
                    fonts.AddFont("LibreFranklin-Thin.ttf", "LibreFranklinThin");
                    fonts.AddFont("LibreFranklin-ThinItalic.ttf", "LibreFranklinThinItalic");
                    fonts.AddFont("LibreFranklin-ExtraLight.ttf", "LibreFranklinExtraLight");
                    fonts.AddFont("LibreFranklin-ExtraLightItalic.ttf", "LibreFranklinExtraLightItalic");
                    fonts.AddFont("LibreFranklin-Light.ttf", "LibreFranklinLight");
                    fonts.AddFont("LibreFranklin-LightItalic.ttf", "LibreFranklinLightItalic");
                    fonts.AddFont("LibreFranklin-Regular.ttf", "LibreFranklinRegular");
                    fonts.AddFont("LibreFranklin-Italic.ttf", "LibreFranklinItalic");
                    fonts.AddFont("LibreFranklin-Medium.ttf", "LibreFranklinMedium");
                    fonts.AddFont("LibreFranklin-MediumItalic.ttf", "LibreFranklinMediumItalic");
                    fonts.AddFont("LibreFranklin-SemiBold.ttf", "LibreFranklinSemiBold");
                    fonts.AddFont("LibreFranklin-SemiBoldItalic.ttf", "LibreFranklinSemiBoldItalic");
                    fonts.AddFont("LibreFranklin-Bold.ttf", "LibreFranklinBold");
                    fonts.AddFont("LibreFranklin-BoldItalic.ttf", "LibreFranklinBoldItalic");
                    fonts.AddFont("LibreFranklin-ExtraBold.ttf", "LibreFranklinExtraBold");
                    fonts.AddFont("LibreFranklin-ExtraBoldItalic.ttf", "LibreFranklinExtraBoldItalic");
                    fonts.AddFont("LibreFranklin-Black.ttf", "LibreFranklinBlack");
                    fonts.AddFont("LibreFranklin-BlackItalic.ttf", "LibreFranklinBlackItalic");
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
            builder.Services.AddHttpClient<IMetricsApiService, MetricsApiService>(client =>
            {
                client.BaseAddress = new Uri("http://localhost:5087"); // WhisperAPI
                client.Timeout = TimeSpan.FromMinutes(5);
            });
            
            // Register ViewModels
            builder.Services.AddTransient<LLMViewModel>();
            builder.Services.AddTransient<WhisperPageViewModel>();
            builder.Services.AddTransient<MetricsViewModel>();
            
            // Register Pages
            builder.Services.AddTransient<LLMPage>();
            builder.Services.AddTransient<SettingsPage>();
            builder.Services.AddTransient<AboutPage>();
            builder.Services.AddTransient<MetricsPage>();

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
