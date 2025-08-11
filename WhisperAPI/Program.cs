using WhisperAPI.Endpoints;
using WhisperAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddSingleton<IWhisperService, WhisperService>();

var app = builder.Build();

app.UseHttpsRedirection();

// Map Whisper endpoints
app.MapWhisperEndpoints();

app.Run();