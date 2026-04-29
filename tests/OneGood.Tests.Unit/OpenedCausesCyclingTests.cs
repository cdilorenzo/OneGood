using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using OneGood.Core.Enums;
using OneGood.Core.Interfaces;
using OneGood.Core.Models;
using OneGood.Infrastructure.Data;
using OneGood.Infrastructure.Repositories;
using OneGood.Infrastructure.Services;

namespace OneGood.Tests.Unit;

/// <summary>
/// Integration-style tests that simulate the full user flow of opening causes,
/// skipping, and verifying opened causes reappear in the rotation.
/// Uses real InMemory DB + real DistributedMemoryCache (no mocks for these).
/// </summary>
public class OpenedCausesCyclingTests : IDisposable
{
    private readonly OneGoodDbContext _db;
    private readonly CauseRepository _causeRepository;
    private readonly IUserRepository _userRepository;
    private readonly IAiEngine _aiEngine;
    private readonly IDistributedCache _cache;
    private readonly ActionEngine _engine;
    private readonly Guid _userId = Guid.Empty; // anonymous

    // 3 causes in ClimateAndNature category with different urgency scores
    private readonly Cause _causeA;
    private readonly Cause _causeB;
    private readonly Cause _causeC;

    public OpenedCausesCyclingTests()
    {
        // Real InMemory DB
        var options = new DbContextOptionsBuilder<OneGoodDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new OneGoodDbContext(options);

        _causeRepository = new CauseRepository(_db);
        _userRepository = Substitute.For<IUserRepository>();
        _aiEngine = Substitute.For<IAiEngine>();
        var logger = Substitute.For<ILogger<ActionEngine>>();

        // Real InMemory distributed cache
        _cache = new MemoryDistributedCache(
            Options.Create(new MemoryDistributedCacheOptions()));

        _engine = new ActionEngine(_causeRepository, _userRepository, _aiEngine, logger, _cache);

        // Create 3 causes with descending urgency: A=95, B=80, C=70
        _causeA = new Cause
        {
            Id = Guid.NewGuid(),
            Title = "Cause A (highest urgency)",
            Description = "Description A",
            Category = CauseCategory.ClimateAndNature,
            SourceApiName = "Test",
            SourceExternalId = "a",
            UrgencyScore = 95,
            LeverageScore = 50
        };
        _causeB = new Cause
        {
            Id = Guid.NewGuid(),
            Title = "Cause B (medium urgency)",
            Description = "Description B",
            Category = CauseCategory.ClimateAndNature,
            SourceApiName = "Test",
            SourceExternalId = "b",
            UrgencyScore = 80,
            LeverageScore = 50
        };
        _causeC = new Cause
        {
            Id = Guid.NewGuid(),
            Title = "Cause C (lowest urgency)",
            Description = "Description C",
            Category = CauseCategory.ClimateAndNature,
            SourceApiName = "Test",
            SourceExternalId = "c",
            UrgencyScore = 70,
            LeverageScore = 50
        };

        _db.Causes.AddRange(_causeA, _causeB, _causeC);
        _db.SaveChanges();

        // Setup AI engine to return a simple action for any cause
        _aiEngine.GenerateActionAsync(Arg.Any<Cause>(), Arg.Any<ActionType>(), Arg.Any<UserProfile?>(), Arg.Any<string>())
            .Returns(callInfo =>
            {
                var cause = callInfo.ArgAt<Cause>(0);
                return new DailyAction
                {
                    Id = Guid.NewGuid(),
                    CauseId = cause.Id,
                    Cause = cause,
                    Type = ActionType.Share,
                    Headline = cause.Title,
                    CallToAction = "Test",
                    WhyNow = "Test",
                    ImpactStatement = "Test"
                };
            });
    }

    public void Dispose() => _db.Dispose();

    /// <summary>
    /// Simulates: Load → see A → skip A → see B → skip B → see C → skip C → wrap → see A again
    /// Verifies cycling works through all 3 causes.
    /// </summary>
    [Fact]
    public async Task SkipCyclesThroughAllCauses_ThenWrapsAround()
    {
        // Step 1: First load → should get A (highest urgency)
        var result1 = await _engine.GetTodaysActionAsync(_userId, "en", "ClimateAndNature");
        Assert.NotNull(result1);
        Assert.Equal(_causeA.Id, result1.CauseId);

        // Step 2: Skip A → should get B
        await _engine.SkipActionAsync(_userId, _causeA.Id);
        var result2 = await _engine.GetTodaysActionAsync(_userId, "en", "ClimateAndNature");
        Assert.NotNull(result2);
        Assert.Equal(_causeB.Id, result2.CauseId);

        // Step 3: Skip B → should get C
        await _engine.SkipActionAsync(_userId, _causeB.Id);
        var result3 = await _engine.GetTodaysActionAsync(_userId, "en", "ClimateAndNature");
        Assert.NotNull(result3);
        Assert.Equal(_causeC.Id, result3.CauseId);

        // Step 4: Skip C → all skipped → wrap around → should get A again
        await _engine.SkipActionAsync(_userId, _causeC.Id);
        var result4 = await _engine.GetTodaysActionAsync(_userId, "en", "ClimateAndNature");
        Assert.NotNull(result4);
        Assert.Equal(_causeA.Id, result4.CauseId);
    }

