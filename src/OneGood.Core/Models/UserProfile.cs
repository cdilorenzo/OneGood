using OneGood.Core.Enums;

namespace OneGood.Core.Models;

public class UserProfile
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    // Google OAuth fields
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public string? GoogleId { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; }

    // What the user cares about — learned over time, not asked upfront
    public List<CauseCategory> PreferredCategories { get; set; } = [];

    // Capacity — what the AI should never exceed
    public decimal MaxDonationPerAction { get; set; } = 5.00m;
    public bool WillingToWrite { get; set; } = true;
    public bool WillingToDonate { get; set; } = true;
    public bool WillingToShare { get; set; } = true;

    // Payment method saved for frictionless giving
    public bool HasSavedPaymentMethod { get; set; }

    // Inferred preferences from behaviour — updated by AI
    public string? InferredInterestsSummary { get; set; }
    public int ActionsCompleted { get; set; }
    public decimal TotalDonated { get; set; }
    public int CurrentStreak { get; set; }
}
