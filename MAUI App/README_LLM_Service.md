# MAUI App LLM API Service

This document describes the LLM API service implementation for the MAUI application that interfaces with the backend LLM API.

## Overview

The service provides a clean interface for the MAUI app to communicate with the LLM API, supporting both standard and streaming responses.

## Components

### 1. Models (`Models/ApiModels.cs`)
- `GenerateRequest`: Request model for LLM generation
- `GenerateResponse`: Response model for LLM generation
- `ModelInfoResponse`: Model information and status
- `ErrorResponse`: Error response model
- `ApiConfiguration`: Configuration for API settings

### 2. Service Interface (`Services/ILLMApiService.cs`)
- `GetModelInfoAsync()`: Get model information and status
- `GenerateResponseAsync()`: Generate complete response
- `GenerateStreamingResponseAsync()`: Generate streaming response
- `ApiResult<T>`: Result wrapper with success/error handling

### 3. Service Implementation (`Services/LLMApiService.cs`)
- Full implementation of the API service
- HTTP client management
- Error handling and logging
- Streaming response support

### 4. Example ViewModel (`ViewModels/LLMViewModel.cs`)
- Demonstrates how to use the service
- MVVM pattern implementation
- Command handling for UI interactions

### 5. Example UI (`Views/LLMPage.xaml/.xaml.cs`)
- Simple UI to test the service
- Shows model status, input, and response
- Supports both standard and streaming generation

## Usage

### Basic Service Usage

```csharp
// Inject the service
public class MyClass
{
    private readonly ILLMApiService _llmService;
    
    public MyClass(ILLMApiService llmService)
    {
        _llmService = llmService;
    }
    
    // Get model info
    var modelInfo = await _llmService.GetModelInfoAsync();
    
    // Generate response
    var response = await _llmService.GenerateResponseAsync("Hello!");
    
    // Stream response
    await _llmService.GenerateStreamingResponseAsync("Hello!", 
        token => Console.Write(token));
}
```

### Configuration

The API base URL and timeout can be configured in `MauiProgram.cs`:

```csharp
builder.Services.Configure<ApiConfiguration>(config =>
{
    config.BaseUrl = "http://localhost:5000";  // Change to your API URL
    config.Timeout = TimeSpan.FromMinutes(5);
});
```

### Dependency Injection

All services are registered in `MauiProgram.cs`:

```csharp
// API Service
builder.Services.AddHttpClient<ILLMApiService, LLMApiService>();

// ViewModels
builder.Services.AddTransient<LLMViewModel>();

// Pages
builder.Services.AddTransient<LLMPage>();
```

## API Endpoints

The service interfaces with these API endpoints:

- `GET /api/llm/info` - Get model information
- `POST /api/llm/generate` - Generate complete response
- `POST /api/llm/stream` - Generate streaming response

## Error Handling

The service includes comprehensive error handling:

- Network errors (connection issues)
- HTTP errors (4xx, 5xx status codes)
- Timeout errors
- Cancellation support
- Logging for debugging

## Features

- **Async/Await**: Full async support for non-blocking operations
- **Cancellation**: Support for request cancellation
- **Streaming**: Real-time streaming response support
- **Error Handling**: Comprehensive error handling and logging
- **Configuration**: Configurable API settings
- **MVVM**: Example MVVM implementation
- **Dependency Injection**: Proper DI setup

## Testing

To test the service:

1. Start the API project
2. Run the MAUI app
3. Navigate to the LLM page
4. Check model status
5. Enter a prompt and test both generation modes

## Notes

- The service assumes the API is running on `http://localhost:5000` by default
- Streaming responses are delivered token by token to the UI
- All operations include proper error handling and logging
- The service is designed to be easily testable and mockable
