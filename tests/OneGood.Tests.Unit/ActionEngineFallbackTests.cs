using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using OneGood.Core.Enums;
using OneGood.Core.Interfaces;
using OneGood.Core.Models;
using OneGood.Infrastructure.Services;
using System.Text;
using System.Text.Json;

namespace OneGood.Tests.Unit;

/// <summary>
/// Tests for AI fallback behavior — verifies that the action engine always returns
/// content from source data even when AI generation fails completely.
/// </summary>
public class ActionEngineFallbackTests
{
    private readonly ICauseRepository _causeRepo;
    private readonly IUserRepository _userRepo;
    private readonly IAiEngine _aiEngine;
    private readonly IDistributedCache _cache;
    private readonly IContentCache _contentCache;
    private readonly ILogger<ActionEngine> _logger;
    private readonly ActionEngine _sut;

    public ActionEngineFallbackTests()
    {
        _causeRepo = Substitute.For<ICauseRepository>();
        _userRepo = Substitute.For<IUserRepository>();
        _aiEngine = Substitute.For<IAiEngine>();
        _cache = Substitute.For<IDistributedCache>();
        _contentCache = Substitute.For<IContentCache>();
        _logger = Substitute.For<ILogger<ActionEngine>>();

        _sut = new ActionEngine(_causeRepo, _userRepo, _aiEngine, _logger, _cache, _contentCache);
    }

