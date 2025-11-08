using Microsoft.Extensions.Options;
using LLMAPI.Services;
using LLMAPI.Interfaces;
using SharedLibrary.Models;

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

        // Chat session endpoints
        var chatGroup = group.MapGroup("/chat")
            .WithTags("LLM Chat");

        // Create chat session endpoint
        chatGroup.MapPost("/create", CreateChatSession)
            .WithName("CreateChatSession")
            .WithSummary("Create a new interactive chat session")
            .Produces<CreateChatSessionResponse>()
            .Produces<ErrorResponse>(500);

        // Send message to chat session endpoint
        chatGroup.MapPost("/{sessionId}", SendChatMessage)
            .WithName("SendChatMessage")
            .WithSummary("Send a message to a chat session")
            .Accepts<ChatMessageRequest>("application/json")
            .Produces<ChatMessageResponse>()
            .Produces<ErrorResponse>(400)
            .Produces<ErrorResponse>(404)
            .Produces<ErrorResponse>(500);

        // Stream message to chat session endpoint
        chatGroup.MapPost("/{sessionId}/stream", StreamChatMessage)
            .WithName("StreamChatMessage")
            .WithSummary("Stream a response from a chat session")
            .Accepts<ChatMessageRequest>("application/json")
            .Produces<string>(200)
            .Produces<ErrorResponse>(400)
            .Produces<ErrorResponse>(404)
            .Produces<ErrorResponse>(500);

        // Get chat history endpoint
        chatGroup.MapGet("/{sessionId}/history", GetChatHistory)
            .WithName("GetChatHistory")
            .WithSummary("Get conversation history for a chat session")
            .Produces<ChatHistoryResponse>()
            .Produces<ErrorResponse>(404)
            .Produces<ErrorResponse>(500);

        // Delete chat session endpoint
        chatGroup.MapDelete("/{sessionId}", DeleteChatSession)
            .WithName("DeleteChatSession")
            .WithSummary("Delete a chat session")
            .Produces(204)
            .Produces<ErrorResponse>(404)
            .Produces<ErrorResponse>(500);

        // Clear chat session history endpoint
        chatGroup.MapPost("/{sessionId}/clear", ClearChatSession)
            .WithName("ClearChatSession")
            .WithSummary("Clear conversation history for a chat session")
            .Produces(204)
            .Produces<ErrorResponse>(404)
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

    // Chat session endpoint handlers
    private static async Task<IResult> CreateChatSession(
        ILLMChatSessionService chatService,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var sessionId = await chatService.CreateSessionAsync(cancellationToken);
            return Results.Ok(new CreateChatSessionResponse(sessionId));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating chat session");
            return Results.Problem(
                detail: "Failed to create chat session",
                statusCode: 500);
        }
    }

    private static async Task<IResult> SendChatMessage(
        string sessionId,
        ChatMessageRequest request,
        ILLMChatSessionService chatService,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return Results.BadRequest(new ErrorResponse("Message is required"));
        }

        if (!chatService.SessionExists(sessionId))
        {
            return Results.NotFound(new ErrorResponse($"Session {sessionId} not found"));
        }

        try
        {
            var response = await chatService.ChatAsync(sessionId, request.Message, cancellationToken);
            return Results.Ok(new ChatMessageResponse(sessionId, request.Message, response));
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound(new ErrorResponse($"Session {sessionId} not found"));
        }
        catch (OperationCanceledException)
        {
            return Results.Problem(
                detail: "Request was cancelled",
                statusCode: 499);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending chat message to session {SessionId}", sessionId);
            return Results.Problem(
                detail: "Failed to send chat message",
                statusCode: 500);
        }
    }

    private static async Task<IResult> StreamChatMessage(
        string sessionId,
        ChatMessageRequest request,
        ILLMChatSessionService chatService,
        ILogger<Program> logger,
        HttpContext context)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return Results.BadRequest(new ErrorResponse("Message is required"));
        }

        if (!chatService.SessionExists(sessionId))
        {
            return Results.NotFound(new ErrorResponse($"Session {sessionId} not found"));
        }

        try
        {
            context.Response.Headers["Content-Type"] = "text/plain; charset=utf-8";
            context.Response.Headers["Cache-Control"] = "no-cache";

            await foreach (var token in chatService.ChatStreamAsync(sessionId, request.Message, context.RequestAborted))
            {
                await context.Response.WriteAsync(token);
                await context.Response.Body.FlushAsync();
            }

            return Results.Empty;
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound(new ErrorResponse($"Session {sessionId} not found"));
        }
        catch (OperationCanceledException)
        {
            return Results.Problem(
                detail: "Request was cancelled",
                statusCode: 499);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error streaming chat message from session {SessionId}", sessionId);
            return Results.Problem(
                detail: "Failed to stream chat message",
                statusCode: 500);
        }
    }

    private static async Task<IResult> GetChatHistory(
        string sessionId,
        ILLMChatSessionService chatService,
        ILogger<Program> logger)
    {
        if (!chatService.SessionExists(sessionId))
        {
            return Results.NotFound(new ErrorResponse($"Session {sessionId} not found"));
        }

        try
        {
            var history = await chatService.GetHistoryAsync(sessionId);
            var historyItems = history.Select(h => new ChatHistoryItemDto(h.Role, h.Content)).ToList();
            return Results.Ok(new ChatHistoryResponse(historyItems));
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound(new ErrorResponse($"Session {sessionId} not found"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting chat history for session {SessionId}", sessionId);
            return Results.Problem(
                detail: "Failed to get chat history",
                statusCode: 500);
        }
    }

    private static async Task<IResult> DeleteChatSession(
        string sessionId,
        ILLMChatSessionService chatService,
        ILogger<Program> logger)
    {
        try
        {
            var deleted = await chatService.DeleteSessionAsync(sessionId);
            if (!deleted)
            {
                return Results.NotFound(new ErrorResponse($"Session {sessionId} not found"));
            }

            return Results.NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting chat session {SessionId}", sessionId);
            return Results.Problem(
                detail: "Failed to delete chat session",
                statusCode: 500);
        }
    }

    private static async Task<IResult> ClearChatSession(
        string sessionId,
        ILLMChatSessionService chatService,
        ILogger<Program> logger)
    {
        try
        {
            var cleared = await chatService.ClearSessionAsync(sessionId);
            if (!cleared)
            {
                return Results.NotFound(new ErrorResponse($"Session {sessionId} not found"));
            }

            return Results.NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error clearing chat session {SessionId}", sessionId);
            return Results.Problem(
                detail: "Failed to clear chat session",
                statusCode: 500);
        }
    }
}

// DTOs for the endpoints
public record GenerateRequest(string Prompt);
public record GenerateResponse(string Prompt, string Response);
public record ModelInfoResponse(string ModelInfo, bool IsReady);

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

// Chat session DTOs
public record CreateChatSessionResponse(string SessionId);
public record ChatMessageRequest(string Message);
public record ChatMessageResponse(string SessionId, string Message, string Response);
public record ChatHistoryResponse(List<ChatHistoryItemDto> Messages);
public record ChatHistoryItemDto(string Role, string Content);
