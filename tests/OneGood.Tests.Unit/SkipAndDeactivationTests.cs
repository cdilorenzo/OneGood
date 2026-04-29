using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OneGood.Core.Enums;
using OneGood.Core.Interfaces;
using OneGood.Core.Models;
using OneGood.Infrastructure.Services;
using System.Text;
using System.Text.Json;

namespace OneGood.Tests.Unit;

/// <summary>
/// Tests for skip behavior, excludeCurrent, and cause deactivation logic.
/// </summary>
public class SkipAndDeactivationTests
{
    private readonly ICauseRepository _causeRepo;
    private readonly IUserRepository _userRepo;
    private readonly IAiEngine _aiEngine;
    private readonly IDistributedCache _cache;
    private readonly IContentCache _contentCache;
    private readonly ILogger<ActionEngine> _logger;
    private readonly ActionEngine _sut;

    public SkipAndDeactivationTests()
    {
        _causeRepo = Substitute.For<ICauseRepository>();
        _userRepo = Substitute.For<IUserRepository>();
        _aiEngine = Substitute.For<IAiEngine>();
        _cache = Substitute.For<IDistributedCache>();
        _contentCache = Substitute.For<IContentCache>();
        _logger = Substitute.For<ILogger<ActionEngine>>();

        _sut = new ActionEngine(_causeRepo, _userRepo, _aiEngine, _logger, _cache, _contentCache);
    }

    private void SetupCacheGetAsync(string key, string? value)
    {
        _cache.GetAsync(key, Arg.Any<CancellationToken>())
            .Returns(value is null ? null : Encoding.UTF8.GetBytes(value));
    }

    private Cause CreateCause(Guid? id = null, string title = "Test", string source = "betterplace.org") => new()
    {
        Id = id ?? Guid.NewGuid(),
        Title = title,
        Description = "Original description",
        Summary = "Original description",
        Category = CauseCategory.ClimateAndNature,
        SourceApiName = source,
        SourceExternalId = Guid.NewGuid().ToString(),
        OrganisationUrl = "https://example.org",
        IsActive = true,
        FundingGoal = 100m,
        FundingCurrent = 90m
    };

    private DailyAction CreateAction(Cause cause) => new()
    {
        Id = Guid.NewGuid(),
        CauseId = cause.Id,
        Cause = cause,
        Type = ActionType.Donate,
        Headline = "AI Headline",
        CallToAction = "Donate now",
        ImpactStatement = "You'll help"
    };

    [Fact]
    public async Task GetTodaysAction_ExcludesCurrent_EvenIfSkipNotProcessedYet()
    {
        // Simulates the race condition: skip POST hasn't been processed
        // but excludeCurrent is passed in the GET request
        var userId = Guid.Empty;
        var currentCauseId = Guid.NewGuid();
        var nextCauseId = Guid.NewGuid();

        // No skips in cache yet (skip POST hasn't arrived)
        SetupCacheGetAsync($"skipped-causes:today:{userId}", null);

        var currentCause = CreateCause(currentCauseId, "Current Cause");
        var nextCause = CreateCause(nextCauseId, "Next Cause");
        var nextAction = CreateAction(nextCause);

        _contentCache.GetActionOfTheDayAsync("en").Returns((CachedDailyAction?)null);
        _contentCache.GetSummaryAsync(Arg.Any<Guid>(), "en").Returns((string?)null);
        _contentCache.GetDailyActionAsync(Arg.Any<Guid>(), "en").Returns((CachedDailyAction?)null);

        // When excludeIds contains currentCauseId, return nextCause
        _causeRepo.GetBestCauseAsync(
            Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(currentCauseId)),
            Arg.Any<IEnumerable<CauseCategory>>(),
            Arg.Any<decimal>(),
            Arg.Any<CauseCategory?>())
            .Returns(nextCause);

        _causeRepo.GetLatestActionByCauseIdAsync(nextCauseId).Returns(nextAction);

        // Act — pass excludeCurrent (simulating the fix)
        var result = await _sut.GetTodaysActionAsync(userId, excludeCurrent: currentCauseId);

