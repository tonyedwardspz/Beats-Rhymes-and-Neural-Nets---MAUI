var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.LLMAPI>("llmapi");
builder.AddProject<Projects.WhisperAPI>("whisperLLM");

builder.Build().Run();
