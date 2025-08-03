namespace API.Services;

public class LLMModelInitializationService : IHostedService
{
    private readonly ILLMModelService _llmService;
    private readonly ILogger<LLMModelInitializationService> _logger;

    public LLMModelInitializationService(ILLMModelService llmService, ILogger<LLMModelInitializationService> logger)
    {
        _llmService = llmService;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting LLM model initialization...");
        
        try
        {
            await _llmService.InitializeAsync();
            _logger.LogInformation("LLM model initialized successfully on startup");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize LLM model on startup");
            // Don't throw here - the model can be initialized later when first requested
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("LLM model initialization service stopped");
        return Task.CompletedTask;
    }
}
