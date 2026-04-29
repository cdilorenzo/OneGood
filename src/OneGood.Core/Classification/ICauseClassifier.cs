using OneGood.Core.Enums;

namespace OneGood.Core.Classification;

/// <summary>
/// AI-powered cause classifier that determines the best <see cref="CauseCategory"/>
/// from textual content. Classification is performed by an LLM for high accuracy
/// across languages and domains.
/// </summary>
public interface ICauseClassifier
{
    /// <summary>
    /// Classifies a cause using AI based on its title and description.
    /// Results are cached by content hash to avoid redundant LLM calls.
    /// </summary>
    /// <param name="title">The cause title.</param>
    /// <param name="description">The cause description (can be empty).</param>
    /// <param name="fallback">Category to return if AI classification fails entirely.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<CauseCategory> ClassifyAsync(
        string? title,
        string? description = null,
        CauseCategory fallback = CauseCategory.HumanRights,
        CancellationToken cancellationToken = default);
}
