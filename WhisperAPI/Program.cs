using System.Text.Json.Nodes;
using Whisper.net;
using Whisper.net.Ggml;
using Whisper.net.Logger;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.UseHttpsRedirection();

app.MapPost("/transcribe", async () =>
{
    var ggmlType = GgmlType.Base;
    var modelFileName = "./whisperModels/ggml-base.bin";
    
    using var whisperLogger = LogProvider.AddConsoleLogging(WhisperLogLevel.Debug);
    
    using var whisperFactory = WhisperFactory.FromPath(modelFileName);
    
    using var processor = whisperFactory.CreateBuilder()
        .WithLanguage("auto")
        .Build();
    
    using var fileStream = File.OpenRead("kennedy.wav");

    // This section processes the audio file and prints the results (start time, end time and text) to the console.
    JsonArray results = new JsonArray();
    await foreach (var result in processor.ProcessAsync(fileStream))
    {
        results.Add($"{result.Start}->{result.End}: {result.Text}");
    }

    return results;
});





app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}