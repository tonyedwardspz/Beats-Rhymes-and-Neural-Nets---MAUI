namespace API.Services;

public interface ILLMModelService
{
    /// <summary>
    /// Initializes the LLM model and executor
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Generates a response for the given prompt
    /// </summary>
    Task<string> GenerateResponseAsync(string prompt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a streaming response for the given prompt
    /// </summary>
    IAsyncEnumerable<string> GenerateStreamingResponseAsync(string prompt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the model is ready for inference
    /// </summary>
    bool IsReady { get; }

    /// <summary>
    /// Gets information about the loaded model
    /// </summary>
    string GetModelInfo();
}
