
namespace WhisperAPI.Services;

/// <summary>
/// Helper class for audio file validation and conversion.
/// Handles file format detection, validation, and conversion to WAV format.
/// </summary>
public class AudioFileHelper
{
    private readonly ILogger<AudioFileHelper> _logger;
    private readonly ITranscodeService _transcodeService;

    public AudioFileHelper(ILogger<AudioFileHelper> logger, ITranscodeService transcodeService)
    {
        _logger = logger;
        _transcodeService = transcodeService;
    }

    /// <summary>
    /// Gets the duration of an audio file in seconds.
    /// </summary>
    /// <param name="filePath">Path to the audio file</param>
    /// <returns>Duration in seconds, or null if unable to determine</returns>
    public async Task<double?> GetAudioDurationAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            // Try to get duration using FFMPEG
            var duration = await _transcodeService.GetDurationAsync(filePath);
            return duration;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get audio duration for file: {FilePath}", filePath);
            return null;
        }
    }

    /// <summary>
    /// Validates and processes an audio file, converting it to WAV format if necessary.
    /// </summary>
    /// <param name="filePath">Path to the audio file to process</param>
    /// <returns>Path to the processed file (original or converted)</returns>
    /// <exception cref="FileNotFoundException">When the file doesn't exist</exception>
    /// <exception cref="InvalidOperationException">When the file is invalid or conversion fails</exception>
    public async Task<string> ProcessAudioFileAsync(string filePath)
    {
        // Validate file exists
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

        // Analyze file format and convert if necessary
        return await AnalyzeAndConvertFileAsync(filePath);
    }

    /// <summary>
    /// Analyzes the file format and converts to WAV if necessary.
    /// </summary>
    private async Task<string> AnalyzeAndConvertFileAsync(string filePath)
    {
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
            
            // Handle different file formats
            return firstHeader switch
            {
                "RIFF" when secondHeader == "WAVE" => await HandleWavFileAsync(filePath),
                "caff" => await HandleCafFileAsync(filePath),
                _ => await HandleUnknownFormatAsync(filePath, firstHeader, secondHeader)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate/convert audio file: {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// Handles WAV files (already in correct format).
    /// </summary>
    private async Task<string> HandleWavFileAsync(string filePath)
    {
        _logger.LogInformation("File is already in WAV format");
        return await Task.FromResult(filePath);
    }

    /// <summary>
    /// Handles CAF files by converting them to WAV using FFMPEG.
    /// </summary>
    private async Task<string> HandleCafFileAsync(string filePath)
    {
        _logger.LogInformation("File is in CAF format, converting to WAV using FFMPEG");
        _logger.LogInformation("Original file path: {OriginalPath}, Size: {Size} bytes", filePath, new FileInfo(filePath).Length);
        
        try
        {
            _logger.LogInformation("Attempting FFMPEG conversion for CAF file");
            var processedFilePath = await _transcodeService.ProcessFile(filePath);
            _logger.LogInformation("FFMPEG conversion result: {ConvertedPath}", processedFilePath);
            
            if (string.IsNullOrEmpty(processedFilePath))
            {
                return await HandleConversionFailureAsync(filePath);
            }
            
            if (!File.Exists(processedFilePath))
            {
                _logger.LogError("Converted file does not exist: {ConvertedPath}", processedFilePath);
                throw new InvalidOperationException($"Converted file does not exist: {processedFilePath}");
            }
            
            var convertedFileInfo = new FileInfo(processedFilePath);
            _logger.LogInformation("Successfully processed file: {ConvertedPath}, Size: {Size} bytes", 
                processedFilePath, convertedFileInfo.Length);
            
            return processedFilePath;
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

    /// <summary>
    /// Handles unknown file formats by attempting FFMPEG conversion.
    /// </summary>
    private async Task<string> HandleUnknownFormatAsync(string filePath, string firstHeader, string secondHeader)
    {
        _logger.LogInformation("Unknown audio format, attempting FFMPEG conversion");
        try
        {
            var processedFilePath = await _transcodeService.ProcessFile(filePath);
            
            if (string.IsNullOrEmpty(processedFilePath) || !File.Exists(processedFilePath))
            {
                _logger.LogError("FFMPEG conversion failed for unknown format: {FirstHeader}/{SecondHeader}", firstHeader, secondHeader);
                throw new InvalidOperationException($"Unsupported audio format. Expected WAV or CAF, got {firstHeader}/{secondHeader}");
            }
            
            _logger.LogInformation("Successfully converted audio file: {ConvertedPath}", processedFilePath);
            return processedFilePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FFMPEG conversion failed for unknown format: {FirstHeader}/{SecondHeader}", firstHeader, secondHeader);
            throw new InvalidOperationException($"Unsupported audio format and conversion failed. Expected WAV or CAF, got {firstHeader}/{secondHeader}. Error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Handles conversion failure by attempting to use the original file if it might be compatible.
    /// </summary>
    private async Task<string> HandleConversionFailureAsync(string filePath)
    {
        _logger.LogError("FFMPEG conversion returned empty path. This usually means:");
        _logger.LogError("1. FFMPEG is not installed on the system");
        _logger.LogError("2. FFMPEG path is not configured correctly");
        _logger.LogError("3. The input file is corrupted or in an unsupported format");
        _logger.LogError("4. FFMPEG conversion failed silently");
        
        // Try alternative approach - maybe the file is already in a compatible format
        _logger.LogInformation("Attempting to use original file as-is (might work if it's already compatible)");
        
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
                    return filePath;
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
        
        return filePath;
    }

    /// <summary>
    /// Cleans up a converted file if it's different from the original.
    /// </summary>
    /// <param name="originalPath">Path to the original file</param>
    /// <param name="processedPath">Path to the processed file</param>
    public void CleanupConvertedFile(string originalPath, string processedPath)
    {
        if (processedPath != originalPath && File.Exists(processedPath))
        {
            try
            {
                File.Delete(processedPath);
                _logger.LogInformation("Cleaned up converted file: {ConvertedPath}", processedPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up converted file: {ConvertedPath}", processedPath);
            }
        }
    }
}
