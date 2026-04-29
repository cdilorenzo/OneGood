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

public class ActionEngineTests
{
    private readonly ICauseRepository _causeRepository;
    private readonly IUserRepository _userRepository;
    private readonly IAiEngine _aiEngine;
    private readonly IDistributedCache _cache;
    private readonly ILogger<ActionEngine> _logger;
    private readonly ActionEngine _sut;

    public ActionEngineTests()
    {
        _causeRepository = Substitute.For<ICauseRepository>();
        _userRepository = Substitute.For<IUserRepository>();
        _aiEngine = Substitute.For<IAiEngine>();
        _cache = Substitute.For<IDistributedCache>();
        _logger = Substitute.For<ILogger<ActionEngine>>();

        _sut = new ActionEngine(_causeRepository, _userRepository, _aiEngine, _logger, _cache);
    }

    private void SetupCacheGetAsync(string key, string? value)
    {
        _cache.GetAsync(key, Arg.Any<CancellationToken>())
            .Returns(value is null ? null : Encoding.UTF8.GetBytes(value));
    }

    [Fact]
    public async Task SkipActionAsync_ClearsCacheForAnonymousUser()
    {
        // Arrange
        var anonymousUserId = Guid.Empty;
        var causeId = Guid.NewGuid();

        SetupCacheGetAsync($"skipped-causes:today:{anonymousUserId}", null);

        // Act
        await _sut.SkipActionAsync(anonymousUserId, causeId);

        // Assert - cache should be cleared for all language/category combos
        await _cache.Received().RemoveAsync(
            Arg.Is<string>(key => key.StartsWith($"action:today:{anonymousUserId}:")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SkipActionAsync_TracksCauseIdInCache()
    {
        // Arrange
        var userId = Guid.Empty;
        var causeId = Guid.NewGuid();

        SetupCacheGetAsync($"skipped-causes:today:{userId}", null);

        // Act
        await _sut.SkipActionAsync(userId, causeId);

        // Assert - cause ID should be tracked in cache
        await _cache.Received(1).SetAsync(
            $"skipped-causes:today:{userId}",
            Arg.Is<byte[]>(b => Encoding.UTF8.GetString(b).Contains(causeId.ToString())),
            Arg.Any<DistributedCacheEntryOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetTodaysActionAsync_ExcludesSkippedCauses()
    {
        // Arrange
        var userId = Guid.Empty;
        var skippedCauseId = Guid.NewGuid();
        var newCauseId = Guid.NewGuid();

        var skippedCauseIds = new List<Guid> { skippedCauseId };
        SetupCacheGetAsync($"skipped-causes:today:{userId}", JsonSerializer.Serialize(skippedCauseIds));

        // Return null from action cache to force fetching a new cause
        SetupCacheGetAsync($"action:today:{userId}", null);

        var newCause = new Cause
        {
            Id = newCauseId,
            Title = "New Cause",
            Category = CauseCategory.ClimateAndNature,
            SourceApiName = "Test"
        };

        var generatedAction = new DailyAction
        {
            Id = Guid.NewGuid(),
            CauseId = newCauseId,
            Cause = newCause,
            Type = ActionType.Share,
            Headline = "Test",
            CallToAction = "Test",
            WhyNow = "Test",
            ImpactStatement = "Test"
        };

        _causeRepository.GetBestCauseAsync(
            Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(skippedCauseId)),
            Arg.Any<IEnumerable<CauseCategory>>(),
            Arg.Any<decimal>())
            .Returns(newCause);

        _aiEngine.GenerateActionAsync(newCause, ActionType.Share, null)
            .Returns(generatedAction);

        // Act
        var result = await _sut.GetTodaysActionAsync(userId);

        // Assert - verify GetBestCauseAsync was called with skipped cause ID in exclude list
        await _causeRepository.Received(1).GetBestCauseAsync(
            Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(skippedCauseId)),
            Arg.Any<IEnumerable<CauseCategory>>(),
            Arg.Any<decimal>());
    }

    [Fact]
    public async Task SkipActionAsync_MultipleSkips_AccumulatesAllCauses()
    {
        // Arrange
        var userId = Guid.Empty;
        var firstCauseId = Guid.NewGuid();
        var secondCauseId = Guid.NewGuid();

        // First skip
        SetupCacheGetAsync($"skipped-causes:today:{userId}", null);

        await _sut.SkipActionAsync(userId, firstCauseId);

        // Verify first cause was stored
        await _cache.Received().SetAsync(
            $"skipped-causes:today:{userId}",
            Arg.Is<byte[]>(b => Encoding.UTF8.GetString(b).Contains(firstCauseId.ToString())),
            Arg.Any<DistributedCacheEntryOptions>(),
            Arg.Any<CancellationToken>());

        // Second skip — simulate first cause already in cache
        _cache.ClearReceivedCalls();
        SetupCacheGetAsync($"skipped-causes:today:{userId}", JsonSerializer.Serialize(new List<Guid> { firstCauseId }));

        await _sut.SkipActionAsync(userId, secondCauseId);

        // Verify both causes are accumulated in the skip list
        await _cache.Received().SetAsync(
            $"skipped-causes:today:{userId}",
            Arg.Is<byte[]>(b => ContainsBothCauseIds(b, firstCauseId, secondCauseId)),
            Arg.Any<DistributedCacheEntryOptions>(),
            Arg.Any<CancellationToken>());
    }

    private static bool ContainsBothCauseIds(byte[] bytes, Guid first, Guid second)
    {
        var str = Encoding.UTF8.GetString(bytes);
        return str.Contains(first.ToString()) && str.Contains(second.ToString());
    }

    [Fact]
    public async Task GetTodaysActionAsync_WrapsAround_WhenAllCausesSkipped()
    {
        // Arrange
        var userId = Guid.Empty;
        var onlyCauseId = Guid.NewGuid();

        var skippedCauseIds = new List<Guid> { onlyCauseId };
        SetupCacheGetAsync($"skipped-causes:today:{userId}", JsonSerializer.Serialize(skippedCauseIds));

        var cause = new Cause
        {
            Id = onlyCauseId,
            Title = "The Only Cause",
            Category = CauseCategory.Education,
            SourceApiName = "Test"
        };

        var generatedAction = new DailyAction
        {
            Id = Guid.NewGuid(),
            CauseId = onlyCauseId,
            Cause = cause,
            Type = ActionType.Share,
            Headline = "Test",
            CallToAction = "Test",
            WhyNow = "Test",
            ImpactStatement = "Test"
        };

        // First call (with excludes) returns null — all causes are skipped
        _causeRepository.GetBestCauseAsync(
            Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(onlyCauseId)),
            Arg.Any<IEnumerable<CauseCategory>>(),
            Arg.Any<decimal>(),
            Arg.Any<CauseCategory?>())
            .Returns((Cause?)null);

        // Second call (after clearing skips) returns the cause
        _causeRepository.GetBestCauseAsync(
            Arg.Is<IEnumerable<Guid>>(ids => !ids.Contains(onlyCauseId)),
            Arg.Any<IEnumerable<CauseCategory>>(),
            Arg.Any<decimal>(),
            Arg.Any<CauseCategory?>())
            .Returns(cause);

        _aiEngine.GenerateActionAsync(cause, ActionType.Share, null, "en")
            .Returns(generatedAction);

        // Act
        var result = await _sut.GetTodaysActionAsync(userId);

        // Assert — should NOT be null, should wrap around
        Assert.NotNull(result);
        Assert.Equal(onlyCauseId, result.CauseId);

        // Verify the skip list was cleared
        await _cache.Received().RemoveAsync(
            $"skipped-causes:today:{userId}",
            Arg.Any<CancellationToken>());
    }
}
