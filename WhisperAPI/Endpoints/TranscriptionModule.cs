
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Whisper.net;
using Whisper.net.Ggml;
using Whisper.net.Logger;

namespace WhisperAPI.Endpoints;

public static class TranscriptionModule
{
    public static void MapWhisperEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/whisper")
            .WithTags("Whisper");

        group.MapGet("modelDetails", GetModelDetails);
    }

    private static async Task<string> GetModelDetails()
    {
        return "Whisper GGML base";
    }
}