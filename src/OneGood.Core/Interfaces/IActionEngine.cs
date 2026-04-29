using OneGood.Core.Models;

namespace OneGood.Core.Interfaces;

public interface IActionEngine
{
    Task<DailyAction?> GetTodaysActionAsync(Guid userId, string language = "en", string? category = null, Guid? excludeCurrent = null, string? actionType = null);
    Task<DailyAction?> GenerateActionForCauseAsync(Guid userId, Guid causeId, string language = "en");
    Task<CompleteActionResult> CompleteActionAsync(Guid userId, Guid actionId, CompleteActionRequest request);
    Task<List<ActionHistoryItem>> GetHistoryAsync(Guid userId);
    Task<Outcome?> GetOutcomeAsync(Guid actionId);
    Task SkipActionAsync(Guid userId, Guid causeId);
}

public record CompleteActionRequest(
    Guid ActionId,
    decimal? AmountDonated = null,
    string? UserNote = null);

public record CompleteActionResult(
    string ImpactMessage,
    int TotalContributors,
    string OutcomeUrl);

public record ActionHistoryItem(
    Guid ActionId,
    string Headline,
    string ActionType,
    DateTime CompletedAt,
    decimal? AmountDonated,
    bool HasOutcome,
    string? OutcomeHeadline);
