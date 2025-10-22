using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Whisper.net;
using Whisper.net.Ggml;
using Whisper.net.Logger;
using WhisperAPI.Endpoints;

namespace WhisperAPI.Services;

public interface IWhisperService
{
    Task<JsonArray> TranscribeFilePathAsync(string filePath);
    Task<JsonArray> TranscribeFileAsync(IWhisperService whisperService, TranscribeWavRequest request);
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

    public async Task<JsonArray> TranscribeFilePathAsync(string filePath)
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

    // public Task<JsonArray> TranscribeFileAsync(IFormFile file)
    // {
    //     throw new NotImplementedException();
    // }

    public async Task<JsonArray> TranscribeFileAsync(IWhisperService whisperService, [FromForm] TranscribeWavRequest request)
    {
        try
        {
            if (request.File == null || request.File.Length == 0)
            {
                // return Results.BadRequest("No file provided or file is empty");
                return new JsonArray();
            }

            // Check if it's a WAV file
            if (!request.File.ContentType.Equals("audio/wav", StringComparison.OrdinalIgnoreCase) &&
                !request.File.FileName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
            {
                // return Results.BadRequest("File must be a WAV audio file");
                return new JsonArray();
            }

            // Create a temporary file path
            var tempFilePath = Path.GetTempFileName();
            try
            {
                // Save the uploaded file to temp location
                using (var stream = new FileStream(tempFilePath, FileMode.Create))
                {
                    await request.File.CopyToAsync(stream);
                }

                // Create a new IFormFile from the temporary file
                var tempFile = new FormFile(
                    new FileStream(tempFilePath, FileMode.Open),
                    0,
                    request.File.Length,
                    request.File.Name,
                    request.File.FileName
                );

                // Transcribe the file
                var results = await whisperService.TranscribeFilePathAsync(tempFilePath);
                return results;
            }
            finally
            {
                // Clean up the temporary file
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
        }
        catch (Exception ex)
        {
            // return Results.Problem($"Transcription failed: {ex.Message}");
            return new JsonArray();
        }
        
    }

    public async Task<string> GetModelDetailsAsync()
    {
        return await Task.FromResult("Whisper GGML base");
    }
}
