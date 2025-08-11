using System.Text.Json.Nodes;
using Whisper.net;
using Whisper.net.Ggml;
using Whisper.net.Logger;

namespace WhisperAPI.Services;

public interface IWhisperService
{
    Task<JsonArray> TranscribeFileAsync(string filePath);
    Task<string> GetModelDetailsAsync();
}

public class WhisperService : IWhisperService
{
    private readonly string _modelFileName;
    private readonly ILogger<WhisperService> _logger;
    private readonly WhisperFactory _whisperFactory;

    public WhisperService(IConfiguration configuration, ILogger<WhisperService> logger)
    {
        _modelFileName = configuration["Whisper:ModelPath"] ?? "./WhisperModels/ggml-base.bin";
        _logger = logger;
        
        // Initialize WhisperFactory at startup
        if (!File.Exists(_modelFileName))
        {
            throw new FileNotFoundException($"Whisper model not found: {_modelFileName}");
        }
        
        _logger.LogInformation("Initializing WhisperFactory with model: {ModelPath}", _modelFileName);
        _whisperFactory = WhisperFactory.FromPath(_modelFileName);
        _logger.LogInformation("WhisperFactory initialized successfully");
    }

    public async Task<JsonArray> TranscribeFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Audio file not found: {filePath}");
        }

        using var whisperLogger = LogProvider.AddConsoleLogging(WhisperLogLevel.Debug);
        using var processor = _whisperFactory.CreateBuilder()
            .WithLanguage("auto")
            .Build();
        using var fileStream = File.OpenRead(filePath);

        JsonArray results = new JsonArray();
        await foreach (var result in processor.ProcessAsync(fileStream))
        {
            results.Add($"{result.Start}->{result.End}: {result.Text}");
        }

        return results;
    }

    public async Task<string> GetModelDetailsAsync()
    {
        return await Task.FromResult("Whisper GGML base");
    }
}
