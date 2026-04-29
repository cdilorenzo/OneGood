using Microsoft.EntityFrameworkCore;
using OneGood.Core.Enums;
using OneGood.Core.Models;
using OneGood.Infrastructure.Data;
using OneGood.Infrastructure.Repositories;

namespace OneGood.Tests.Unit;

/// <summary>
/// Tests for CauseRepository — verifies IsActive filtering, deactivation,
/// upsert behavior, and duplicate prevention.
/// </summary>
public class CauseRepositoryTests : IDisposable
{
    private readonly OneGoodDbContext _db;
    private readonly CauseRepository _sut;

    public CauseRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<OneGoodDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new OneGoodDbContext(options);
        _sut = new CauseRepository(_db);
    }

    public void Dispose() => _db.Dispose();

    private Cause CreateCause(string title = "Test", string source = "betterplace.org",
        string externalId = "1", bool isActive = true) => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        Description = "Description",
        Summary = "Summary",
        Category = CauseCategory.ClimateAndNature,
        SourceApiName = source,
        SourceExternalId = externalId,
        OrganisationName = "Org",
        OrganisationUrl = "https://example.org",
        IsActive = isActive,
        LastRefreshedAt = DateTime.UtcNow,
        UrgencyScore = 80
    };

    [Fact]
    public async Task GetAllCausesAsync_ReturnsOnlyActiveCauses()
    {
        // Arrange
        var active = CreateCause("Active", externalId: "1");
        var inactive = CreateCause("Inactive", externalId: "2");
        inactive.IsActive = false;

        _db.Causes.AddRange(active, inactive);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetAllCausesAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal("Active", result[0].Title);
    }

    [Fact]
    public async Task GetBestCauseAsync_ExcludesInactiveCauses()
    {
        // Arrange — only inactive cause exists
        var inactive = CreateCause("Inactive", externalId: "1");
        inactive.IsActive = false;
        inactive.UrgencyScore = 99;

        _db.Causes.Add(inactive);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetBestCauseAsync([], [], 5m);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetCauseCountsByCategoryAsync_CountsOnlyActiveCauses()
    {
        // Arrange
        var active1 = CreateCause("A1", externalId: "1");
        active1.Category = CauseCategory.Education;
        var active2 = CreateCause("A2", externalId: "2");
        active2.Category = CauseCategory.Education;
        var inactive = CreateCause("I1", externalId: "3");
        inactive.Category = CauseCategory.Education;
        inactive.IsActive = false;

        _db.Causes.AddRange(active1, active2, inactive);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetCauseCountsByCategoryAsync();

        // Assert — should count only 2 active causes
        Assert.Equal(2, result[CauseCategory.Education]);
    }

    [Fact]
    public async Task DeactivateStaleCausesAsync_FlagsOldCausesAsInactive()
    {
        // Arrange
        var fresh = CreateCause("Fresh", externalId: "1");
        fresh.LastRefreshedAt = DateTime.UtcNow;

        var stale = CreateCause("Stale", externalId: "2");
        stale.LastRefreshedAt = DateTime.UtcNow.AddHours(-7);

        _db.Causes.AddRange(fresh, stale);
        await _db.SaveChangesAsync();

        // Act — deactivate causes not refreshed in the last 5 minutes
        var threshold = DateTime.UtcNow.AddMinutes(-5);
        var count = await _sut.DeactivateStaleCausesAsync(threshold);

        // Assert
        Assert.Equal(1, count);

        var freshFromDb = await _db.Causes.FindAsync(fresh.Id);
        var staleFromDb = await _db.Causes.FindAsync(stale.Id);
        Assert.True(freshFromDb!.IsActive);
        Assert.False(staleFromDb!.IsActive);
    }

    [Fact]
    public async Task DeactivateStaleCausesAsync_DoesNotDeactivateAlreadyInactiveCauses()
    {
        // Arrange
        var alreadyInactive = CreateCause("Already Inactive", externalId: "1");
        alreadyInactive.IsActive = false;
        alreadyInactive.LastRefreshedAt = DateTime.UtcNow.AddDays(-1);

        _db.Causes.Add(alreadyInactive);
        await _db.SaveChangesAsync();

        // Act
        var count = await _sut.DeactivateStaleCausesAsync(DateTime.UtcNow);

        // Assert — should not count already-inactive causes
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task UpsertAsync_UpdatesExistingCause_BySourceKey()
    {
        // Arrange — insert a cause
        var original = CreateCause("Original Title", "betterplace.org", "ext-123");
        _db.Causes.Add(original);
        await _db.SaveChangesAsync();

        // Act — upsert with same source key but different title
        var updated = CreateCause("Updated Title", "betterplace.org", "ext-123");
        await _sut.UpsertAsync(updated);

        // Assert — should update, not create a duplicate
        var allCauses = await _db.Causes.ToListAsync();
        Assert.Single(allCauses);
        Assert.Equal("Updated Title", allCauses[0].Title);
    }

    [Fact]
    public async Task UpsertAsync_InsertsNewCause_WhenNoMatch()
    {
        // Act
        var cause = CreateCause("New Cause", "openPetition.de", "ext-456");
        await _sut.UpsertAsync(cause);

        // Assert
        var allCauses = await _db.Causes.ToListAsync();
        Assert.Single(allCauses);
        Assert.Equal("New Cause", allCauses[0].Title);
    }

    [Fact]
    public async Task UpsertAsync_ReactivatesCause_WhenRefreshed()
    {
        // Arrange — cause was deactivated
        var cause = CreateCause("Reactivated", "betterplace.org", "ext-789");
        cause.IsActive = false;
        _db.Causes.Add(cause);
        await _db.SaveChangesAsync();

        // Act — same source key comes back from API
        var refreshed = CreateCause("Reactivated", "betterplace.org", "ext-789");
        refreshed.IsActive = true;
        await _sut.UpsertAsync(refreshed);

        // Assert — cause should be reactivated
        var fromDb = await _db.Causes.FirstAsync();
        Assert.True(fromDb.IsActive);
    }

    [Fact]
    public async Task UpdateSummaryAsync_PersistsSummaryToDatabase()
    {
        // Arrange
        var cause = CreateCause("Cause With Summary", externalId: "sum-1");
        cause.Summary = "Original truncated...";
        _db.Causes.Add(cause);
        await _db.SaveChangesAsync();

        // Act
        await _sut.UpdateSummaryAsync(cause.Id, "en", "AI-generated detailed summary.");

        // Assert — stored in CauseTranslation table
        var translation = await _db.CauseTranslations
            .FirstOrDefaultAsync(t => t.CauseId == cause.Id && t.Language == "en");
        Assert.NotNull(translation);
        Assert.Equal("AI-generated detailed summary.", translation.Summary);
    }

    [Fact]
    public async Task UpdateSummaryAsync_UpsertsSameLanguage()
    {
        // Arrange
        var cause = CreateCause("Cause", externalId: "sum-2");
        _db.Causes.Add(cause);
        await _db.SaveChangesAsync();
        await _sut.UpdateSummaryAsync(cause.Id, "de", "Erste Zusammenfassung");

        // Act — update same language
        await _sut.UpdateSummaryAsync(cause.Id, "de", "Aktualisierte Zusammenfassung");

        // Assert — only one row, updated
        var translations = await _db.CauseTranslations
            .Where(t => t.CauseId == cause.Id && t.Language == "de").ToListAsync();
        Assert.Single(translations);
        Assert.Equal("Aktualisierte Zusammenfassung", translations[0].Summary);
    }

    [Fact]
    public async Task GetTranslatedSummaryAsync_ReturnsSummaryForLanguage()
    {
        // Arrange
        var cause = CreateCause("Cause", externalId: "sum-3");
        _db.Causes.Add(cause);
        await _db.SaveChangesAsync();
        await _sut.UpdateSummaryAsync(cause.Id, "en", "English summary");
        await _sut.UpdateSummaryAsync(cause.Id, "de", "Deutsche Zusammenfassung");

        // Act
        var en = await _sut.GetTranslatedSummaryAsync(cause.Id, "en");
        var de = await _sut.GetTranslatedSummaryAsync(cause.Id, "de");
        var it = await _sut.GetTranslatedSummaryAsync(cause.Id, "it");

        // Assert
        Assert.Equal("English summary", en);
        Assert.Equal("Deutsche Zusammenfassung", de);
        Assert.Null(it);
    }

    [Fact]
    public async Task GetLatestActionByCauseIdAsync_ReturnsNewestAction()
    {
        // Arrange
        var cause = CreateCause("Cause", externalId: "act-1");
        _db.Causes.Add(cause);

        var older = new DailyAction
        {
            Id = Guid.NewGuid(), CauseId = cause.Id, Cause = cause,
            Type = ActionType.Donate, Headline = "Old",
            ValidFrom = DateTime.UtcNow.AddDays(-2)
        };
        var newer = new DailyAction
        {
            Id = Guid.NewGuid(), CauseId = cause.Id, Cause = cause,
            Type = ActionType.Donate, Headline = "New",
            ValidFrom = DateTime.UtcNow
        };
        _db.DailyActions.AddRange(older, newer);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetLatestActionByCauseIdAsync(cause.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("New", result.Headline);
    }

    [Fact]
    public async Task GetLatestActionByCauseIdAsync_ReturnsNull_WhenNoActions()
    {
        // Arrange
        var cause = CreateCause("No Actions", externalId: "noact-1");
        _db.Causes.Add(cause);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetLatestActionByCauseIdAsync(cause.Id);

        // Assert
        Assert.Null(result);
    }
}