    /// <summary>
    /// Simulates: Open A → "show next" (excludeCurrent) → see B → skip B → should see A again.
    /// The opened cause A must NOT be in the skip list, so it reappears.
    /// </summary>
    [Fact]
    public async Task OpenedCause_ReappearsAfterSkippingOthers()
    {
        // Step 1: Load → get A
        var result1 = await _engine.GetTodaysActionAsync(_userId, "en", "ClimateAndNature");
        Assert.NotNull(result1);
        Assert.Equal(_causeA.Id, result1.CauseId);

        // Step 2: User "opens" A (frontend-only, no server call needed for opened tracking)
        // Then clicks "Show next cause" → uses excludeCurrent, NOT skip
        var result2 = await _engine.GetTodaysActionAsync(_userId, "en", "ClimateAndNature", excludeCurrent: _causeA.Id);
        Assert.NotNull(result2);
        Assert.Equal(_causeB.Id, result2.CauseId);

        // Step 3: Skip B (server-side skip)
        await _engine.SkipActionAsync(_userId, _causeB.Id);

        // Step 4: Load next → A should come back (it was never skipped, only transiently excluded)
        var result3 = await _engine.GetTodaysActionAsync(_userId, "en", "ClimateAndNature");
        Assert.NotNull(result3);
        Assert.Equal(_causeA.Id, result3.CauseId);
    }

    /// <summary>
    /// Full scenario: Open A → next → skip B → see A → skip A (opened) → see C → skip C → wrap → see A
    /// The key: skipping an opened cause uses excludeCurrent (simulated by the frontend),
    /// not the server skip list. So A never permanently disappears.
    /// </summary>
    [Fact]
    public async Task OpenedCause_NeverPermanentlyDisappears_FullCycle()
    {
        // Step 1: Load → A
        var result1 = await _engine.GetTodaysActionAsync(_userId, "en", "ClimateAndNature");
        Assert.Equal(_causeA.Id, result1!.CauseId);

        // Step 2: Open A, click "Show next" (excludeCurrent=A)
        var result2 = await _engine.GetTodaysActionAsync(_userId, "en", "ClimateAndNature", excludeCurrent: _causeA.Id);
        Assert.Equal(_causeB.Id, result2!.CauseId);

        // Step 3: Skip B
        await _engine.SkipActionAsync(_userId, _causeB.Id);

        // Step 4: Load → A comes back (not skipped)
        var result3 = await _engine.GetTodaysActionAsync(_userId, "en", "ClimateAndNature");
        Assert.Equal(_causeA.Id, result3!.CauseId);

        // Step 5: "Skip" A on frontend (but A is opened, so frontend uses excludeCurrent)
        var result4 = await _engine.GetTodaysActionAsync(_userId, "en", "ClimateAndNature", excludeCurrent: _causeA.Id);
        // skip list = [B], excludeCurrent = A → only C remains
        Assert.Equal(_causeC.Id, result4!.CauseId);

        // Step 6: Skip C
        await _engine.SkipActionAsync(_userId, _causeC.Id);

        // Step 7: Load → skip list = [B, C], no excludeCurrent → A comes back!
        var result5 = await _engine.GetTodaysActionAsync(_userId, "en", "ClimateAndNature");
        Assert.Equal(_causeA.Id, result5!.CauseId);

        // Step 8: excludeCurrent=A again → skip list [B,C] + A = all excluded → wrap around → A
        var result6 = await _engine.GetTodaysActionAsync(_userId, "en", "ClimateAndNature", excludeCurrent: _causeA.Id);
        Assert.NotNull(result6);
        // After wrap-around, skip list is cleared, excludeCurrent A still in local list
        // So returns B or C (whichever is highest urgency)
        Assert.Equal(_causeB.Id, result6.CauseId);
    }

    /// <summary>
    /// Edge case: Only 1 cause in category. Open it, "show next" → should still show it (wrap).
    /// </summary>
    [Fact]
    public async Task SingleCause_OpenAndNext_StillShowsCause()
    {
        // Remove B and C, keep only A
        _db.Causes.Remove(_causeB);
        _db.Causes.Remove(_causeC);
        await _db.SaveChangesAsync();

        // Load → A
        var result1 = await _engine.GetTodaysActionAsync(_userId, "en", "ClimateAndNature");
        Assert.Equal(_causeA.Id, result1!.CauseId);

        // Open A, "show next" with excludeCurrent=A
        // Only 1 cause, excluding it → null → wrap around → A again
        var result2 = await _engine.GetTodaysActionAsync(_userId, "en", "ClimateAndNature", excludeCurrent: _causeA.Id);
        Assert.NotNull(result2);
        Assert.Equal(_causeA.Id, result2.CauseId);
    }

