namespace OneGood.Core.AI;

/// <summary>
/// Configuration options for AI services.
/// </summary>
public class AiOptions
{
    public const string SectionName = "AI";

    /// <summary>
    /// The AI provider to use.
    /// </summary>
    public AiProvider Provider { get; set; } = AiProvider.Groq;

    /// <summary>
    /// Groq configuration (FREE tier available! Very fast inference).
    /// </summary>
    public GroqOptions Groq { get; set; } = new();

    /// <summary>
    /// Google Gemini configuration (FREE tier: 15 req/min).
    /// </summary>
    public GeminiOptions Gemini { get; set; } = new();

    /// <summary>
    /// Anthropic-specific configuration (paid, but high quality).
    /// </summary>
    public AnthropicOptions Anthropic { get; set; } = new();

    /// <summary>
    /// Ollama configuration for local/self-hosted models (FREE!).
    /// </summary>
    public OllamaOptions Ollama { get; set; } = new();
}

public enum AiProvider
{
    /// <summary>FREE tier, extremely fast, Llama 3 &amp; Mixtral models.</summary>
    Groq,
    /// <summary>FREE tier (15 req/min), Google's Gemini models.</summary>
    Gemini,
    /// <summary>Paid, high quality Claude models.</summary>
    Anthropic,
    /// <summary>FREE, self-hosted local models.</summary>
    Ollama
}

public class GroqOptions
{
    /// <summary>Get free API key at https://console.groq.com/keys</summary>
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "llama-3.3-70b-versatile";
    public int MaxTokens { get; set; } = 4096;
}

public class GeminiOptions
{
    /// <summary>Get free API key at https://aistudio.google.com/apikey</summary>
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-2.0-flash";
    public int MaxTokens { get; set; } = 4096;
}

public class AnthropicOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "claude-sonnet-4-20250514";
    public int MaxTokens { get; set; } = 4096;
}

public class OllamaOptions
{
    public string Endpoint { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "llama3.2";
}
