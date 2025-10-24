

namespace WhisperAPI.Endpoints;

public static class TranscriptionModule
{
    public static void MapWhisperEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/whisper")
            .WithTags("Whisper");

        group.MapGet("modelDetails", GetModelDetails);
        group.MapPost("transcribe", TranscribeFilePath);
        group.MapPost("transcribe-wav", TranscribeFile).DisableAntiforgery();
    }

    private static async Task<string> GetModelDetails(IWhisperService whisperService)
    {
        return await whisperService.GetModelDetailsAsync();
    }

    private static async Task<IResult> TranscribeFilePath(IWhisperService whisperService, [FromBody] TranscribeFilePathRequest request)
    {
        try
        {
            var results = await whisperService.TranscribeFilePathAsync(request.FilePath);
            
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

public record TranscribeFilePathRequest(string FilePath);
public record TranscribeWavRequest(IFormFile File);