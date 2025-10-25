var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.LLMAPI>("LLM-API");
builder.AddProject<Projects.WhisperAPI>("Whisper-API");

builder.Build().Run();
