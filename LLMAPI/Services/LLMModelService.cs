namespace LLMAPI.Services;


public class LLMModelService : ILLMModelService, IDisposable
{
    private readonly LLMConfiguration _config;
    private LLamaWeights? _model;
    private StatelessExecutor? _executor;
    private ModelParams? _parameters;
    private bool _isInitialized = false;
    private readonly SemaphoreSlim _initializationSemaphore = new(1, 1);
    private string? _resolvedModelPath;
    public bool IsReady => _isInitialized && _executor != null;

    public LLMModelService(IOptions<LLMConfiguration> config)
    {
        _config = config.Value;
        _resolvedModelPath = ResolveModelPath(_config.ModelPath);
        EnsureModelDirectoryExists();
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
            Console.WriteLine($"Resolving model path: '{modelPath}' from base: '{baseDirectory}', project root: '{projectRoot}'");
            
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
            Console.WriteLine($"Resolved model path to: '{resolvedPath}'");
            return resolvedPath;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error resolving model path '{modelPath}': {ex.Message}");
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
                    Console.WriteLine($"Found solution file at: {currentDir.FullName}");
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
                        Console.WriteLine($"Found Models directory with llm/whisper at: {currentDir.FullName}");
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
        // bin/Debug/netX.0 -> bin/Debug -> bin -> LLMAPI -> solution root
        var fallbackDir = new DirectoryInfo(startDirectory);
        for (int i = 0; i < 4 && fallbackDir != null; i++)
        {
            fallbackDir = fallbackDir.Parent;
        }
        
        Console.WriteLine($"Using fallback project root: {fallbackDir?.FullName ?? startDirectory}");
        return fallbackDir?.FullName ?? startDirectory;
    }

    /// <summary>
    /// Ensures the model directory exists, creating it if necessary
    /// </summary>
    private void EnsureModelDirectoryExists()
    {
        try
        {
            if (_resolvedModelPath != null)
            {
                var modelDirectory = Path.GetDirectoryName(_resolvedModelPath);
                if (!string.IsNullOrEmpty(modelDirectory) && !Directory.Exists(modelDirectory))
                {
                    Directory.CreateDirectory(modelDirectory);
                    Console.WriteLine($"Created model directory: {modelDirectory}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to create model directory: {ex.Message}");
        }
    }

    /// <summary>
    /// Initializes the LLM model and executor
    /// </summary>
    public async Task InitializeAsync()
    {
        await _initializationSemaphore.WaitAsync();
        try
        {
            if (_isInitialized)
                return;

            if (_resolvedModelPath == null)
            {
                _resolvedModelPath = ResolveModelPath(_config.ModelPath);
                EnsureModelDirectoryExists();
            }

            if (!File.Exists(_resolvedModelPath))
            {
                var errorMessage = $"Model file not found at path: {_resolvedModelPath} (resolved from: {_config.ModelPath}). " +
                                   $"Please ensure the model file exists in the Models/llm/ directory.";
                Console.WriteLine($"Error: {errorMessage}");
                _isInitialized = false;
                throw new FileNotFoundException(errorMessage);
            }

            Console.WriteLine($"Loading LLM model from: {_resolvedModelPath}");
            
            try
            {
                _parameters = new ModelParams(_resolvedModelPath)
                {
                    ContextSize = (uint)_config.ContextSize,
                    GpuLayerCount = _config.GpuLayerCount,
                    BatchSize = (uint)_config.BatchSize,
                    Threads = _config.Threads ?? Environment.ProcessorCount / 2
                };

                _model = await LLamaWeights.LoadFromFileAsync(_parameters);
                _executor = new StatelessExecutor(_model, _parameters);
                
                _isInitialized = true;
                Console.WriteLine($"LLM model loaded successfully from path: {_resolvedModelPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading model file: {ex.Message}");
                _isInitialized = false;
                throw;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing LLM model: {ex.Message}");
            _isInitialized = false;
            throw; // Re-throw to let the caller handle it
        }
        finally
        {
            _initializationSemaphore.Release();
        }
    }

    /// <summary>
    /// Generates a response for the given prompt
    /// </summary>
    public async Task<string> GenerateResponseAsync(string prompt, CancellationToken cancellationToken = default)
    {
        if (!_isInitialized)
        {
            await InitializeAsync();
        }

        if (_executor == null)
        {
            throw new InvalidOperationException("Model not initialized properly");
        }

        var response = new List<string>();
        
        await foreach (var result in _executor.InferAsync(prompt, cancellationToken: cancellationToken))
        {
            if (cancellationToken.IsCancellationRequested)
                break;
                
            response.Add(result);
        }

        return string.Join("", response);
    }

    /// <summary>
    /// Generates a streaming response for the given prompt
    /// </summary>
    public async IAsyncEnumerable<string> GenerateStreamingResponseAsync(
        string prompt, 
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!_isInitialized)
        {
            await InitializeAsync();
        }

        if (_executor == null)
        {
            throw new InvalidOperationException("Model not initialized properly");
        }

        await foreach (var result in _executor.InferAsync(prompt, cancellationToken: cancellationToken))
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;
                
            yield return result;
        }
    }

    /// <summary>
    /// Gets information about the loaded model
    /// </summary>
    public string GetModelInfo()
    {
        try
        {
            if (_resolvedModelPath == null)
            {
                _resolvedModelPath = ResolveModelPath(_config.ModelPath);
            }

            var modelExists = File.Exists(_resolvedModelPath);
            var modelSize = 0L;
            
            if (modelExists)
            {
                try
                {
                    var fileInfo = new FileInfo(_resolvedModelPath);
                    modelSize = fileInfo.Length;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error getting model file size: {ex.Message}");
                }
            }

            var modelInfo = new
            {
                ModelName = Path.GetFileName(_resolvedModelPath ?? _config.ModelPath),
                ModelPath = _resolvedModelPath ?? _config.ModelPath,
                OriginalModelPath = _config.ModelPath,
                ModelSize = modelSize,
                ContextSize = _parameters?.ContextSize ?? 0,
                GpuLayerCount = _config.GpuLayerCount,
                BatchSize = _config.BatchSize,
                Threads = _parameters?.Threads ?? _config.Threads ?? Environment.ProcessorCount / 2,
                ModelExists = modelExists,
                IsInitialized = _isInitialized
            };

            return System.Text.Json.JsonSerializer.Serialize(modelInfo, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting model info: {ex.Message}");
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                Error = "Failed to get model information",
                Message = ex.Message,
                ModelPath = _config.ModelPath,
                IsInitialized = _isInitialized
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
    }

    /// <summary>
    /// Gets the loaded model weights for creating contexts (for chat sessions)
    /// </summary>
    public LLamaWeights? GetModel()
    {
        return _model;
    }

    /// <summary>
    /// Gets the model parameters for creating contexts
    /// </summary>
    public ModelParams? GetModelParameters()
    {
        return _parameters;
    }

    public void Dispose()
    {
        _model?.Dispose();
        _initializationSemaphore?.Dispose();
        GC.SuppressFinalize(this);
    }
}