    private Cause CreateTestCause(string title = "Test Cause", string source = "betterplace.org") => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        Description = "This is the original description from the source API.",
        Category = CauseCategory.AnimalWelfare,
        SourceApiName = source,
        SourceExternalId = "123",
        OrganisationName = "Test Org",
        OrganisationUrl = "https://example.org/donate",
        FundingGoal = 1000m,
        FundingCurrent = 950m,
        IsActive = true,
        Summary = "This is the original description from the source API."
    };

    private void SetupNoSkips()
    {
        _cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((byte[]?)null);
    }

    [Fact]
    public async Task GetTodaysAction_ReturnsSourceData_WhenAiGenerationFails()
    {
        // Arrange
        SetupNoSkips();
        var cause = CreateTestCause();

        _causeRepo.GetBestCauseAsync(
            Arg.Any<IEnumerable<Guid>>(), Arg.Any<IEnumerable<CauseCategory>>(),
            Arg.Any<decimal>(), Arg.Any<CauseCategory?>())
            .Returns(cause);

        _causeRepo.GetLatestActionByCauseIdAsync(cause.Id).Returns((DailyAction?)null);
        _contentCache.GetDailyActionAsync(cause.Id, "en").Returns((CachedDailyAction?)null);
        _contentCache.GetSummaryAsync(cause.Id, "en").Returns((string?)null);
        _contentCache.GetActionOfTheDayAsync("en").Returns((CachedDailyAction?)null);

        // AI completely fails
        _aiEngine.GenerateActionAsync(Arg.Any<Cause>(), Arg.Any<ActionType>(), Arg.Any<UserProfile?>(), Arg.Any<string>())
            .ThrowsAsync(new InvalidOperationException("All AI providers failed"));

        // Act
        var result = await _sut.GetTodaysActionAsync(Guid.Empty);

        // Assert — should return a fallback action with source data, NOT throw
        Assert.NotNull(result);
        Assert.Equal(cause.Title, result.Headline);
        Assert.Equal(cause.Id, result.CauseId);
        Assert.Equal(ActionType.Donate, result.Type);
    }

    [Fact]
    public async Task GetTodaysAction_ReturnsCauseDescription_WhenNoAiSummaryAvailable()
    {
        // Arrange
        SetupNoSkips();
        var cause = CreateTestCause();
        cause.Summary = "Short trun..."; // truncated placeholder ending with "..."

        _causeRepo.GetBestCauseAsync(
            Arg.Any<IEnumerable<Guid>>(), Arg.Any<IEnumerable<CauseCategory>>(),
            Arg.Any<decimal>(), Arg.Any<CauseCategory?>())
            .Returns(cause);

        _causeRepo.GetLatestActionByCauseIdAsync(cause.Id).Returns((DailyAction?)null);
        _contentCache.GetDailyActionAsync(cause.Id, "en").Returns((CachedDailyAction?)null);
        _contentCache.GetSummaryAsync(cause.Id, "en").Returns((string?)null);
        _contentCache.GetActionOfTheDayAsync("en").Returns((CachedDailyAction?)null);

        _aiEngine.GenerateActionAsync(Arg.Any<Cause>(), Arg.Any<ActionType>(), Arg.Any<UserProfile?>(), Arg.Any<string>())
            .ThrowsAsync(new InvalidOperationException("AI failed"));

        // Act
        var result = await _sut.GetTodaysActionAsync(Guid.Empty);

        // Assert — summary should be full description, not the truncated placeholder
        Assert.NotNull(result);
        Assert.Equal(cause.Description, result.Cause.Summary);
    }

    [Fact]
    public async Task GetTodaysAction_UsesAiSummaryFromCache_WhenAvailable()
    {
        // Arrange
        SetupNoSkips();
        var cause = CreateTestCause();
        var aiSummary = "AI-generated concise summary of the cause.";

        _causeRepo.GetBestCauseAsync(
            Arg.Any<IEnumerable<Guid>>(), Arg.Any<IEnumerable<CauseCategory>>(),
            Arg.Any<decimal>(), Arg.Any<CauseCategory?>())
            .Returns(cause);

        _causeRepo.GetLatestActionByCauseIdAsync(cause.Id).Returns((DailyAction?)null);
        _contentCache.GetSummaryAsync(cause.Id, "en").Returns(aiSummary);
        _contentCache.GetDailyActionAsync(cause.Id, "en").Returns((CachedDailyAction?)null);
        _contentCache.GetActionOfTheDayAsync("en").Returns((CachedDailyAction?)null);

        _aiEngine.GenerateActionAsync(Arg.Any<Cause>(), Arg.Any<ActionType>(), Arg.Any<UserProfile?>(), Arg.Any<string>())
            .ThrowsAsync(new InvalidOperationException("AI failed"));

        // Act
        var result = await _sut.GetTodaysActionAsync(Guid.Empty);

        // Assert — should use the cached AI summary
        Assert.NotNull(result);
        Assert.Equal(aiSummary, result.Cause.Summary);
    }

    [Fact]
    public async Task GetTodaysAction_NeverCallsAiSummary_InRequestPath()
    {
        // Arrange
        SetupNoSkips();
        var cause = CreateTestCause();

        _causeRepo.GetBestCauseAsync(
            Arg.Any<IEnumerable<Guid>>(), Arg.Any<IEnumerable<CauseCategory>>(),
            Arg.Any<decimal>(), Arg.Any<CauseCategory?>())
            .Returns(cause);

        _causeRepo.GetLatestActionByCauseIdAsync(cause.Id).Returns((DailyAction?)null);
        _contentCache.GetSummaryAsync(cause.Id, "en").Returns((string?)null);
        _contentCache.GetDailyActionAsync(cause.Id, "en").Returns((CachedDailyAction?)null);
        _contentCache.GetActionOfTheDayAsync("en").Returns((CachedDailyAction?)null);

        var fallbackAction = new DailyAction
        {
            Id = Guid.NewGuid(), CauseId = cause.Id, Cause = cause,
            Type = ActionType.Donate, Headline = "Test"
        };
        _aiEngine.GenerateActionAsync(Arg.Any<Cause>(), Arg.Any<ActionType>(), Arg.Any<UserProfile?>(), Arg.Any<string>())
            .Returns(fallbackAction);

        // Act
        await _sut.GetTodaysActionAsync(Guid.Empty);

        // Assert — SummarizeDescriptionAsync should NEVER be called in request path
        await _aiEngine.DidNotReceive().SummarizeDescriptionAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CauseCategory>(), Arg.Any<string>());
    }

    [Fact]
    public async Task GetTodaysAction_PersonalisationFailure_DoesNotCrash()
    {
        // Arrange
        SetupNoSkips();
        var userId = Guid.NewGuid();
        var cause = CreateTestCause();
        var profile = new UserProfile { UserId = userId };

        _userRepo.GetProfileAsync(userId).Returns(profile);
        _userRepo.GetRecentCauseIdsAsync(userId, 30).Returns(new List<Guid>());

        _causeRepo.GetBestCauseAsync(
            Arg.Any<IEnumerable<Guid>>(), Arg.Any<IEnumerable<CauseCategory>>(),
            Arg.Any<decimal>(), Arg.Any<CauseCategory?>())
            .Returns(cause);

        _causeRepo.GetLatestActionByCauseIdAsync(cause.Id).Returns((DailyAction?)null);
        _contentCache.GetSummaryAsync(cause.Id, "en").Returns((string?)null);
        _contentCache.GetDailyActionAsync(cause.Id, "en").Returns((CachedDailyAction?)null);

        var action = new DailyAction
        {
            Id = Guid.NewGuid(), CauseId = cause.Id, Cause = cause,
            Type = ActionType.Donate, Headline = "Test"
        };
        _aiEngine.GenerateActionAsync(Arg.Any<Cause>(), Arg.Any<ActionType>(), Arg.Any<UserProfile?>(), Arg.Any<string>())
            .Returns(action);

        // Personalisation fails
        _aiEngine.PersonaliseWhyYouAsync(Arg.Any<DailyAction>(), Arg.Any<UserProfile>())
            .ThrowsAsync(new InvalidOperationException("Rate limited"));

        // Act
        var result = await _sut.GetTodaysActionAsync(userId);

        // Assert — should still return the action
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetTodaysAction_UsesExistingDbAction_InsteadOfGeneratingNew()
    {
        // Arrange
        SetupNoSkips();
        var cause = CreateTestCause();
        var existingAction = new DailyAction
        {
            Id = Guid.NewGuid(),
            CauseId = cause.Id,
            Cause = cause,
            Type = ActionType.Donate,
            Headline = "Existing AI headline from DB",
            ImpactStatement = "Existing impact"
        };

        _causeRepo.GetBestCauseAsync(
            Arg.Any<IEnumerable<Guid>>(), Arg.Any<IEnumerable<CauseCategory>>(),
            Arg.Any<decimal>(), Arg.Any<CauseCategory?>())
            .Returns(cause);

        _contentCache.GetSummaryAsync(cause.Id, "en").Returns((string?)null);
        _contentCache.GetDailyActionAsync(cause.Id, "en").Returns((CachedDailyAction?)null);
        _contentCache.GetActionOfTheDayAsync("en").Returns((CachedDailyAction?)null);

        // Existing action in DB
        _causeRepo.GetLatestActionByCauseIdAsync(cause.Id, "en").Returns(existingAction);

        // Act
        var result = await _sut.GetTodaysActionAsync(Guid.Empty);

        // Assert — should use DB action, not call AI
        Assert.NotNull(result);
        Assert.Equal("Existing AI headline from DB", result.Headline);
        await _aiEngine.DidNotReceive().GenerateActionAsync(
            Arg.Any<Cause>(), Arg.Any<ActionType>(), Arg.Any<UserProfile?>(), Arg.Any<string>());
    }

    [Fact]
    public async Task GetTodaysAction_DoesNotCreateDuplicateActions_WhenDbActionExists()
    {
        // Arrange
        SetupNoSkips();
        var cause = CreateTestCause();
        var existingAction = new DailyAction
        {
            Id = Guid.NewGuid(), CauseId = cause.Id, Cause = cause,
            Type = ActionType.Donate, Headline = "Existing"
        };

        _causeRepo.GetBestCauseAsync(
            Arg.Any<IEnumerable<Guid>>(), Arg.Any<IEnumerable<CauseCategory>>(),
            Arg.Any<decimal>(), Arg.Any<CauseCategory?>())
            .Returns(cause);

        _contentCache.GetSummaryAsync(cause.Id, "en").Returns((string?)null);
        _contentCache.GetDailyActionAsync(cause.Id, "en").Returns((CachedDailyAction?)null);
        _contentCache.GetActionOfTheDayAsync("en").Returns((CachedDailyAction?)null);
        _causeRepo.GetLatestActionByCauseIdAsync(cause.Id, "en").Returns(existingAction);

        // Act
        await _sut.GetTodaysActionAsync(Guid.Empty);

        // Assert — AddActionAsync should NOT be called
        await _causeRepo.DidNotReceive().AddActionAsync(Arg.Any<DailyAction>());
    }

    [Fact]
    public async Task GetTodaysAction_FallbackAction_HasCorrectActionType_ForPetition()
    {
        // Arrange
        SetupNoSkips();
        var cause = CreateTestCause("Save the Forest", "openPetition.de");
        cause.FundingGoal = null;

        _causeRepo.GetBestCauseAsync(
            Arg.Any<IEnumerable<Guid>>(), Arg.Any<IEnumerable<CauseCategory>>(),
            Arg.Any<decimal>(), Arg.Any<CauseCategory?>())
            .Returns(cause);

        _causeRepo.GetLatestActionByCauseIdAsync(cause.Id).Returns((DailyAction?)null);
        _contentCache.GetSummaryAsync(cause.Id, "en").Returns((string?)null);
        _contentCache.GetDailyActionAsync(cause.Id, "en").Returns((CachedDailyAction?)null);
        _contentCache.GetActionOfTheDayAsync("en").Returns((CachedDailyAction?)null);

        _aiEngine.GenerateActionAsync(Arg.Any<Cause>(), Arg.Any<ActionType>(), Arg.Any<UserProfile?>(), Arg.Any<string>())
            .ThrowsAsync(new InvalidOperationException("AI failed"));

        // Act
        var result = await _sut.GetTodaysActionAsync(Guid.Empty);

        // Assert — petition source should get Sign action type
        Assert.NotNull(result);
        Assert.Equal(ActionType.Sign, result.Type);
    }

    [Fact]
    public async Task GetTodaysAction_FallbackAction_HasCorrectActionType_ForParliament()
    {
        // Arrange
        SetupNoSkips();
        var cause = CreateTestCause("Upcoming Vote", "Abgeordnetenwatch");
        cause.FundingGoal = null;

        _causeRepo.GetBestCauseAsync(
            Arg.Any<IEnumerable<Guid>>(), Arg.Any<IEnumerable<CauseCategory>>(),
            Arg.Any<decimal>(), Arg.Any<CauseCategory?>())
            .Returns(cause);

        _causeRepo.GetLatestActionByCauseIdAsync(cause.Id).Returns((DailyAction?)null);
        _contentCache.GetSummaryAsync(cause.Id, "en").Returns((string?)null);
        _contentCache.GetDailyActionAsync(cause.Id, "en").Returns((CachedDailyAction?)null);
        _contentCache.GetActionOfTheDayAsync("en").Returns((CachedDailyAction?)null);

        _aiEngine.GenerateActionAsync(Arg.Any<Cause>(), Arg.Any<ActionType>(), Arg.Any<UserProfile?>(), Arg.Any<string>())
            .ThrowsAsync(new InvalidOperationException("AI failed"));

        // Act
        var result = await _sut.GetTodaysActionAsync(Guid.Empty);

        // Assert — parliament source should get Write action type
        Assert.NotNull(result);
        Assert.Equal(ActionType.Write, result.Type);
    }
}
