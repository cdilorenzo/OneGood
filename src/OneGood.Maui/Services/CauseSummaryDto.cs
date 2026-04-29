namespace OneGood.Maui.Services;

public class CauseSummaryDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string SourceApiName { get; set; } = string.Empty;
    public string OrganisationName { get; set; } = string.Empty;
    public string? OrganisationUrl { get; set; }
    public string? ImageUrl { get; set; }
    public decimal? FundingGoal { get; set; }
    public decimal? FundingCurrent { get; set; }
    public double UrgencyScore { get; set; }
    public double LeverageScore { get; set; }
}
