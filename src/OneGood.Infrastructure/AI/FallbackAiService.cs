using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneGood.Core.AI;

namespace OneGood.Infrastructure.AI;

/// <summary>
/// AI service with immediate fallback to alternative providers.
/// No retries with delays - just immediately tries the next provider if one fails.
/// Order: Primary (configured) → Gemini → Groq → Anthropic
/// </summary>
public class FallbackAiService : IAiService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AiOptions _options;
    private readonly ILogger<FallbackAiService> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public FallbackAiService(
        IHttpClientFactory httpClientFactory,
        IOptions<AiOptions> options,
        ILogger<FallbackAiService> logger,
        ILoggerFactory loggerFactory)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    public async Task<string> GetCompletionAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var messages = new[] { new ChatMessage(ChatRole.User, prompt) };
        return await GetCompletionAsync(messages, cancellationToken);
    }

    public async Task<string> GetCompletionAsync(
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken = default)
    {
        var providers = GetProviderOrder();
        Exception? lastException = null;

        foreach (var provider in providers)
        {
            try
            {
                var service = CreateService(provider);
                if (service is null) continue;

                _logger.LogDebug("Trying AI provider: {Provider}", provider);
                var result = await service.GetCompletionAsync(messages, cancellationToken);

                if (!string.IsNullOrEmpty(result))
                {
                    _logger.LogDebug("AI provider {Provider} succeeded", provider);
                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI provider {Provider} failed, trying next...", provider);
                lastException = ex;
                // Immediately try next provider - no delay!
            }
        }

        // All providers failed
        _logger.LogError(lastException, "All AI providers failed");
        throw new InvalidOperationException(
            "All AI providers failed. Please check API keys and rate limits.", 
            lastException);
    }

    public async IAsyncEnumerable<string> StreamCompletionAsync(
        string prompt,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var providers = GetProviderOrder();

        foreach (var provider in providers)
        {
            var service = CreateService(provider);
            if (service is null) continue;

            bool succeeded = false;

            await using var enumerator = service.StreamCompletionAsync(prompt, cancellationToken).GetAsyncEnumerator(cancellationToken);

            while (true)
            {
                string? current;
                try
                {
                    if (!await enumerator.MoveNextAsync())
                        break;
                    current = enumerator.Current;
                    succeeded = true;
                }
                catch (Exception ex)
                {
                    if (!succeeded)
                    {
                        _logger.LogWarning(ex, "AI provider {Provider} stream failed, trying next...", provider);
                        break; // Try next provider
                    }
                    throw; // Error mid-stream, can't recover
                }

                yield return current;
            }

            if (succeeded) yield break;
        }

        throw new InvalidOperationException("All AI providers failed for streaming.");
    }

    private IEnumerable<AiProvider> GetProviderOrder()
    {
        // Start with configured primary, then fallbacks
        var primary = _options.Provider;
        var all = new[] { AiProvider.Gemini, AiProvider.Groq, AiProvider.Anthropic };

        yield return primary;

        foreach (var provider in all.Where(p => p != primary))
        {
            yield return provider;
        }
    }

    private IAiService? CreateService(AiProvider provider)
    {
        try
        {
            return provider switch
            {
                AiProvider.Groq when !string.IsNullOrEmpty(_options.Groq?.ApiKey) =>
                    new GroqAiService(
                        _httpClientFactory,
                        Options.Create(_options),
                        _loggerFactory.CreateLogger<GroqAiService>()),

                AiProvider.Gemini when !string.IsNullOrEmpty(_options.Gemini?.ApiKey) =>
                    new GeminiAiService(
                        _httpClientFactory,
                        Options.Create(_options),
                        _loggerFactory.CreateLogger<GeminiAiService>()),

                AiProvider.Anthropic when !string.IsNullOrEmpty(_options.Anthropic?.ApiKey) =>
                    new AnthropicAiService(
                        Options.Create(_options),
                        _loggerFactory.CreateLogger<AnthropicAiService>()),

                _ => null
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create AI service for provider {Provider}", provider);
            return null;
        }
    }
}