        // Assert — should get the NEXT cause, not the current one
        Assert.NotNull(result);
        Assert.Equal(nextCauseId, result.CauseId);
        Assert.NotEqual(currentCauseId, result.CauseId);
    }

    [Fact]
    public async Task SkipAction_AddsToSkipList_AndClearsUserCache()
    {
        var userId = Guid.NewGuid();
        var causeId = Guid.NewGuid();

        SetupCacheGetAsync($"skipped-causes:today:{userId}", null);

        await _sut.SkipActionAsync(userId, causeId);

        // Verify causeId was added to skip list
        await _cache.Received(1).SetAsync(
            $"skipped-causes:today:{userId}",
            Arg.Is<byte[]>(b => Encoding.UTF8.GetString(b).Contains(causeId.ToString())),
            Arg.Any<DistributedCacheEntryOptions>(),
            Arg.Any<CancellationToken>());

        // Verify user action caches were cleared
        await _cache.Received().RemoveAsync(
            Arg.Is<string>(key => key.StartsWith($"action:today:{userId}:")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetTodaysAction_WithCategoryFilter_OnlyReturnsCausesInCategory()
    {
        var userId = Guid.Empty;
        SetupCacheGetAsync($"skipped-causes:today:{userId}", null);

        var cause = CreateCause();
        cause.Category = CauseCategory.Education;
        var action = CreateAction(cause);

        _contentCache.GetActionOfTheDayAsync("en").Returns((CachedDailyAction?)null);
        _contentCache.GetSummaryAsync(cause.Id, "en").Returns((string?)null);
        _contentCache.GetDailyActionAsync(cause.Id, "en").Returns((CachedDailyAction?)null);

        _causeRepo.GetBestCauseAsync(
            Arg.Any<IEnumerable<Guid>>(),
            Arg.Any<IEnumerable<CauseCategory>>(),
            Arg.Any<decimal>(),
            Arg.Is<CauseCategory?>(c => c == CauseCategory.Education))
            .Returns(cause);

        _causeRepo.GetLatestActionByCauseIdAsync(cause.Id).Returns(action);

        // Act
        var result = await _sut.GetTodaysActionAsync(userId, category: "Education");

        // Assert
        Assert.NotNull(result);

        // Verify the category filter was passed through
        await _causeRepo.Received().GetBestCauseAsync(
            Arg.Any<IEnumerable<Guid>>(),
            Arg.Any<IEnumerable<CauseCategory>>(),
            Arg.Any<decimal>(),
            CauseCategory.Education);
    }

    [Fact]
    public async Task GetTodaysAction_ReturnsNull_WhenNoCausesExist()
    {
        var userId = Guid.Empty;
        SetupCacheGetAsync($"skipped-causes:today:{userId}", null);

        _contentCache.GetActionOfTheDayAsync("en").Returns((CachedDailyAction?)null);

        _causeRepo.GetBestCauseAsync(
            Arg.Any<IEnumerable<Guid>>(), Arg.Any<IEnumerable<CauseCategory>>(),
            Arg.Any<decimal>(), Arg.Any<CauseCategory?>())
            .Returns((Cause?)null);

        _causeRepo.GetCauseCountsByCategoryAsync()
            .Returns(new Dictionary<CauseCategory, int>());

        // Act
        var result = await _sut.GetTodaysActionAsync(userId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetTodaysAction_CombinesSkipsAndExcludeCurrent()
    {
        var userId = Guid.Empty;
        var skippedId = Guid.NewGuid();
        var excludeCurrentId = Guid.NewGuid();
        var freshCauseId = Guid.NewGuid();

        // One cause already skipped
        SetupCacheGetAsync($"skipped-causes:today:{userId}",
            JsonSerializer.Serialize(new List<Guid> { skippedId }));

        var freshCause = CreateCause(freshCauseId, "Fresh Cause");
        var freshAction = CreateAction(freshCause);

        _contentCache.GetActionOfTheDayAsync("en").Returns((CachedDailyAction?)null);
        _contentCache.GetSummaryAsync(freshCauseId, "en").Returns((string?)null);
        _contentCache.GetDailyActionAsync(freshCauseId, "en").Returns((CachedDailyAction?)null);

        // Verify both IDs are excluded
        _causeRepo.GetBestCauseAsync(
            Arg.Is<IEnumerable<Guid>>(ids =>
                ids.Contains(skippedId) && ids.Contains(excludeCurrentId)),
            Arg.Any<IEnumerable<CauseCategory>>(),
            Arg.Any<decimal>(),
            Arg.Any<CauseCategory?>())
            .Returns(freshCause);

        _causeRepo.GetLatestActionByCauseIdAsync(freshCauseId).Returns(freshAction);

        // Act
        var result = await _sut.GetTodaysActionAsync(userId, excludeCurrent: excludeCurrentId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(freshCauseId, result.CauseId);
    }
}
