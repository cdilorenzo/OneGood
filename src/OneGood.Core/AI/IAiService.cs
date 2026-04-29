namespace OneGood.Core.AI;

/// <summary>
/// Abstraction for AI chat completion services.
/// Allows swapping providers (Anthropic, Azure, etc.) without changing application code.
/// </summary>
public interface IAiService
{
    /// <summary>
    /// Sends a message to the AI and returns the response.
    /// </summary>
    Task<string> GetCompletionAsync(string prompt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a message with conversation history context.
    /// </summary>
    Task<string> GetCompletionAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams the AI response as it's generated.
    /// </summary>
    IAsyncEnumerable<string> StreamCompletionAsync(string prompt, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a message in a conversation.
/// </summary>
public record ChatMessage(ChatRole Role, string Content);

/// <summary>
/// The role of a message sender.
/// </summary>
public enum ChatRole
{
    System,
    User,
    Assistant
}
