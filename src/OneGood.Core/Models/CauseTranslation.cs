namespace OneGood.Core.Models;

/// <summary>
/// Stores a language-specific AI-generated summary for a cause.
/// Persisted to survive cache eviction so summaries don't need to be regenerated.
/// </summary>
public class CauseTranslation
{
    public Guid Id { get; set; }
    public Guid CauseId { get; set; }
    public Cause Cause { get; set; } = null!;

    /// <summary>
    /// ISO 639-1 language code (e.g. "en", "de", "it").
    /// </summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// AI-generated summary in the target language.
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
