using Microsoft.EntityFrameworkCore;
using OneGood.Core;
using OneGood.Core.Enums;
using OneGood.Core.Interfaces;
using OneGood.Core.Models;
using OneGood.Infrastructure.Data;

namespace OneGood.Infrastructure.Repositories;

public class CauseRepository : ICauseRepository
{
    private readonly OneGoodDbContext _db;

    public CauseRepository(OneGoodDbContext db) => _db = db;

    public async Task<Cause?> GetByIdAsync(Guid id)
        => await _db.Causes.FindAsync(id);

    public async Task<Cause?> GetBestCauseAsync(
        IEnumerable<Guid> excludeIds,
        IEnumerable<CauseCategory> preferredCategories,
        decimal maxDonationAmount,
        CauseCategory? filterCategory = null,
        IEnumerable<string>? filterSources = null,
        IEnumerable<string>? excludeSources = null)
    {
        var excludeList = excludeIds.ToList();
        var preferredList = preferredCategories.ToList();

        var query = _db.Causes
            .Where(c => c.IsActive && !excludeList.Contains(c.Id));

        // Hard filter: when user explicitly selects a category, only show that category
        if (filterCategory is not null)
        {
            query = query.Where(c => c.Category == filterCategory.Value);
        }

        // Hard filter: when user selects an action type, only show causes from matching sources
        if (filterSources is not null)
        {
            var sourceList = filterSources.ToList();
            if (sourceList.Count > 0)
            {
                query = query.Where(c => sourceList.Contains(c.SourceApiName));
            }
        }

        // Exclude sources (used for Share type — everything except donate/sign/write)
        if (excludeSources is not null)
        {
            var excludeSourceList = excludeSources.ToList();
            if (excludeSourceList.Count > 0)
            {
                query = query.Where(c => !excludeSourceList.Contains(c.SourceApiName));
            }
        }

        // Soft prefer
        if (preferredList.Count != 0)
        {
            query = query.OrderByDescending(c => preferredList.Contains(c.Category))
                         .ThenByDescending(c => c.UrgencyScore)
                         .ThenByDescending(c => c.LeverageScore);
        }
        else
        {
            query = query.OrderByDescending(c => c.UrgencyScore)
                         .ThenByDescending(c => c.LeverageScore);
        }

        return await query.FirstOrDefaultAsync();
    }

    public async Task<DailyAction?> GetActionByIdAsync(Guid actionId)
        => await _db.DailyActions
            .Include(a => a.Cause)
            .FirstOrDefaultAsync(a => a.Id == actionId);

    public async Task<DailyAction?> GetLatestActionByCauseIdAsync(Guid causeId, string? language = null)
        => await _db.DailyActions
            .Include(a => a.Cause)
            .Where(a => a.CauseId == causeId)
            .Where(a => language == null || a.Language == language)
            .OrderByDescending(a => a.ValidFrom)
            .FirstOrDefaultAsync();

    public async Task<List<Cause>> GetAllCausesAsync()
        => await _db.Causes
            .Where(c => c.IsActive)
            .OrderByDescending(c => c.UrgencyScore)
            .ToListAsync();

    public async Task<Dictionary<CauseCategory, int>> GetCauseCountsByCategoryAsync()
    {
        var causes = await _db.Causes.Where(c => c.IsActive).Select(c => c.Category).ToListAsync();
        return causes
            .GroupBy(c => c)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    public async Task<Dictionary<string, int>> GetCauseCountsByActionTypeAsync()
    {
        var sources = await _db.Causes
            .Where(c => c.IsActive)
            .Select(c => c.SourceApiName)
            .ToListAsync();

        var counts = new Dictionary<string, int>
        {
            ["Donate"] = 0,
            ["Sign"] = 0,
            ["Write"] = 0,
            ["Share"] = 0
        };

        foreach (var source in sources)
        {
            var type = ActionTypeMapping.FromSource(source);
            counts[type] = counts.GetValueOrDefault(type) + 1;
        }

        return counts;
    }

    public async Task UpsertAsync(Cause cause)
    {
        var existing = await _db.Causes
            .FirstOrDefaultAsync(c => c.SourceApiName == cause.SourceApiName
                                   && c.SourceExternalId == cause.SourceExternalId);

        if (existing is not null)
        {
            existing.Title = cause.Title;
            existing.Description = cause.Description;
            existing.FundingGoal = cause.FundingGoal;
            existing.FundingCurrent = cause.FundingCurrent;
            existing.Deadline = cause.Deadline;
            existing.UrgencyScore = cause.UrgencyScore;
            existing.LeverageScore = cause.LeverageScore;
            existing.ActionsToTippingPoint = cause.ActionsToTippingPoint;
            existing.LastRefreshedAt = DateTime.UtcNow;
            existing.IsActive = true; // Reactivate if it was deactivated
        }
        else
        {
            cause.Id = Guid.NewGuid();
            cause.LastRefreshedAt = DateTime.UtcNow;
            _db.Causes.Add(cause);
        }

        await _db.SaveChangesAsync();
    }

    public async Task UpsertBatchAsync(IEnumerable<Cause> causes)
    {
        foreach (var cause in causes)
        {
            await UpsertAsync(cause);
        }
    }

    public async Task UpdateActionAsync(DailyAction action)
    {
        _db.DailyActions.Update(action);
        await _db.SaveChangesAsync();
    }

    public async Task AddActionAsync(DailyAction action)
    {
        _db.DailyActions.Add(action);
        await _db.SaveChangesAsync();
    }

    public async Task<int> DeactivateStaleCausesAsync(DateTime refreshedBefore)
    {
        var staleCauses = await _db.Causes
            .Where(c => c.IsActive && c.LastRefreshedAt < refreshedBefore)
            .ToListAsync();

        foreach (var cause in staleCauses)
        {
            cause.IsActive = false;
        }

        await _db.SaveChangesAsync();
        return staleCauses.Count;
    }

    public async Task UpdateSummaryAsync(Guid causeId, string language, string summary)
    {
        var existing = await _db.CauseTranslations
            .FirstOrDefaultAsync(t => t.CauseId == causeId && t.Language == language);

        if (existing is not null)
        {
            existing.Summary = summary;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _db.CauseTranslations.Add(new CauseTranslation
            {
                Id = Guid.NewGuid(),
                CauseId = causeId,
                Language = language,
                Summary = summary,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();
    }

    public async Task<string?> GetTranslatedSummaryAsync(Guid causeId, string language)
    {
        var translation = await _db.CauseTranslations
            .FirstOrDefaultAsync(t => t.CauseId == causeId && t.Language == language);
        return translation?.Summary;
    }
}
