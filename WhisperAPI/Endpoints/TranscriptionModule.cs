
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using WhisperAPI.Services;

namespace WhisperAPI.Endpoints;

public static class TranscriptionModule
{
    public static void MapWhisperEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/whisper")
            .WithTags("Whisper");

        group.MapGet("modelDetails", GetModelDetails);
        group.MapPost("transcribe", TranscribeFile);
    }

    private static async Task<string> GetModelDetails(IWhisperService whisperService)
    {
        return await whisperService.GetModelDetailsAsync();
    }

    private static async Task<IResult> TranscribeFile(IWhisperService whisperService, [FromBody] TranscribeRequest request)
    {
        try
        {
            var results = await whisperService.TranscribeFileAsync(request.FilePath);
            return Results.Ok(results);
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

public record TranscribeRequest(string FilePath);
public record TranscribeRequest(string FilePath);