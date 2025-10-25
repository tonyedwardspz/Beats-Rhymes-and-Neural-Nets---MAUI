
using Microsoft.Extensions.Options;
using LLMAPI.Services;

namespace LLMAPI.Endpoints;

public static class LLMModule
{
    public static void MapLLMEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/llm")
            .WithTags("LLM");

        // Get model info endpoint
        group.MapGet("/info", GetModelInfo)
            .WithName("GetModelInfo")
            .WithSummary("Get information about the loaded LLM model")
            .Produces<ModelInfoResponse>()
            .Produces<ErrorResponse>(500);

        // Generate response endpoint
        group.MapPost("/generate", GenerateResponse)
            .WithName("GenerateResponse")
            .WithSummary("Generate a response for the given prompt")
            .Accepts<GenerateRequest>("application/json")
            .Produces<GenerateResponse>()
            .Produces<ErrorResponse>(400)
            .Produces<ErrorResponse>(499)
            .Produces<ErrorResponse>(500);

        // Generate streaming response endpoint
        group.MapPost("/stream", GenerateStreamingResponse)
            .WithName("GenerateStreamingResponse")
            .WithSummary("Generate a streaming response for the given prompt")
            .Accepts<GenerateRequest>("application/json")
            .Produces<string>(200)
            .Produces<ErrorResponse>(400)
            .Produces<ErrorResponse>(499)
            .Produces<ErrorResponse>(500);

        // Get configuration endpoint
        group.MapGet("/config", GetConfiguration)
            .WithName("GetConfiguration")
            .WithSummary("Get current LLM configuration")
            .Produces<LLMConfigurationResponse>()
            .Produces<ErrorResponse>(500);

        // Update configuration endpoint
        group.MapPut("/config", UpdateConfiguration)
            .WithName("UpdateConfiguration")
            .WithSummary("Update LLM configuration")
            .Accepts<LLMConfigurationUpdateRequest>("application/json")
            .Produces<LLMConfigurationResponse>()
            .Produces<ErrorResponse>(400)
            .Produces<ErrorResponse>(500);
    }

    private static IResult GetConfiguration(
        IOptions<LLMConfiguration> config,
        ILogger<Program> logger)
    {
        try
        {
            var configuration = config.Value;
            var response = new LLMConfigurationResponse(
                configuration.ModelPath,
                configuration.ContextSize,
                configuration.GpuLayerCount,
                configuration.BatchSize,
                configuration.Threads
            );
            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting LLM configuration");
            return Results.Problem(
                detail: "Failed to get LLM configuration",
                statusCode: 500);
        }
    }

    private static async Task<IResult> UpdateConfiguration(
        LLMConfigurationUpdateRequest request,
        IOptions<LLMConfiguration> config,
        ILLMModelService llmService,
        ILogger<Program> logger)
    {
        try
        {
            // Validate the request
            if (request.ContextSize < 512 || request.ContextSize > 8192)
            {
                return Results.BadRequest(new ErrorResponse("ContextSize must be between 512 and 8192"));
            }

            if (request.GpuLayerCount < 0 || request.GpuLayerCount > 50)
            {
                return Results.BadRequest(new ErrorResponse("GpuLayerCount must be between 0 and 50"));
            }

            if (request.BatchSize < 1 || request.BatchSize > 2048)
            {
                return Results.BadRequest(new ErrorResponse("BatchSize must be between 1 and 2048"));
            }

            if (request.Threads.HasValue && (request.Threads < 0 || request.Threads > 32))
            {
                return Results.BadRequest(new ErrorResponse("Threads must be between 0 and 32"));
            }

            // Update the configuration
            var configuration = config.Value;
            configuration.ContextSize = request.ContextSize;
            configuration.GpuLayerCount = request.GpuLayerCount;
            configuration.BatchSize = request.BatchSize;
            configuration.Threads = request.Threads;

            // If the model is already initialized, we need to reinitialize it with new parameters
            if (llmService.IsReady)
            {
                logger.LogInformation("Reinitializing LLM model with new configuration...");
                await llmService.InitializeAsync();
            }

            var response = new LLMConfigurationResponse(
                configuration.ModelPath,
                configuration.ContextSize,
                configuration.GpuLayerCount,
                configuration.BatchSize,
                configuration.Threads
            );

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating LLM configuration");
            return Results.Problem(
                detail: "Failed to update LLM configuration",
                statusCode: 500);
        }
    }

    private static IResult GetModelInfo(ILLMModelService llmService, ILogger<Program> logger)
    {
        try
        {
            var info = llmService.GetModelInfo();
            return Results.Ok(new ModelInfoResponse(info, llmService.IsReady));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting model info");
            return Results.Problem(
                detail: "Failed to get model information",
                statusCode: 500);
        }
    }

    private static async Task<IResult> GenerateResponse(
        GenerateRequest request,
        ILLMModelService llmService,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return Results.BadRequest(new ErrorResponse("Prompt is required"));
        }

        try
        {
            if (!llmService.IsReady)
            {
                logger.LogInformation("Initializing LLM model...");
                await llmService.InitializeAsync();
            }

            var response = await llmService.GenerateResponseAsync(request.Prompt, cancellationToken);
            return Results.Ok(new GenerateResponse(request.Prompt, response));
        }
        catch (OperationCanceledException)
        {
            return Results.Problem(
                detail: "Request was cancelled",
                statusCode: 499);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating response for prompt: {Prompt}", request.Prompt);
            return Results.Problem(
                detail: "Failed to generate response",
                statusCode: 500);
        }
    }

    private static async Task<IResult> GenerateStreamingResponse(
        GenerateRequest request,
        ILLMModelService llmService,
        ILogger<Program> logger,
        HttpContext context)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return Results.BadRequest(new ErrorResponse("Prompt is required"));
        }

        try
        {
            if (!llmService.IsReady)
            {
                logger.LogInformation("Initializing LLM model...");
                await llmService.InitializeAsync();
            }

            context.Response.Headers["Content-Type"] = "text/plain; charset=utf-8";
            context.Response.Headers["Cache-Control"] = "no-cache";

            await foreach (var token in llmService.GenerateStreamingResponseAsync(request.Prompt, context.RequestAborted))
            {
                await context.Response.WriteAsync(token);
                await context.Response.Body.FlushAsync();
            }

            return Results.Empty;
        }
        catch (OperationCanceledException)
        {
            return Results.Problem(
                detail: "Request was cancelled",
                statusCode: 499);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating streaming response for prompt: {Prompt}", request.Prompt);
            return Results.Problem(
                detail: "Failed to generate streaming response",
                statusCode: 500);
        }
    }
}

// DTOs for the endpoints
public record GenerateRequest(string Prompt);
public record GenerateResponse(string Prompt, string Response);
public record ModelInfoResponse(string ModelInfo, bool IsReady);
public record ErrorResponse(string Error);

// Configuration DTOs
public record LLMConfigurationResponse(
    string ModelPath,
    int ContextSize,
    int GpuLayerCount,
    int BatchSize,
    int? Threads);

public record LLMConfigurationUpdateRequest(
    int ContextSize,
    int GpuLayerCount,
    int BatchSize,
    int? Threads);
