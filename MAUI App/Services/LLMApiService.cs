using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MAUI_App.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MAUI_App.Services;

/// <summary>
/// Service for communicating with the LLM LLMAPI
/// </summary>
public class LLMApiService : ILLMApiService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LLMApiService> _logger;
    private readonly ApiConfiguration _config;
    private readonly JsonSerializerOptions _jsonOptions;

    public LLMApiService(
        HttpClient httpClient, 
        ILogger<LLMApiService> logger,
        IOptions<ApiConfiguration> config)
    {
        _httpClient = httpClient;
        _logger = logger;
        _config = config.Value;
        
        // Configure HttpClient
        _httpClient.BaseAddress = new Uri(_config.BaseUrl);
        _httpClient.Timeout = _config.Timeout;
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    /// <inheritdoc />
    public async Task<ApiResult<ModelInfoResponse>> GetModelInfoAsync()
    {
        try
        {
            _logger.LogInformation("Requesting model information from LLMAPI");
            
            var response = await _httpClient.GetAsync("/api/llm/info");
            
            if (response.IsSuccessStatusCode)
            {
                var modelInfo = await response.Content.ReadFromJsonAsync<ModelInfoResponse>(_jsonOptions);
                
                if (modelInfo != null)
                {
                    _logger.LogInformation("Successfully retrieved model info: {ModelInfo}", modelInfo.ModelInfo);
                    return ApiResult<ModelInfoResponse>.Success(modelInfo);
                }
                
                return ApiResult<ModelInfoResponse>.Failure("Failed to deserialize model info response");
            }
            
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Failed to get model info. Status: {StatusCode}, Content: {Content}", 
                response.StatusCode, errorContent);
            
            return ApiResult<ModelInfoResponse>.Failure(
                $"LLMAPI request failed with status {response.StatusCode}", 
                (int)response.StatusCode);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error while requesting model info");
            return ApiResult<ModelInfoResponse>.Failure("Network error: Unable to connect to LLMAPI");
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timeout while getting model info");
            return ApiResult<ModelInfoResponse>.Failure("Request timeout");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while getting model info");
            return ApiResult<ModelInfoResponse>.Failure($"Unexpected error: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<ApiResult<GenerateResponse>> GenerateResponseAsync(
        string prompt, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return ApiResult<GenerateResponse>.Failure("Prompt cannot be empty", 400);
        }

        try
        {
            _logger.LogInformation("Generating response for prompt (length: {Length})", prompt.Length);
            
            var request = new GenerateRequest(prompt);
            var response = await _httpClient.PostAsJsonAsync("/api/llm/generate", request, _jsonOptions, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var generateResponse = await response.Content.ReadFromJsonAsync<GenerateResponse>(_jsonOptions, cancellationToken);
                
                if (generateResponse != null)
                {
                    _logger.LogInformation("Successfully generated response (length: {Length})", 
                        generateResponse.Response.Length);
                    return ApiResult<GenerateResponse>.Success(generateResponse);
                }
                
                return ApiResult<GenerateResponse>.Failure("Failed to deserialize generate response");
            }
            
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Failed to generate response. Status: {StatusCode}, Content: {Content}", 
                response.StatusCode, errorContent);
            
            // Try to parse error response
            var errorMessage = "Unknown error";
            try
            {
                var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(errorContent, _jsonOptions);
                if (errorResponse != null)
                {
                    errorMessage = errorResponse.Error;
                }
            }
            catch
            {
                // Use default error message if parsing fails
            }
            
            return ApiResult<GenerateResponse>.Failure(errorMessage, (int)response.StatusCode);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Generate response request was cancelled");
            return ApiResult<GenerateResponse>.Failure("Request was cancelled", 499);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error while generating response");
            return ApiResult<GenerateResponse>.Failure("Network error: Unable to connect to LLMAPI");
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timeout while generating response");
            return ApiResult<GenerateResponse>.Failure("Request timeout");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while generating response");
            return ApiResult<GenerateResponse>.Failure($"Unexpected error: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<ApiResult<string>> GenerateStreamingResponseAsync(
        string prompt, 
        Action<string> onTokenReceived, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return ApiResult<string>.Failure("Prompt cannot be empty", 400);
        }

        if (onTokenReceived == null)
        {
            throw new ArgumentNullException(nameof(onTokenReceived));
        }

        try
        {
            _logger.LogInformation("Starting streaming response for prompt (length: {Length})", prompt.Length);
            
            var request = new GenerateRequest(prompt);
            var jsonContent = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            
            using var response = await _httpClient.PostAsync("/api/llm/stream", content, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to start streaming response. Status: {StatusCode}, Content: {Content}", 
                    response.StatusCode, errorContent);
                
                return ApiResult<string>.Failure(
                    $"LLMAPI request failed with status {response.StatusCode}", 
                    (int)response.StatusCode);
            }
            
            var fullResponse = new StringBuilder();
            
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);
            
            var buffer = new char[1024];
            int bytesRead;
            
            while ((bytesRead = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
            {
                var token = new string(buffer, 0, bytesRead);
                fullResponse.Append(token);
                onTokenReceived(token);
            }
            
            var finalResponse = fullResponse.ToString();
            _logger.LogInformation("Completed streaming response (length: {Length})", finalResponse.Length);
            
            return ApiResult<string>.Success(finalResponse);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Streaming response request was cancelled");
            return ApiResult<string>.Failure("Request was cancelled", 499);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error while streaming response");
            return ApiResult<string>.Failure("Network error: Unable to connect to LLMAPI");
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timeout while streaming response");
            return ApiResult<string>.Failure("Request timeout");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while streaming response");
            return ApiResult<string>.Failure($"Unexpected error: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        GC.SuppressFinalize(this);
    }
}
