using System.Text.Json.Serialization;
using OneGood.Core.Enums;

namespace OneGood.Core.Models;

public class Cause
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// AI-generated short summary (1-2 sentences, ~50 words max).
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Original full description from the source.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    public CauseCategory Category { get; set; }
    public string OrganisationName { get; set; } = string.Empty;
    public string OrganisationUrl { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }

    // Funding progress (for donation actions)
    public decimal? FundingGoal { get; set; }
    public decimal? FundingCurrent { get; set; }
    public decimal? FundingGap => FundingGoal - FundingCurrent;

    // Urgency signals
    public DateTime? Deadline { get; set; }
    public double UrgencyScore { get; set; }
    public double LeverageScore { get; set; }
    public int ActionsToTippingPoint { get; set; }

    // Source tracking
    public string SourceApiName { get; set; } = string.Empty;
    public string SourceExternalId { get; set; } = string.Empty;
    public DateTime LastRefreshedAt { get; set; }
    public bool IsActive { get; set; } = true;

    [JsonIgnore]
    public ICollection<DailyAction> Actions { get; set; } = [];

    [JsonIgnore]
    public ICollection<CauseTranslation> Translations { get; set; } = [];
}
