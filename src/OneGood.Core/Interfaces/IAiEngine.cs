using OneGood.Core.Enums;
using OneGood.Core.Models;

namespace OneGood.Core.Interfaces;

public interface IAiEngine
{
    Task<ScoredCause> ScoreCauseUrgencyAsync(Cause cause);
    Task<DailyAction> GenerateActionAsync(Cause cause, ActionType type, UserProfile? userProfile, string language = "en");
    Task<string> PersonaliseWhyYouAsync(DailyAction action, UserProfile userProfile);
    Task<Outcome> GenerateOutcomeStoryAsync(DailyAction action, string rawOutcomeData);
    Task<UserProfile> UpdateInferredPreferencesAsync(UserProfile profile, List<UserAction> recentActions);

    /// <summary>
    /// Summarizes a long description into 1-2 clear sentences (~50 words max).
    /// </summary>
    Task<string> SummarizeDescriptionAsync(string title, string description, CauseCategory category, string language = "en");
}

public record ScoredCause(
    double UrgencyScore,
    double LeverageScore,
    int ActionsToTippingPoint,
    string UrgencyReason);
