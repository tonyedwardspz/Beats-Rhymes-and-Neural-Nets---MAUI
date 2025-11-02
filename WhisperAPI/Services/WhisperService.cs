using SharedLibrary.Models;

namespace WhisperAPI.Services;

public interface IWhisperService
{
    Task<JsonArray> TranscribeFilePathAsync(string filePath, string? transcriptionType = null, string? sessionId = null, int? chunkIndex = null);
    Task<JsonArray> TranscribeFileAsync(IWhisperService whisperService, TranscribeWavRequest request);
    Task<string> GetModelDetailsAsync();
    Task<List<WhisperModel>> GetAvailableModelsAsync();
    Task<bool> SwitchModelAsync(string modelName);
}

public class WhisperService : IWhisperService
{
    private string _modelFileName;
    private readonly ILogger<WhisperService> _logger;
    private WhisperFactory _whisperFactory;
    private readonly AudioFileHelper _audioFileHelper;
    private readonly IMetricsService _metricsService;
    private readonly IConfiguration _configuration;
    private readonly string _modelsDirectory;

    public WhisperService(IConfiguration configuration, ILogger<WhisperService> logger, AudioFileHelper audioFileHelper, IMetricsService metricsService)
    {
        _configuration = configuration;
        _modelFileName = configuration["Whisper:ModelPath"] ?? "./WhisperModels/ggml-base.bin";
        _modelsDirectory = Path.GetDirectoryName(_modelFileName) ?? "./WhisperModels";
        _logger = logger;
        _audioFileHelper = audioFileHelper;
        _metricsService = metricsService;
        
        // Initialize WhisperFactory at startup
        if (!File.Exists(_modelFileName))
        {
            throw new FileNotFoundException($"Whisper model not found: {_modelFileName}");
        }
        
        _logger.LogInformation("Initializing WhisperFactory with model: {ModelPath}", _modelFileName);
        _whisperFactory = WhisperFactory.FromPath(_modelFileName);
        _logger.LogInformation("WhisperFactory initialized successfully");
    }

    public async Task<JsonArray> TranscribeFilePathAsync(string filePath, string? transcriptionType = null, string? sessionId = null, int? chunkIndex = null)
    {
        var startTime = DateTime.UtcNow;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        var metrics = new TranscriptionMetrics
        {
            Timestamp = startTime,
            ModelName = Path.GetFileNameWithoutExtension(_modelFileName),
            TranscriptionType = transcriptionType ?? "File Upload",
            SessionId = sessionId ?? Guid.NewGuid().ToString(),
            ChunkIndex = chunkIndex,
            FileSizeBytes = new FileInfo(filePath).Length
        };

        try
        {
            // Get audio duration before processing
            var audioDuration = await _audioFileHelper.GetAudioDurationAsync(filePath);
            metrics.AudioDurationSeconds = audioDuration;

            // Process the audio file (validate and convert if necessary)
            var preprocessingStart = stopwatch.ElapsedMilliseconds;
            var processedFilePath = await _audioFileHelper.ProcessAudioFileAsync(filePath);
            var preprocessingTime = stopwatch.ElapsedMilliseconds - preprocessingStart;
            metrics.PreprocessingTimeMs = preprocessingTime;

            // Perform transcription
            var transcriptionStart = stopwatch.ElapsedMilliseconds;
            using var whisperLogger = LogProvider.AddConsoleLogging(WhisperLogLevel.Debug);
            using var processor = _whisperFactory.CreateBuilder()
                .WithLanguage("auto")
                .Build();
            using var fileStream = File.OpenRead(processedFilePath);

            JsonArray results = new JsonArray();
            var transcribedText = new List<string>();
            await foreach (var result in processor.ProcessAsync(fileStream))
            {
                var resultText = $"{result.Start}->{result.End}: {result.Text}";
                results.Add(resultText);
                transcribedText.Add(result.Text);
            }
            var transcriptionTime = stopwatch.ElapsedMilliseconds - transcriptionStart;
            metrics.TranscriptionTimeMs = transcriptionTime;
            metrics.TranscribedText = string.Join(" ", transcribedText);

            // Clean up converted file if it's different from the original
            _audioFileHelper.CleanupConvertedFile(filePath, processedFilePath);

            // Record successful metrics
            stopwatch.Stop();
            metrics.TotalTimeMs = stopwatch.ElapsedMilliseconds;
            metrics.Success = true;
            await _metricsService.RecordMetricsAsync(metrics);

            return results;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            metrics.TotalTimeMs = stopwatch.ElapsedMilliseconds;
            metrics.Success = false;
            metrics.ErrorMessage = ex.Message;
            await _metricsService.RecordMetricsAsync(metrics);
            throw;
        }
    }


