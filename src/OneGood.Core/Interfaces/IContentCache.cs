namespace OneGood.Core.Interfaces;

/// <summary>
/// Interface for caching AI-generated content per cause.
/// This separates AI content caching (permanent per cause) from user session caching.
/// </summary>
public interface IContentCache
{
    /// <summary>
    /// Gets a cached AI summary for a cause in a specific language.
    /// </summary>
    Task<string?> GetSummaryAsync(Guid causeId, string language);

    /// <summary>
    /// Caches an AI summary for a cause in a specific language.
    /// </summary>
    Task SetSummaryAsync(Guid causeId, string language, string summary);

    /// <summary>
    /// Gets a cached daily action for a cause in a specific language.
    /// </summary>
    Task<CachedDailyAction?> GetDailyActionAsync(Guid causeId, string language);

    /// <summary>
    /// Caches a daily action for a cause in a specific language.
    /// </summary>
    Task SetDailyActionAsync(Guid causeId, string language, CachedDailyAction action);

    /// <summary>
    /// Gets the pre-warmed "action of the day" for anonymous users.
    /// </summary>
    Task<CachedDailyAction?> GetActionOfTheDayAsync(string language);

    /// <summary>
    /// Sets the pre-warmed "action of the day" for anonymous users.
    /// </summary>
    Task SetActionOfTheDayAsync(string language, CachedDailyAction action, TimeSpan ttl);

    /// <summary>
    /// Removes all cached content for a cause (when cause is deleted/deactivated).
    /// </summary>
    Task InvalidateCauseAsync(Guid causeId);

    /// <summary>
    /// Removes all cached content for causes not in the provided list (cleanup old causes).
    /// </summary>
    Task CleanupStaleCausesAsync(IEnumerable<Guid> activeCauseIds);
}

/// <summary>
/// Cached daily action data (serializable for storage).
/// </summary>
public record CachedDailyAction
{
    public Guid ActionId { get; init; }
    public Guid CauseId { get; init; }
    public string Type { get; init; } = string.Empty;
    public string Headline { get; init; } = string.Empty;
    public string CallToAction { get; init; } = string.Empty;
    public string WhyNow { get; init; } = string.Empty;
    public string ImpactStatement { get; init; } = string.Empty;
    public string CauseCategory { get; init; } = string.Empty;
    public string CauseOrganisation { get; init; } = string.Empty;
    public string CauseUrl { get; init; } = string.Empty;
    public string? CauseImageUrl { get; init; }
    public string CauseSummary { get; init; } = string.Empty;
    public string CauseDescription { get; init; } = string.Empty;
    public decimal? SuggestedAmount { get; init; }
    public string? PaymentLinkUrl { get; init; }
    public string? PreWrittenLetter { get; init; }
    public string? RecipientName { get; init; }
    public string? RecipientEmail { get; init; }
    public string? ShareText { get; init; }
    public string? ShareUrl { get; init; }
    public bool IsAiGenerated { get; init; }
    public DateTime GeneratedAt { get; init; }
}
