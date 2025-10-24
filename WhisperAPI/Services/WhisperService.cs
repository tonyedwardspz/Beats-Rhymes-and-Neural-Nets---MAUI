using System.Diagnostics;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Whisper.net;
using Whisper.net.Ggml;
using Whisper.net.Logger;
using WhisperAPI.Endpoints;
using WhisperAPI.Interfaces;

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
    private readonly ITranscodeService _transcodeService;

    public WhisperService(IConfiguration configuration, ILogger<WhisperService> logger, ITranscodeService transcodeService)
    {
        _modelFileName = configuration["Whisper:ModelPath"] ?? "./WhisperModels/ggml-base.bin";
        _logger = logger;
        _transcodeService = transcodeService;
        
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

        // Log file information for debugging
        var fileInfo = new FileInfo(filePath);
        _logger.LogInformation("Processing file: {FilePath}, Size: {Size} bytes, LastWrite: {LastWrite}", 
            filePath, fileInfo.Length, fileInfo.LastWriteTime);

        // Validate file size
        if (fileInfo.Length == 0)
        {
            throw new InvalidOperationException($"Audio file is empty: {filePath}");
        }

        // Validate and potentially convert audio file
        string processedFilePath = filePath;
        try
        {
            using var headerStream = File.OpenRead(filePath);
            var header = new byte[12];
            var bytesRead = await headerStream.ReadAsync(header, 0, 12);
            
            if (bytesRead < 12)
            {
                throw new InvalidOperationException($"File too small to be a valid audio file: {filePath}");
            }

            // Check file format
            var firstHeader = System.Text.Encoding.ASCII.GetString(header, 0, 4);
            var secondHeader = System.Text.Encoding.ASCII.GetString(header, 8, 4);
            
            _logger.LogInformation("Audio file header - First: {FirstHeader}, Second: {SecondHeader}", firstHeader, secondHeader);
            
            // Check if it's a WAV file
            if (firstHeader == "RIFF" && secondHeader == "WAVE")
            {
                _logger.LogInformation("File is already in WAV format");
            }
            // Check if it's a CAF file (Core Audio Format)
            else if (firstHeader == "caff")
            {
                _logger.LogInformation("File is in CAF format, converting to WAV using FFMPEG");
                _logger.LogInformation("Original file path: {OriginalPath}, Size: {Size} bytes", filePath, new FileInfo(filePath).Length);
                
                try
                {
                    _logger.LogInformation("Attempting FFMPEG conversion for CAF file");
                    processedFilePath = await _transcodeService.ProcessFile(filePath);
                    _logger.LogInformation("FFMPEG conversion result: {ConvertedPath}", processedFilePath);
                    
                    if (string.IsNullOrEmpty(processedFilePath))
                    {
                        _logger.LogError("FFMPEG conversion returned empty path. This usually means:");
                        _logger.LogError("1. FFMPEG is not installed on the system");
                        _logger.LogError("2. FFMPEG path is not configured correctly");
                        _logger.LogError("3. The input file is corrupted or in an unsupported format");
                        _logger.LogError("4. FFMPEG conversion failed silently");
                        
                        // Try alternative approach - maybe the file is already in a compatible format
                        _logger.LogInformation("Attempting to use original file as-is (might work if it's already compatible)");
                        processedFilePath = filePath;
                        
                        // Validate that the original file might work
                        try
                        {
                            using var testStream = File.OpenRead(filePath);
                            var testHeader = new byte[12];
                            var testBytesRead = await testStream.ReadAsync(testHeader, 0, 12);
                            
                            if (testBytesRead >= 12)
                            {
                                var testFirstHeader = System.Text.Encoding.ASCII.GetString(testHeader, 0, 4);
                                var testSecondHeader = System.Text.Encoding.ASCII.GetString(testHeader, 8, 4);
                                
                                _logger.LogInformation("Original file headers: {FirstHeader}/{SecondHeader}", testFirstHeader, testSecondHeader);
                                
                                if (testFirstHeader == "RIFF" && testSecondHeader == "WAVE")
                                {
                                    _logger.LogInformation("Original file is already in WAV format, using as-is");
                                }
                                else
                                {
                                    throw new InvalidOperationException(
                                        "FFMPEG conversion failed and file is not in WAV format. " +
                                        "Please ensure FFMPEG is installed and accessible. " +
                                        "On macOS, you can install it with: brew install ffmpeg");
                                }
                            }
                        }
                        catch (Exception testEx)
                        {
                            _logger.LogError(testEx, "Failed to validate original file format");
                            throw new InvalidOperationException(
                                "FFMPEG conversion failed. Please ensure FFMPEG is installed and accessible. " +
                                "On macOS, you can install it with: brew install ffmpeg");
                        }
                    }
                    
                    if (!File.Exists(processedFilePath))
                    {
                        _logger.LogError("Converted file does not exist: {ConvertedPath}", processedFilePath);
                        throw new InvalidOperationException($"Converted file does not exist: {processedFilePath}");
                    }
                    
                    var convertedFileInfo = new FileInfo(processedFilePath);
                    _logger.LogInformation("Successfully processed file: {ConvertedPath}, Size: {Size} bytes", 
                        processedFilePath, convertedFileInfo.Length);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "FFMPEG conversion failed for file: {FilePath}", filePath);
                    
                    // Provide more helpful error message
                    if (ex.Message.Contains("FFMPEG conversion returned empty path"))
                    {
                        throw new InvalidOperationException(
                            "FFMPEG conversion failed. Please ensure FFMPEG is installed and accessible. " +
                            "On macOS, you can install it with: brew install ffmpeg", ex);
                    }
                    
                    throw new InvalidOperationException($"FFMPEG conversion failed: {ex.Message}", ex);
                }
            }
            else
            {
                _logger.LogInformation("Unknown audio format, attempting FFMPEG conversion");
                try
                {
                    processedFilePath = await _transcodeService.ProcessFile(filePath);
                    
                    if (string.IsNullOrEmpty(processedFilePath) || !File.Exists(processedFilePath))
                    {
                        _logger.LogError("FFMPEG conversion failed for unknown format: {FirstHeader}/{SecondHeader}", firstHeader, secondHeader);
                        throw new InvalidOperationException($"Unsupported audio format. Expected WAV or CAF, got {firstHeader}/{secondHeader}");
                    }
                    
                    _logger.LogInformation("Successfully converted audio file: {ConvertedPath}", processedFilePath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "FFMPEG conversion failed for unknown format: {FirstHeader}/{SecondHeader}", firstHeader, secondHeader);
                    throw new InvalidOperationException($"Unsupported audio format and conversion failed. Expected WAV or CAF, got {firstHeader}/{secondHeader}. Error: {ex.Message}", ex);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate/convert audio file: {FilePath}", filePath);
            throw;
        }

        using var whisperLogger = LogProvider.AddConsoleLogging(WhisperLogLevel.Debug);
        using var processor = _whisperFactory.CreateBuilder()
            .WithLanguage("auto")
            .Build();
        using var fileStream = File.OpenRead(processedFilePath);

        JsonArray results = new JsonArray();
        await foreach (var result in processor.ProcessAsync(fileStream))
        {
            results.Add($"{result.Start}->{result.End}: {result.Text}");
        }

        // Clean up converted file if it's different from the original
        if (processedFilePath != filePath && File.Exists(processedFilePath))
        {
            try
            {
                File.Delete(processedFilePath);
                _logger.LogInformation("Cleaned up converted file: {ConvertedPath}", processedFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up converted file: {ConvertedPath}", processedFilePath);
            }
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

            // Create a temporary WAV file path with a unique name
            var tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}_{request.File.FileName}");
            try
            {
                _logger.LogInformation("Saving uploaded file to temporary location: {TempFilePath}", tempFilePath);
                
                // Delete the temporary file if it exists
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }

                // Save the uploaded file to temp location
                using (var stream = new FileStream(tempFilePath, FileMode.Create))
                {
                    await request.File.CopyToAsync(stream);
                    await stream.FlushAsync(); // Ensure data is written to disk
                }

                // Verify the file was written correctly
                var tempFileInfo = new FileInfo(tempFilePath);
                _logger.LogInformation("Temporary file created: {TempFilePath}, Size: {Size} bytes", 
                    tempFilePath, tempFileInfo.Length);

                if (tempFileInfo.Length == 0)
                {
                    throw new InvalidOperationException("Temporary file is empty after upload");
                }

                if (tempFileInfo.Length != request.File.Length)
                {
                    _logger.LogWarning("File size mismatch: Original={OriginalSize}, Saved={SavedSize}", 
                        request.File.Length, tempFileInfo.Length);
                }

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
            _logger.LogError(ex, "Transcription failed");
            return new JsonArray();
        }
        
    }

    public async Task<string> GetModelDetailsAsync()
    {
        return await Task.FromResult("Whisper GGML base");
    }
}
