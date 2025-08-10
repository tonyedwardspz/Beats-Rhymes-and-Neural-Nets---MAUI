using LLMAPI.Services;
using LLMAPI.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<LLMConfiguration>(
builder.Configuration.GetSection(LLMConfiguration.SectionName));

builder.Services.AddSingleton<ILLMModelService, LLMModelService>();

// Add hosted service to initialize the model on startup
builder.Services.AddHostedService<LLMModelInitializationService>();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Map endpoint modules
app.MapLLMEndpoints();

app.Run();
