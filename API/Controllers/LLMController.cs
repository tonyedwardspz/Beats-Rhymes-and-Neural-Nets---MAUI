using API.Services;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LLMController : ControllerBase
{
    private readonly ILLMModelService _llmService;
    private readonly ILogger<LLMController> _logger;

    public LLMController(ILLMModelService llmService, ILogger<LLMController> logger)
    {
        _llmService = llmService;
        _logger = logger;
    }

    [HttpGet("info")]
    public IActionResult GetModelInfo()
    {
        try
        {
            var info = _llmService.GetModelInfo();
            return Ok(new { modelInfo = info, isReady = _llmService.IsReady });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting model info");
            return StatusCode(500, new { error = "Failed to get model information" });
        }
    }

    [HttpPost("generate")]
    public async Task<IActionResult> GenerateResponse([FromBody] GenerateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return BadRequest(new { error = "Prompt is required" });
        }

        try
        {
            if (!_llmService.IsReady)
            {
                _logger.LogInformation("Initializing LLM model...");
                await _llmService.InitializeAsync();
            }

            var response = await _llmService.GenerateResponseAsync(request.Prompt, HttpContext.RequestAborted);
            return Ok(new { prompt = request.Prompt, response });
        }
        catch (OperationCanceledException)
        {
            return StatusCode(499, new { error = "Request was cancelled" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating response for prompt: {Prompt}", request.Prompt);
            return StatusCode(500, new { error = "Failed to generate response" });
        }
    }

    [HttpPost("stream")]
    public async Task<IActionResult> GenerateStreamingResponse([FromBody] GenerateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return BadRequest(new { error = "Prompt is required" });
        }

        try
        {
            if (!_llmService.IsReady)
            {
                _logger.LogInformation("Initializing LLM model...");
                await _llmService.InitializeAsync();
            }

            Response.Headers["Content-Type"] = "text/plain; charset=utf-8";
            Response.Headers["Cache-Control"] = "no-cache";
            
            await foreach (var token in _llmService.GenerateStreamingResponseAsync(request.Prompt, HttpContext.RequestAborted))
            {
                await Response.WriteAsync(token);
                await Response.Body.FlushAsync();
            }

            return new EmptyResult();
        }
        catch (OperationCanceledException)
        {
            return StatusCode(499, new { error = "Request was cancelled" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating streaming response for prompt: {Prompt}", request.Prompt);
            return StatusCode(500, new { error = "Failed to generate streaming response" });
        }
    }
}

public record GenerateRequest(string Prompt);
