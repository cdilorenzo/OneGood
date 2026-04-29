using OneGood.Core.Enums;

namespace OneGood.Core.Models;

public class UserAction
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid DailyActionId { get; set; }
    public DailyAction DailyAction { get; set; } = null!;

    public DateTime CompletedAt { get; set; }
    public ActionType ActionType { get; set; }
    public decimal? AmountDonated { get; set; }
    public bool SawOutcome { get; set; }
    public string? UserNote { get; set; }
}
