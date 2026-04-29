namespace OneGood.Core.Models;

public class Outcome
{
    public Guid Id { get; set; }
    public Guid DailyActionId { get; set; }
    public DailyAction DailyAction { get; set; } = null!;

    public string Headline { get; set; } = string.Empty;
    public string Story { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public DateTime? OutcomeDate { get; set; }
    public bool IsPositive { get; set; }
    public int PeopleImpacted { get; set; }
    public string? SourceUrl { get; set; }

    // Stats at time of outcome
    public int TotalActionsContributed { get; set; }
    public decimal TotalDonationsRaised { get; set; }
}
