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
/// AI service using Groq - FREE tier available!
/// Groq provides extremely fast inference for open-source models.
/// Get your free API key at: https://console.groq.com/keys
/// </summary>
public class GroqAiService : IAiService
{
    private readonly HttpClient _httpClient;
    private readonly GroqOptions _options;
    private readonly ILogger<GroqAiService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public GroqAiService(
        IHttpClientFactory httpClientFactory,
        IOptions<AiOptions> options,
        ILogger<GroqAiService> logger)
    {
        _options = options.Value.Groq;
        _logger = logger;

        if (string.IsNullOrEmpty(_options.ApiKey))
        {
            throw new InvalidOperationException(
                "Groq API key is not configured. Get a FREE key at https://console.groq.com/keys and set AI:Groq:ApiKey");
        }

        _httpClient = httpClientFactory.CreateClient("Groq");
        _httpClient.BaseAddress = new Uri("https://api.groq.com/openai/v1/");
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_options.ApiKey}");
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
        var request = new GroqRequest
        {
            Model = _options.Model,
            MaxTokens = _options.MaxTokens,
            Messages = messages.Select(m => new GroqMessage
            {
                Role = m.Role.ToString().ToLowerInvariant(),
                Content = m.Content
            }).ToList()
        };

        var response = await _httpClient.PostAsJsonAsync(
            "chat/completions",
            request,
            JsonOptions,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<GroqResponse>(JsonOptions, cancellationToken);
        return result?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
    }

    public async IAsyncEnumerable<string> StreamCompletionAsync(
        string prompt,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = new GroqRequest
        {
            Model = _options.Model,
            MaxTokens = _options.MaxTokens,
            Stream = true,
            Messages = [new GroqMessage { Role = "user", Content = prompt }]
        };

        var jsonContent = JsonSerializer.Serialize(request, JsonOptions);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
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
            if (data == "[DONE]") break;

            var chunk = JsonSerializer.Deserialize<GroqStreamResponse>(data, JsonOptions);
            var content = chunk?.Choices?.FirstOrDefault()?.Delta?.Content;
            if (!string.IsNullOrEmpty(content))
            {
                yield return content;
            }
        }
    }

    private record GroqRequest
    {
        public string Model { get; init; } = default!;
        public int MaxTokens { get; init; }
        public bool? Stream { get; init; }
        public List<GroqMessage> Messages { get; init; } = [];
    }

    private record GroqMessage
    {
        public string Role { get; init; } = default!;
        public string Content { get; init; } = default!;
    }

    private record GroqResponse
    {
        public List<GroqChoice>? Choices { get; init; }
    }

    private record GroqChoice
    {
        public GroqMessage? Message { get; init; }
    }

    private record GroqStreamResponse
    {
        public List<GroqStreamChoice>? Choices { get; init; }
    }

    private record GroqStreamChoice
    {
        public GroqDelta? Delta { get; init; }
    }

    private record GroqDelta
    {
        public string? Content { get; init; }
    }
}