    public async Task<JsonArray> TranscribeFileAsync(IWhisperService whisperService, [FromForm] TranscribeWavRequest request)
    {
        try
        {
            if (request.File == null || request.File.Length == 0)
            {
                _logger.LogWarning("No file provided or file is empty");
                return new JsonArray();
            }

            _logger.LogInformation("Processing uploaded file: {FileName}, ContentType: {ContentType}, Size: {Size} bytes", 
                request.File.FileName, request.File.ContentType, request.File.Length);

            // Create a temporary file path with a unique name
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

                // Use the same transcription logic as TranscribeFilePathAsync which includes audio conversion
                var results = await whisperService.TranscribeFilePathAsync(tempFilePath, request.TranscriptionType, request.SessionId, request.ChunkIndex);
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
            _logger.LogError(ex, "Transcription failed for file: {FileName}", request.File?.FileName);
            return new JsonArray();
        }
    }

    public async Task<string> GetModelDetailsAsync()
    {
        var modelInfo = new
        {
            ModelName = Path.GetFileNameWithoutExtension(_modelFileName),
            ModelPath = _modelFileName,
            ModelSize = File.Exists(_modelFileName) ? new FileInfo(_modelFileName).Length : 0,
            ModelSizeFormatted = File.Exists(_modelFileName) ? FormatFileSize(new FileInfo(_modelFileName).Length) : "Unknown",
            ModelExists = File.Exists(_modelFileName),
            FactoryInitialized = _whisperFactory != null,
            QuantizationLevel = GetQuantizationLevel(_modelFileName),
            ModelType = GetModelType(_modelFileName)
        };
        
        return await Task.FromResult(System.Text.Json.JsonSerializer.Serialize(modelInfo, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }

    private string GetQuantizationLevel(string modelPath)
    {
        if (!File.Exists(modelPath))
            return "Unknown";

        var fileName = Path.GetFileName(modelPath).ToLower();
        
        // Check for quantization patterns in filename
        if (fileName.Contains("q8_0")) return "Q8_0 (8-bit)";
        if (fileName.Contains("q6_k")) return "Q6_K (6-bit)";
        if (fileName.Contains("q5_k")) return "Q5_K (5-bit)";
        if (fileName.Contains("q4_k")) return "Q4_K (4-bit)";
        if (fileName.Contains("q3_k")) return "Q3_K (3-bit)";
        if (fileName.Contains("q2_k")) return "Q2_K (2-bit)";
        if (fileName.Contains("q1_k")) return "Q1_K (1-bit)";
        if (fileName.Contains("f16")) return "F16 (16-bit float)";
        if (fileName.Contains("f32")) return "F32 (32-bit float)";
        
        // Default for base model
        return "Base (No quantization)";
    }

    private string GetModelType(string modelPath)
    {
        if (!File.Exists(modelPath))
            return "Unknown";

        var fileName = Path.GetFileName(modelPath).ToLower();
        
        if (fileName.Contains("tiny")) return "Tiny";
        if (fileName.Contains("base")) return "Base";
        if (fileName.Contains("small")) return "Small";
        if (fileName.Contains("medium")) return "Medium";
        if (fileName.Contains("large")) return "Large";
        
        return "Unknown";
    }

    private string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    public async Task<List<WhisperModel>> GetAvailableModelsAsync()
    {
        var models = new List<WhisperModel>();
        
        try
        {
            if (!Directory.Exists(_modelsDirectory))
            {
                _logger.LogWarning("Models directory not found: {ModelsDirectory}", _modelsDirectory);
                return models;
            }

            var modelFiles = Directory.GetFiles(_modelsDirectory, "*.bin")
                .Where(f => Path.GetFileName(f).StartsWith("ggml-"))
                .OrderBy(f => f)
                .ToList();

            foreach (var modelFile in modelFiles)
            {
                var fileInfo = new FileInfo(modelFile);
                var fileName = Path.GetFileName(modelFile);
                var modelName = Path.GetFileNameWithoutExtension(modelFile);
                
                var model = new WhisperModel
                {
                    Name = modelName,
                    FileName = fileName,
                    FilePath = modelFile,
                    SizeBytes = fileInfo.Length,
                    SizeFormatted = FormatFileSize(fileInfo.Length),
                    ModelType = GetModelType(modelFile),
                    QuantizationLevel = GetQuantizationLevel(modelFile),
                    IsCurrent = Path.GetFileName(modelFile) == Path.GetFileName(_modelFileName)
                };
                
                models.Add(model);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available models");
        }

        return await Task.FromResult(models);
    }

    public Task<bool> SwitchModelAsync(string modelName)
    {
        try
        {
            var modelPath = Path.Combine(_modelsDirectory, $"{modelName}.bin");
            
            if (!File.Exists(modelPath))
            {
                _logger.LogError("Model file not found: {ModelPath}", modelPath);
                return Task.FromResult(false);
            }

            _logger.LogInformation("Switching to model: {ModelPath}", modelPath);
            
            // Dispose the current factory
            _whisperFactory?.Dispose();
            
            // Create new factory with the new model
            _whisperFactory = WhisperFactory.FromPath(modelPath);
            _modelFileName = modelPath;
            
            // Update configuration (this will persist the change)
            _configuration["Whisper:ModelPath"] = modelPath;
            
            _logger.LogInformation("Successfully switched to model: {ModelPath}", modelPath);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to switch to model: {ModelName}", modelName);
            return Task.FromResult(false);
        }
    }
}
