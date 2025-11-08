namespace LLMAPI.Services;

public class LLMConfiguration
{
    public const string SectionName = "LLM";
    public string ModelPath { get; set; } = "../Models/llm/gemma-3-4B-it-QAT-Q4_0.gguf";
    // https://huggingface.co/lmstudio-community/gemma-3-4B-it-qat-GGUF/blob/main/gemma-3-4B-it-QAT-Q4_0.gguf
    public int ContextSize { get; set; } = 2048;
    public int GpuLayerCount { get; set; } = 0;
    public int BatchSize { get; set; } = 512;
    public int? Threads { get; set; }
}
