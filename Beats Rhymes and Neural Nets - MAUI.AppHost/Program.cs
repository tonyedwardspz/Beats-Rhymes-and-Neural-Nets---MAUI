var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.LLMAPI>("llmapi");
builder.AddProject<Projects.WhisperAPI>("whisperAPI");

builder.Build().Run();
