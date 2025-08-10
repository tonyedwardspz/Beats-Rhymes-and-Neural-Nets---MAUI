namespace LLMAPI.Services;

using LLama;
using LLama.Common;
using Microsoft.Extensions.Options;

public class LLMModelService : ILLMModelService, IDisposable
{
    private readonly LLMConfiguration _config;
    private LLamaWeights? _model;
    private StatelessExecutor? _executor;
    private ModelParams? _parameters;
    private bool _isInitialized = false;
    private readonly SemaphoreSlim _initializationSemaphore = new(1, 1);
    public bool IsReady => _isInitialized && _executor != null;

    public LLMModelService(IOptions<LLMConfiguration> config)
    {
        _config = config.Value;
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

            if (!File.Exists(_config.ModelPath))
            {
                throw new FileNotFoundException($"Model file not found at path: {_config.ModelPath}");
            }

            Console.WriteLine("Loading LLM model...");
            
            _parameters = new ModelParams(_config.ModelPath)
            {
                ContextSize = (uint)_config.ContextSize,
                GpuLayerCount = _config.GpuLayerCount,
                BatchSize = (uint)_config.BatchSize,
                Threads = _config.Threads ?? Environment.ProcessorCount / 2
            };

            _model = await LLamaWeights.LoadFromFileAsync(_parameters);
            _executor = new StatelessExecutor(_model, _parameters);
            
            _isInitialized = true;
            Console.WriteLine($"LLM model loaded successfully from path: {_config.ModelPath}");
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
        if (!_isInitialized || _model == null)
        {
            return "Model not loaded";
        }

        return $"Model: {Path.GetFileName(_config.ModelPath)}, Context Size: {_parameters?.ContextSize ?? 0}";
    }

    public void Dispose()
    {
        _model?.Dispose();
        _initializationSemaphore?.Dispose();
        GC.SuppressFinalize(this);
    }
}