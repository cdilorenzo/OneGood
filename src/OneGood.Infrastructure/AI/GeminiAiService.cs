using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneGood.Core.AI;

namespace OneGood.Infrastructure.AI;

/// <summary>
/// AI service using Google Gemini - FREE tier available!
/// Free tier: 15 requests/minute, 1 million tokens/month.
/// Get your free API key at: https://aistudio.google.com/apikey
/// </summary>
public class GeminiAiService : IAiService
{
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiAiService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public GeminiAiService(
        IHttpClientFactory httpClientFactory,
        IOptions<AiOptions> options,
        ILogger<GeminiAiService> logger)
    {
        _options = options.Value.Gemini;
        _logger = logger;

        if (string.IsNullOrEmpty(_options.ApiKey))
        {
            throw new InvalidOperationException(
                "Gemini API key is not configured. Get a FREE key at https://aistudio.google.com/apikey and set AI:Gemini:ApiKey");
        }

        _httpClient = httpClientFactory.CreateClient("Gemini");
        _httpClient.BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta/");
    }

    public async Task<string> GetCompletionAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var messages = new[] { new Core.AI.ChatMessage(Core.AI.ChatRole.User, prompt) };
        return await GetCompletionAsync(messages, cancellationToken);
    }

    public async Task<string> GetCompletionAsync(
        IEnumerable<Core.AI.ChatMessage> messages,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var messageList = messages.ToList();
            var isGemmaModel = _options.Model.StartsWith("gemma", StringComparison.OrdinalIgnoreCase);

            // Check for system message
            var systemMessage = messageList.FirstOrDefault(m => m.Role == Core.AI.ChatRole.System);

            // Gemma models don't support systemInstruction - prepend to first user message instead
            GeminiContent? systemInstruction = null;
            var contentMessages = messageList.Where(m => m.Role != Core.AI.ChatRole.System).ToList();

            if (systemMessage != null)
            {
                if (isGemmaModel)
                {
                    // For Gemma: prepend system message to first user message
                    var firstUserIdx = contentMessages.FindIndex(m => m.Role == Core.AI.ChatRole.User);
                    if (firstUserIdx >= 0)
                    {
                        var original = contentMessages[firstUserIdx];
                        contentMessages[firstUserIdx] = new Core.AI.ChatMessage(
                            original.Role,
                            $"{systemMessage.Content}\n\n{original.Content}");
                    }
                }
                else
                {
                    // For Gemini: use systemInstruction field
                    systemInstruction = new GeminiContent { Parts = [new GeminiPart { Text = systemMessage.Content }] };
                }
            }

            var request = new GeminiRequest
            {
                Contents = contentMessages
                    .Select(m => new GeminiContent
                    {
                        Role = m.Role == Core.AI.ChatRole.User ? "user" : "model",
                        Parts = [new GeminiPart { Text = m.Content }]
                    }).ToList(),
                SystemInstruction = systemInstruction,
                GenerationConfig = new GeminiGenerationConfig
                {
                    MaxOutputTokens = _options.MaxTokens
                }
            };

            var url = $"models/{_options.Model}:generateContent?key={_options.ApiKey}";
            var response = await _httpClient.PostAsJsonAsync(url, request, JsonOptions, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<GeminiResponse>(JsonOptions, cancellationToken);
            return result?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Gemini API");
            throw;
        }
    }

    public async IAsyncEnumerable<string> StreamCompletionAsync(
        string prompt,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = new GeminiRequest
        {
            Contents = [new GeminiContent
            {
                Role = "user",
                Parts = [new GeminiPart { Text = prompt }]
            }],
            GenerationConfig = new GeminiGenerationConfig
            {
                MaxOutputTokens = _options.MaxTokens
            }
        };

        var url = $"models/{_options.Model}:streamGenerateContent?key={_options.ApiKey}&alt=sse";
        var jsonContent = JsonSerializer.Serialize(request, JsonOptions);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
        };

        using var response = await _httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            if (string.IsNullOrEmpty(line) || !line.StartsWith("data: ")) continue;

            var data = line["data: ".Length..];
            var chunk = JsonSerializer.Deserialize<GeminiResponse>(data, JsonOptions);
            var text = chunk?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
            if (!string.IsNullOrEmpty(text))
            {
                yield return text;
            }
        }
    }

    private record GeminiRequest
    {
        public List<GeminiContent> Contents { get; init; } = [];
        public GeminiContent? SystemInstruction { get; init; }
        public GeminiGenerationConfig? GenerationConfig { get; init; }
    }

    private record GeminiContent
    {
        public string? Role { get; init; }
        public List<GeminiPart> Parts { get; init; } = [];
    }

    private record GeminiPart
    {
        public string? Text { get; init; }
    }

    private record GeminiGenerationConfig
    {
        public int MaxOutputTokens { get; init; }
    }

    private record GeminiResponse
    {
        public List<GeminiCandidate>? Candidates { get; init; }
    }

    private record GeminiCandidate
    {
        public GeminiContent? Content { get; init; }
    }
}
