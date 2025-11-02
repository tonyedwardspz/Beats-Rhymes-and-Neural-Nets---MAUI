
using SharedLibrary.Models;

namespace WhisperAPI.Endpoints;

public static class TranscriptionModule
{
    public static void MapWhisperEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/whisper")
            .WithTags("Whisper");

        group.MapGet("modelDetails", GetModelDetails);
        group.MapGet("models", GetAvailableModels);
        group.MapPost("models/switch", SwitchModel);
        group.MapGet("metrics", GetMetrics);
        group.MapDelete("metrics", ClearMetrics);
        group.MapPost("transcribe", TranscribeFilePath);
        group.MapPost("transcribe-wav", TranscribeFile).DisableAntiforgery();
    }

    private static async Task<string> GetModelDetails(IWhisperService whisperService)
    {
        return await whisperService.GetModelDetailsAsync();
    }

    private static async Task<IResult> GetAvailableModels(IWhisperService whisperService)
    {
        try
        {
            var models = await whisperService.GetAvailableModelsAsync();
            return Results.Ok(models);
        }
        catch (Exception ex)
        {
            return Results.Problem($"Failed to get available models: {ex.Message}");
        }
    }

    private static async Task<IResult> SwitchModel(IWhisperService whisperService, [FromBody] SwitchModelRequest request)
    {
        try
        {
            var success = await whisperService.SwitchModelAsync(request.ModelName);
            if (success)
            {
                return Results.Ok(new { message = $"Successfully switched to model: {request.ModelName}" });
            }
            else
            {
                return Results.BadRequest(new { message = $"Failed to switch to model: {request.ModelName}" });
            }
        }
        catch (Exception ex)
        {
            return Results.Problem($"Failed to switch model: {ex.Message}");
        }
    }

    private static async Task<IResult> GetMetrics(IMetricsService metricsService)
    {
        try
        {
            var metrics = await metricsService.GetAllMetricsAsync();
            return Results.Ok(new { transcriptionMetrics = metrics });
        }
        catch (Exception ex)
        {
            return Results.Problem($"Failed to get metrics: {ex.Message}");
        }
    }

    private static async Task<IResult> ClearMetrics(IMetricsService metricsService)
    {
        try
        {
            await metricsService.ClearMetricsAsync();
            return Results.Ok(new { message = "All metrics cleared successfully" });
        }
        catch (Exception ex)
        {
            return Results.Problem($"Failed to clear metrics: {ex.Message}");
        }
    }

    private static async Task<IResult> TranscribeFilePath(IWhisperService whisperService, [FromBody] TranscribeFilePathRequest request)
    {
        try
        {
            var results = await whisperService.TranscribeFilePathAsync(request.FilePath, request.TranscriptionType, request.SessionId);
            
            // Convert JsonArray to string array
            var stringResults = results.Select(node => node?.ToString() ?? string.Empty).ToArray();
            
            var response = new TranscriptionResponse(stringResults);
            return Results.Ok(response);
        }
        catch (FileNotFoundException ex)
        {
            return Results.NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            return Results.Problem($"Transcription failed: {ex.Message}");
        }
    }

    private static async Task<IResult> TranscribeFile(IWhisperService whisperService, [FromForm] TranscribeWavRequest request)
    {
        try
        {
            var results = await whisperService.TranscribeFileAsync(whisperService, request);
            
            // Convert JsonArray to string array
            var stringResults = results.Select(node => node?.ToString() ?? string.Empty).ToArray();
            
            var response = new TranscriptionResponse(stringResults);
            return Results.Ok(response);
        }
        catch (FileNotFoundException ex)
        {
            return Results.NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            return Results.Problem($"Transcription failed: {ex.Message}");
        }
    }
}

public record TranscribeFilePathRequest(string FilePath, string? TranscriptionType = null, string? SessionId = null);

public class TranscribeWavRequest
{
    public IFormFile File { get; set; } = null!;
    public string? TranscriptionType { get; set; }
    public string? SessionId { get; set; }
}

public record SwitchModelRequest(string ModelName);