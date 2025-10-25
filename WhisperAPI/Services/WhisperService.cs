
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
    private readonly AudioFileHelper _audioFileHelper;
    private readonly IMetricsService _metricsService;

    public WhisperService(IConfiguration configuration, ILogger<WhisperService> logger, AudioFileHelper audioFileHelper, IMetricsService metricsService)
    {
        _modelFileName = configuration["Whisper:ModelPath"] ?? "./WhisperModels/ggml-base.bin";
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

    public async Task<JsonArray> TranscribeFilePathAsync(string filePath)
    {
        var startTime = DateTime.UtcNow;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var metrics = new TranscriptionMetrics
        {
            Timestamp = startTime,
            ModelName = Path.GetFileNameWithoutExtension(_modelFileName),
            FileName = Path.GetFileName(filePath),
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
            await foreach (var result in processor.ProcessAsync(fileStream))
            {
                results.Add($"{result.Start}->{result.End}: {result.Text}");
            }
            var transcriptionTime = stopwatch.ElapsedMilliseconds - transcriptionStart;
            metrics.TranscriptionTimeMs = transcriptionTime;

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