    /// <summary>
    /// Verifies that handleSkip on a non-opened cause uses the server skip list (permanent),
    /// while handleSkip on an opened cause should use excludeCurrent (transient).
    /// This test verifies the server side: after skip, cause is excluded; after excludeCurrent, it isn't.
    /// </summary>
    [Fact]
    public async Task Skip_PermanentlyExcludes_ExcludeCurrent_DoesNot()
    {
        // Skip A permanently
        await _engine.SkipActionAsync(_userId, _causeA.Id);

        // Load → A is excluded, get B
        var result1 = await _engine.GetTodaysActionAsync(_userId, "en", "ClimateAndNature");
        Assert.Equal(_causeB.Id, result1!.CauseId);

        // Load again (no new skip) → A is STILL excluded, get B again
        var result2 = await _engine.GetTodaysActionAsync(_userId, "en", "ClimateAndNature");
        Assert.Equal(_causeB.Id, result2!.CauseId);

        // Now use excludeCurrent on B (transient) → should get C
        var result3 = await _engine.GetTodaysActionAsync(_userId, "en", "ClimateAndNature", excludeCurrent: _causeB.Id);
        Assert.Equal(_causeC.Id, result3!.CauseId);

        // Load without excludeCurrent → B is back (was only transiently excluded), A still skipped → get B
        var result4 = await _engine.GetTodaysActionAsync(_userId, "en", "ClimateAndNature");
        Assert.Equal(_causeB.Id, result4!.CauseId);
    }

    /// <summary>
    /// User skips causes in "All" view, then switches to a specific category.
    /// The causes in that category were already skipped globally.
    /// They should still appear (wrap-around should clear the global skip list).
    /// </summary>
    [Fact]
    public async Task SwitchCategory_AfterSkipping_StillShowsCauses()
    {
        // Add a cause in a different category
        var causeD = new Cause
        {
            Id = Guid.NewGuid(),
            Title = "Cause D (Education)",
            Description = "Description D",
            Category = CauseCategory.Education,
            SourceApiName = "Test",
            SourceExternalId = "d",
            UrgencyScore = 90,
            LeverageScore = 50
        };
        _db.Causes.Add(causeD);
        await _db.SaveChangesAsync();

        // Browse "All" (no category filter) and skip several causes
        // A (Climate, urgency=95) first
        var r1 = await _engine.GetTodaysActionAsync(_userId, "en");
        Assert.Equal(_causeA.Id, r1!.CauseId);
        await _engine.SkipActionAsync(_userId, _causeA.Id);

        // D (Education, urgency=90) next
        var r2 = await _engine.GetTodaysActionAsync(_userId, "en");
        Assert.Equal(causeD.Id, r2!.CauseId);
        await _engine.SkipActionAsync(_userId, causeD.Id);

        // B (Climate, urgency=80) next
        var r3 = await _engine.GetTodaysActionAsync(_userId, "en");
        Assert.Equal(_causeB.Id, r3!.CauseId);
        await _engine.SkipActionAsync(_userId, _causeB.Id);

        // Skip list = [A, D, B]. Now switch to "Climate & Nature"
        // A and B are both Climate and both skipped. C (urgency=70) is not skipped.
        var r4 = await _engine.GetTodaysActionAsync(_userId, "en", "ClimateAndNature");
        Assert.NotNull(r4);
        Assert.Equal(_causeC.Id, r4.CauseId);
    }

    /// <summary>
    /// User skips ALL causes in "All" view, then switches to a category.
    /// The skip list should wrap around and causes should still be shown.
    /// </summary>
    [Fact]
    public async Task SwitchCategory_AfterSkippingAll_WrapsAndShowsCauses()
    {
        // Skip all 3 Climate causes in "All" view
        await _engine.SkipActionAsync(_userId, _causeA.Id);
        await _engine.SkipActionAsync(_userId, _causeB.Id);
        await _engine.SkipActionAsync(_userId, _causeC.Id);

        // Now switch to "Climate & Nature" — all 3 are skipped
        // Wrap-around should clear skip list and return A
        var result = await _engine.GetTodaysActionAsync(_userId, "en", "ClimateAndNature");
        Assert.NotNull(result);
        Assert.Equal(_causeA.Id, result.CauseId);
    }

