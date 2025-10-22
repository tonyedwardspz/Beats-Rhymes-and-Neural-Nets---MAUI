using Microsoft.AspNetCore.Antiforgery;
using WhisperAPI.Endpoints;
using WhisperAPI.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add anti-forgery services
builder.Services.AddAntiforgery();

// Add services to the container
builder.Services.AddSingleton<IWhisperService, WhisperService>();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Add anti-forgery middleware
app.UseAntiforgery();



// Map Whisper endpoints
app.MapWhisperEndpoints();

app.Run();





