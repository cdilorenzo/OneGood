using OneGood.Core.Enums;
using OneGood.Core.Models;

namespace OneGood.Core.Interfaces;

public interface ICauseRepository
{
    Task<Cause?> GetByIdAsync(Guid id);
    Task<Cause?> GetBestCauseAsync(
        IEnumerable<Guid> excludeIds,
        IEnumerable<CauseCategory> preferredCategories,
        decimal maxDonationAmount,
        CauseCategory? filterCategory = null,
        IEnumerable<string>? filterSources = null,
        IEnumerable<string>? excludeSources = null);
    Task<Dictionary<CauseCategory, int>> GetCauseCountsByCategoryAsync();
    Task<Dictionary<string, int>> GetCauseCountsByActionTypeAsync();
    Task<DailyAction?> GetActionByIdAsync(Guid actionId);
    Task<DailyAction?> GetLatestActionByCauseIdAsync(Guid causeId, string? language = null);
    Task<List<Cause>> GetAllCausesAsync();
    Task UpsertAsync(Cause cause);
    Task UpsertBatchAsync(IEnumerable<Cause> causes);
    Task<int> DeactivateStaleCausesAsync(DateTime refreshedBefore);
    Task UpdateActionAsync(DailyAction action);
    Task AddActionAsync(DailyAction action);
    Task UpdateSummaryAsync(Guid causeId, string language, string summary);
    Task<string?> GetTranslatedSummaryAsync(Guid causeId, string language);
}
