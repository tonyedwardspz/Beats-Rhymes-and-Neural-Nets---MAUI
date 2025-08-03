using API.Services;
using Microsoft.AspNetCore.Mvc;

namespace API.Endpoints;

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
