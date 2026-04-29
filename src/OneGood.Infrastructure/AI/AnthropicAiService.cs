using System.Runtime.CompilerServices;
using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneGood.Core.AI;

namespace OneGood.Infrastructure.AI;

/// <summary>
/// AI service implementation using Anthropic Claude.
/// Claude is a sustainable choice - Anthropic focuses on AI safety and responsible development.
/// </summary>
public class AnthropicAiService : IAiService
{
    private readonly AnthropicClient _client;
    private readonly AnthropicOptions _options;
    private readonly ILogger<AnthropicAiService> _logger;

    public AnthropicAiService(
        IOptions<AiOptions> options,
        ILogger<AnthropicAiService> logger)
    {
        _options = options.Value.Anthropic;
        _logger = logger;

        if (string.IsNullOrEmpty(_options.ApiKey))
        {
            throw new InvalidOperationException(
                "Anthropic API key is not configured. Set AI:Anthropic:ApiKey in configuration.");
        }

        _client = new AnthropicClient(_options.ApiKey);
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
            var anthropicMessages = messages
                .Where(m => m.Role != Core.AI.ChatRole.System)
                .Select(m => new Message
                {
                    Role = MapRole(m.Role),
                    Content = [new TextContent { Text = m.Content }]
                })
                .ToList();

            var systemMessage = messages
                .FirstOrDefault(m => m.Role == Core.AI.ChatRole.System)?.Content;

            var parameters = new MessageParameters
            {
                Model = _options.Model,
                MaxTokens = _options.MaxTokens,
                Messages = anthropicMessages,
                SystemMessage = systemMessage
            };

            var response = await _client.Messages.GetClaudeMessageAsync(parameters, cancellationToken);

            return response.Content
                .OfType<TextContent>()
                .FirstOrDefault()?.Text ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Anthropic API");
            throw;
        }
    }

    public async IAsyncEnumerable<string> StreamCompletionAsync(
        string prompt,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var parameters = new MessageParameters
        {
            Model = _options.Model,
            MaxTokens = _options.MaxTokens,
            Messages = [new Message { Role = RoleType.User, Content = [new TextContent { Text = prompt }] }],
            Stream = true
        };

        await foreach (var response in _client.Messages.StreamClaudeMessageAsync(parameters, cancellationToken))
        {
            if (response.Delta?.Text is { } text)
            {
                yield return text;
            }
        }
    }

    private static RoleType MapRole(Core.AI.ChatRole role) => role switch
    {
        Core.AI.ChatRole.User => RoleType.User,
        Core.AI.ChatRole.Assistant => RoleType.Assistant,
        _ => RoleType.User
    };
}