    /// <summary>
    /// User skips causes in one category, switches to another, skips there,
    /// then switches back. Causes should always be available.
    /// </summary>
    [Fact]
    public async Task SwitchBetweenCategories_CausesAlwaysAvailable()
    {
        var causeD = new Cause
        {
            Id = Guid.NewGuid(),
            Title = "Cause D (Education)",
            Description = "Description D",
            Category = CauseCategory.Education,
            SourceApiName = "Test",
            SourceExternalId = "d",
            UrgencyScore = 90,
            LeverageScore = 50
        };
        _db.Causes.Add(causeD);
        await _db.SaveChangesAsync();

        // In Climate: see A, skip it
        var r1 = await _engine.GetTodaysActionAsync(_userId, "en", "ClimateAndNature");
        Assert.Equal(_causeA.Id, r1!.CauseId);
        await _engine.SkipActionAsync(_userId, _causeA.Id);

        // Switch to Education: should see D (skip list has A but D is Education)
        var r2 = await _engine.GetTodaysActionAsync(_userId, "en", "Education");
        Assert.NotNull(r2);
        Assert.Equal(causeD.Id, r2.CauseId);

        // Skip D
        await _engine.SkipActionAsync(_userId, causeD.Id);

        // Switch back to Climate: skip list = [A, D]. A is Climate → excluded. B available.
        var r3 = await _engine.GetTodaysActionAsync(_userId, "en", "ClimateAndNature");
        Assert.NotNull(r3);
        Assert.Equal(_causeB.Id, r3.CauseId);

        // Skip B
        await _engine.SkipActionAsync(_userId, _causeB.Id);

        // Skip list = [A, D, B]. Climate has A,B skipped, C available.
        var r4 = await _engine.GetTodaysActionAsync(_userId, "en", "ClimateAndNature");
        Assert.NotNull(r4);
        Assert.Equal(_causeC.Id, r4.CauseId);

        // Skip C → skip list = [A,D,B,C]. All Climate skipped.
        await _engine.SkipActionAsync(_userId, _causeC.Id);

        // Climate should wrap around
        var r5 = await _engine.GetTodaysActionAsync(_userId, "en", "ClimateAndNature");
        Assert.NotNull(r5);
        // After wrap, skip list cleared, A is highest urgency
        Assert.Equal(_causeA.Id, r5.CauseId);

        // And Education should also work after the wrap
        var r6 = await _engine.GetTodaysActionAsync(_userId, "en", "Education");
        Assert.NotNull(r6);
        Assert.Equal(causeD.Id, r6.CauseId);
    }

    /// <summary>
    /// The last-resort fallback: even if the normal wrap-around somehow fails
    /// (edge case), the engine should still return a cause when causes exist.
    /// This simulates calling GetTodaysActionAsync when causes exist but the skip
    /// list is empty (wrap-around won't trigger), and verifies we always get a result.
    /// </summary>
    [Fact]
    public async Task NeverReturnsNull_WhenCausesExistInCategory()
    {
        // Request Climate causes — should always get one
        for (int i = 0; i < 10; i++)
        {
            var result = await _engine.GetTodaysActionAsync(_userId, "en", "ClimateAndNature");
            Assert.NotNull(result);

            // Skip it
            await _engine.SkipActionAsync(_userId, result.CauseId);
        }

        // After 10 iterations (3 causes, wrapped multiple times), still works
        var final = await _engine.GetTodaysActionAsync(_userId, "en", "ClimateAndNature");
        Assert.NotNull(final);
    }

    /// <summary>
    /// Rapid category switching — simulates user quickly clicking between categories.
    /// Must never return null when causes exist.
    /// </summary>
    [Fact]
    public async Task RapidCategorySwitching_NeverReturnsNull()
    {
        var causeD = new Cause
        {
            Id = Guid.NewGuid(),
            Title = "Cause D (Education)",
            Description = "Description D",
            Category = CauseCategory.Education,
            SourceApiName = "Test",
            SourceExternalId = "d",
            UrgencyScore = 90,
            LeverageScore = 50
        };
        _db.Causes.Add(causeD);
        await _db.SaveChangesAsync();

        // Simulate rapid switching: Climate → Education → All → Climate → skip → Education...
        var categories = new string?[] { "ClimateAndNature", "Education", null, "ClimateAndNature", "Education", null };

        foreach (var cat in categories)
        {
            var result = await _engine.GetTodaysActionAsync(_userId, "en", cat);
            Assert.NotNull(result);

            // Skip the cause shown
            await _engine.SkipActionAsync(_userId, result.CauseId);
        }

        // After skipping in many categories, every category should still work
        var r1 = await _engine.GetTodaysActionAsync(_userId, "en", "ClimateAndNature");
        Assert.NotNull(r1);

        var r2 = await _engine.GetTodaysActionAsync(_userId, "en", "Education");
        Assert.NotNull(r2);

        var r3 = await _engine.GetTodaysActionAsync(_userId, "en");
        Assert.NotNull(r3);
    }
}
