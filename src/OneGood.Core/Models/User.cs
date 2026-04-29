using OneGood.Core.Enums;

namespace OneGood.Core.Models;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime LastActiveAt { get; set; }
    public UserProfile Profile { get; set; } = new();
    public ICollection<UserAction> CompletedActions { get; set; } = [];
    public bool IsAnonymous { get; set; }
    public string? StripeCustomerId { get; set; }
    public string? TimeZone { get; set; }
    public string? CountryCode { get; set; }
}
