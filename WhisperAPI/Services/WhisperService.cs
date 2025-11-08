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
    private string? _resolvedModelPath;
    private readonly ILogger<WhisperService> _logger;
    private WhisperFactory? _whisperFactory;
    private readonly AudioFileHelper _audioFileHelper;
    private readonly IMetricsService _metricsService;
    private readonly IConfiguration _configuration;
    private readonly string _modelsDirectory;
    private readonly object _factoryLock = new object();

    public WhisperService(IConfiguration configuration, ILogger<WhisperService> logger, AudioFileHelper audioFileHelper, IMetricsService metricsService)
    {
        _configuration = configuration;
        _logger = logger; // Assign logger first so it's available in ResolveModelPath
        _audioFileHelper = audioFileHelper;
        _metricsService = metricsService;
        
        var configPath = configuration["Whisper:ModelPath"] ?? "./WhisperModels/ggml-base.bin";
        _modelFileName = configPath;
        _resolvedModelPath = ResolveModelPath(configPath);
        _modelsDirectory = Path.GetDirectoryName(_resolvedModelPath) ?? "./WhisperModels";
        
        // Ensure model directory exists
        EnsureModelDirectoryExists();
        
        // Try to initialize WhisperFactory, but don't throw if model is missing
        // This allows the service to start and initialize lazily when needed
        try
        {
            if (File.Exists(_resolvedModelPath))
            {
                _logger.LogInformation("Initializing WhisperFactory with model: {ModelPath}", _resolvedModelPath);
                _whisperFactory = WhisperFactory.FromPath(_resolvedModelPath);
                _logger.LogInformation("WhisperFactory initialized successfully");
            }
            else
            {
                _logger.LogWarning("Whisper model not found at startup: {ModelPath}. Factory will be initialized on first use.", _resolvedModelPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize WhisperFactory at startup. Factory will be initialized on first use.");
        }
    }

    /// <summary>
    /// Resolves the model path, handling relative paths from the bin directory to project root
    /// </summary>
    private string ResolveModelPath(string modelPath)
    {
        try
        {
            if (Path.IsPathRooted(modelPath))
            {
                return modelPath;
            }

            // Get the base directory (typically bin/Debug/netX.0)
            var baseDirectory = AppContext.BaseDirectory ?? Directory.GetCurrentDirectory();
            
            // Find project root (solution root) by going up until we find the solution file or Models directory
            var projectRoot = FindProjectRoot(baseDirectory);
            
            // Normalize the model path - if it starts with ../, remove it since we're already at project root
            var normalizedPath = modelPath;
            if (normalizedPath.StartsWith("../") || normalizedPath.StartsWith("..\\"))
            {
                // Remove the ../ prefix
                normalizedPath = normalizedPath.Substring(3);
            }
            else if (normalizedPath.StartsWith("./") || normalizedPath.StartsWith(".\\"))
            {
                // Remove the ./ prefix
                normalizedPath = normalizedPath.Substring(2);
            }
            
            // Resolve relative path from project root
            var resolvedPath = Path.GetFullPath(Path.Combine(projectRoot, normalizedPath));
            return resolvedPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving model path '{ModelPath}'", modelPath);
            return modelPath; // Return original path if resolution fails
        }
    }

    /// <summary>
    /// Finds the project root directory by traversing up from the current directory
    /// </summary>
    private string FindProjectRoot(string startDirectory)
    {
        var currentDir = new DirectoryInfo(startDirectory);
        var maxLevels = 10; // Safety limit
        var level = 0;
        
        // Go up the directory tree looking for the solution file first (most reliable)
        while (currentDir != null && level < maxLevels)
        {
            // Prioritize finding .sln file (solution root) - this is the most reliable indicator
            try
            {
                var solutionFiles = currentDir.GetFiles("*.sln");
                if (solutionFiles.Length > 0)
                {
                    _logger.LogInformation("Found solution file at: {Path}", currentDir.FullName);
                    return currentDir.FullName;
                }
            }
            catch
            {
                // Ignore errors when checking for files
            }
            
            currentDir = currentDir.Parent;
            level++;
        }
        
        // If no .sln found, try again looking for Models directory with llm/whisper subdirectories
        currentDir = new DirectoryInfo(startDirectory);
        level = 0;
        while (currentDir != null && level < maxLevels)
        {
            try
            {
                var modelsDir = Path.Combine(currentDir.FullName, "Models");
                if (Directory.Exists(modelsDir))
                {
                    // Verify it's the solution root by checking for subdirectories llm and whisper
                    var llmDir = Path.Combine(modelsDir, "llm");
                    var whisperDir = Path.Combine(modelsDir, "whisper");
                    if (Directory.Exists(llmDir) || Directory.Exists(whisperDir))
                    {
                        _logger.LogInformation("Found Models directory with llm/whisper at: {Path}", currentDir.FullName);
                        return currentDir.FullName;
                    }
                }
            }
            catch
            {
                // Ignore errors when checking directories
            }
            
            currentDir = currentDir.Parent;
            level++;
        }
        
        // Fallback: go up 4 levels from bin/Debug/netX.0 to reach solution root
        // bin/Debug/netX.0 -> bin/Debug -> bin -> WhisperAPI -> solution root
        var fallbackDir = new DirectoryInfo(startDirectory);
        for (int i = 0; i < 4 && fallbackDir != null; i++)
        {
            fallbackDir = fallbackDir.Parent;
        }
        
        _logger.LogInformation("Using fallback project root: {Path}", fallbackDir?.FullName ?? startDirectory);
        return fallbackDir?.FullName ?? startDirectory;
    }

    /// <summary>
    /// Ensures the model directory exists, creating it if necessary
    /// </summary>
    private void EnsureModelDirectoryExists()
    {
        try
        {
            if (!string.IsNullOrEmpty(_modelsDirectory) && !Directory.Exists(_modelsDirectory))
            {
                Directory.CreateDirectory(_modelsDirectory);
                _logger.LogInformation("Created model directory: {ModelsDirectory}", _modelsDirectory);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create model directory: {ModelsDirectory}", _modelsDirectory);
        }
    }

    /// <summary>
    /// Ensures the WhisperFactory is initialized before use
    /// </summary>
    private void EnsureFactoryInitialized()
    {
        if (_whisperFactory != null)
            return;

        lock (_factoryLock)
        {
            if (_whisperFactory != null)
                return;

            try
            {
                if (_resolvedModelPath == null)
                _resolvedModelPath = ResolveModelPath(_modelFileName);

                if (!File.Exists(_resolvedModelPath))
                {
                    var errorMessage = $"Whisper model not found at path: {_resolvedModelPath} (resolved from: {_modelFileName}). " +
                                       $"Please ensure the model file exists in the Models/whisper/ directory.";
                    _logger.LogError(errorMessage);
                    throw new FileNotFoundException(errorMessage);
                }

                _logger.LogInformation("Initializing WhisperFactory with model: {ModelPath}", _resolvedModelPath);
                _whisperFactory = WhisperFactory.FromPath(_resolvedModelPath);
                _logger.LogInformation("WhisperFactory initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize WhisperFactory");
                throw;
            }
        }
    }

    public async Task<JsonArray> TranscribeFilePathAsync(string filePath, string? transcriptionType = null, string? sessionId = null, int? chunkIndex = null)
    {
        // Ensure factory is initialized before use
        EnsureFactoryInitialized();

        var startTime = DateTime.UtcNow;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        var metrics = new TranscriptionMetrics
        {
            Timestamp = startTime,
            ModelName = Path.GetFileNameWithoutExtension(_resolvedModelPath ?? _modelFileName),
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
            using var processor = _whisperFactory!.CreateBuilder()
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
        try
        {
            if (_resolvedModelPath == null)
                _resolvedModelPath = ResolveModelPath(_modelFileName);

            var modelExists = File.Exists(_resolvedModelPath);
            var modelSize = 0L;
            var modelSizeFormatted = "Unknown";

            if (modelExists)
            {
                try
                {
                    var fileInfo = new FileInfo(_resolvedModelPath);
                    modelSize = fileInfo.Length;
                    modelSizeFormatted = FormatFileSize(modelSize);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error getting model file size");
                }
            }

            var modelInfo = new
            {
                ModelName = Path.GetFileNameWithoutExtension(_resolvedModelPath ?? _modelFileName),
                ModelPath = _resolvedModelPath ?? _modelFileName,
                OriginalModelPath = _modelFileName,
                ModelSize = modelSize,
                ModelSizeFormatted = modelSizeFormatted,
                ModelExists = modelExists,
                FactoryInitialized = _whisperFactory != null,
                QuantizationLevel = GetQuantizationLevel(_resolvedModelPath ?? _modelFileName),
                ModelType = GetModelType(_resolvedModelPath ?? _modelFileName)
            };
            
            return await Task.FromResult(System.Text.Json.JsonSerializer.Serialize(modelInfo, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting model details");
            return await Task.FromResult(System.Text.Json.JsonSerializer.Serialize(new
            {
                Error = "Failed to get model details",
                Message = ex.Message,
                ModelPath = _modelFileName,
                FactoryInitialized = _whisperFactory != null
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
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
            // Ensure directory exists
            EnsureModelDirectoryExists();

            if (!Directory.Exists(_modelsDirectory))
            {
                _logger.LogWarning("Models directory not found: {ModelsDirectory}", _modelsDirectory);
                return models;
            }

            var modelFiles = Directory.GetFiles(_modelsDirectory, "*.bin")
                .Where(f => Path.GetFileName(f).StartsWith("ggml-"))
                .OrderBy(f => f)
                .ToList();

            var currentModelFileName = Path.GetFileName(_resolvedModelPath ?? _modelFileName);

            foreach (var modelFile in modelFiles)
            {
                try
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
                        IsCurrent = Path.GetFileName(modelFile) == currentModelFileName
                    };
                    
                    models.Add(model);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error processing model file: {ModelFile}", modelFile);
                }
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
            // Ensure directory exists
            EnsureModelDirectoryExists();

            var modelPath = Path.Combine(_modelsDirectory, $"{modelName}.bin");
            var resolvedModelPath = ResolveModelPath(modelPath);
            
            if (!File.Exists(resolvedModelPath))
            {
                _logger.LogError("Model file not found: {ModelPath} (resolved from: {OriginalPath})", resolvedModelPath, modelPath);
                return Task.FromResult(false);
            }

            _logger.LogInformation("Switching to model: {ModelPath}", resolvedModelPath);
            
            lock (_factoryLock)
            {
                // Dispose the current factory
                _whisperFactory?.Dispose();
                
                try
                {
                    // Create new factory with the new model
                    _whisperFactory = WhisperFactory.FromPath(resolvedModelPath);
                    _modelFileName = modelPath;
                    _resolvedModelPath = resolvedModelPath;
                    
                    // Update configuration (this will persist the change)
                    _configuration["Whisper:ModelPath"] = modelPath;
                    
                    _logger.LogInformation("Successfully switched to model: {ModelPath}", resolvedModelPath);
                    return Task.FromResult(true);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create WhisperFactory for model: {ModelPath}", resolvedModelPath);
                    _whisperFactory = null;
                    return Task.FromResult(false);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to switch to model: {ModelName}", modelName);
            return Task.FromResult(false);
        }
    }
}
