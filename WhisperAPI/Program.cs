using Microsoft.AspNetCore.Antiforgery;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add anti-forgery services
builder.Services.AddAntiforgery();

// Configure FFMPEG path
var ffmpegFound = false;
var ffmpegPath = "";

// Try to find FFMPEG in common locations
var pathEnv = Environment.GetEnvironmentVariable("PATH")?.Split(':') ?? new string[0];
var commonPaths = new[]
{
    "/usr/local/bin",
    "/usr/bin", 
    "/opt/homebrew/bin",
    "/opt/local/bin"
}.Concat(pathEnv)
.Where(p => !string.IsNullOrEmpty(p))
.Distinct()
.ToArray();

foreach (var path in commonPaths)
{
    var ffmpegExe = Path.Combine(path, "ffmpeg");
    if (File.Exists(ffmpegExe))
    {
        try
        {
            FFmpeg.SetExecutablesPath(path);
            ffmpegPath = path;
            ffmpegFound = true;
            Console.WriteLine($"FFMPEG found at: {path}");
            break;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FFMPEG found at {path} but failed to configure: {ex.Message}");
        }
    }
}

// If not found in common paths, try using system PATH
if (!ffmpegFound)
{
    try
    {
        // Let Xabe.FFmpeg try to find it in the system PATH
        FFmpeg.SetExecutablesPath("");
        ffmpegFound = true;
        Console.WriteLine("FFMPEG configured to use system PATH");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to configure FFMPEG from system PATH: {ex.Message}");
    }
}

if (!ffmpegFound)
{
    Console.WriteLine("WARNING: FFMPEG not found! Audio format conversion will not work.");
    Console.WriteLine("Please install FFMPEG: brew install ffmpeg");
    Console.WriteLine("Or set FFMPEG:Path in configuration");
}
else
{
    Console.WriteLine($"FFMPEG configured successfully at: {ffmpegPath}");
}

// Add services to the container
builder.Services.AddSingleton<ITranscodeService, FfMpegTranscodeService>();
builder.Services.AddSingleton<AudioFileHelper>();
builder.Services.AddSingleton<IWhisperService, WhisperService>();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Add anti-forgery middleware
app.UseAntiforgery();



// Map Whisper endpoints
app.MapWhisperEndpoints();

app.Run();





