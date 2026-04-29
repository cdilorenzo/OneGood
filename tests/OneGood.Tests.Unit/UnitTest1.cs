using OneGood.Core.Enums;
using OneGood.Core.Models;

namespace OneGood.Tests.Unit;

public class CauseModelTests
{
    [Fact]
    public void Cause_FundingGap_ReturnsCorrectValue()
    {
        // Arrange
        var cause = new Cause
        {
            FundingGoal = 1000m,
            FundingCurrent = 750m
        };

        // Act
        var gap = cause.FundingGap;

        // Assert
        Assert.Equal(250m, gap);
    }

    [Fact]
    public void Cause_FundingGap_ReturnsNullWhenGoalIsNull()
    {
        // Arrange
        var cause = new Cause
        {
            FundingGoal = null,
            FundingCurrent = 100m
        };

        // Act
        var gap = cause.FundingGap;

        // Assert
        Assert.Null(gap);
    }

    [Fact]
    public void Cause_FundingGap_ReturnsNullWhenCurrentIsNull()
    {
        // Arrange
        var cause = new Cause
        {
            FundingGoal = 1000m,
            FundingCurrent = null
        };

        // Act
        var gap = cause.FundingGap;

        // Assert
        Assert.Null(gap);
    }

    [Fact]
    public void Cause_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var cause = new Cause();

        // Assert
        Assert.Equal(Guid.Empty, cause.Id);
        Assert.Equal(string.Empty, cause.Title);
        Assert.Equal(string.Empty, cause.Description);
        Assert.Equal(string.Empty, cause.Summary);
        Assert.Equal(string.Empty, cause.OrganisationName);
        Assert.Equal(string.Empty, cause.OrganisationUrl);
        Assert.Equal(string.Empty, cause.SourceApiName);
        Assert.Equal(string.Empty, cause.SourceExternalId);
        Assert.True(cause.IsActive);
        Assert.Empty(cause.Actions);
    }

    [Theory]
    [InlineData(CauseCategory.ClimateAndNature)]
    [InlineData(CauseCategory.Democracy)]
    [InlineData(CauseCategory.Education)]
    [InlineData(CauseCategory.HumanRights)]
    [InlineData(CauseCategory.Refugees)]
    public void Cause_Category_CanBeSetToAnyValue(CauseCategory category)
    {
        // Arrange & Act
        var cause = new Cause { Category = category };

        // Assert
        Assert.Equal(category, cause.Category);
    }

    [Fact]
    public void Cause_WithFullData_HasAllPropertiesSet()
    {
        // Arrange
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var deadline = now.AddDays(30);

        // Act
        var cause = new Cause
        {
            Id = id,
            Title = "Test Cause",
            Summary = "Short summary",
            Description = "Full description of the cause",
            Category = CauseCategory.ClimateAndNature,
            OrganisationName = "Test Org",
            OrganisationUrl = "https://example.com",
            ImageUrl = "https://example.com/image.jpg",
            FundingGoal = 10000m,
            FundingCurrent = 5000m,
            Deadline = deadline,
            UrgencyScore = 85.5,
            LeverageScore = 90.0,
            ActionsToTippingPoint = 100,
            SourceApiName = "TestApi",
            SourceExternalId = "ext-123",
            LastRefreshedAt = now,
            IsActive = true
        };

        // Assert
        Assert.Equal(id, cause.Id);
        Assert.Equal("Test Cause", cause.Title);
        Assert.Equal("Short summary", cause.Summary);
        Assert.Equal("Full description of the cause", cause.Description);
        Assert.Equal(CauseCategory.ClimateAndNature, cause.Category);
        Assert.Equal("Test Org", cause.OrganisationName);
        Assert.Equal("https://example.com", cause.OrganisationUrl);
        Assert.Equal("https://example.com/image.jpg", cause.ImageUrl);
        Assert.Equal(10000m, cause.FundingGoal);
        Assert.Equal(5000m, cause.FundingCurrent);
        Assert.Equal(5000m, cause.FundingGap);
        Assert.Equal(deadline, cause.Deadline);
        Assert.Equal(85.5, cause.UrgencyScore);
        Assert.Equal(90.0, cause.LeverageScore);
        Assert.Equal(100, cause.ActionsToTippingPoint);
        Assert.Equal("TestApi", cause.SourceApiName);
        Assert.Equal("ext-123", cause.SourceExternalId);
        Assert.Equal(now, cause.LastRefreshedAt);
        Assert.True(cause.IsActive);
    }
}

public class DailyActionModelTests
{
    [Fact]
    public void DailyAction_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var action = new DailyAction();

        // Assert
        Assert.Equal(Guid.Empty, action.Id);
        Assert.Equal(Guid.Empty, action.CauseId);
        Assert.Equal(string.Empty, action.Headline);
        Assert.Equal(string.Empty, action.CallToAction);
        Assert.Equal(string.Empty, action.WhyNow);
        Assert.Equal(string.Empty, action.ImpactStatement);
        Assert.Equal(string.Empty, action.WhyYou);
        Assert.Equal(0, action.TimesCompleted);
        Assert.Equal(0, action.TimesShown);
        Assert.Empty(action.UserActions);
        Assert.Null(action.Outcome);
    }

    [Theory]
    [InlineData(ActionType.Donate)]
    [InlineData(ActionType.Share)]
    [InlineData(ActionType.Sign)]
    [InlineData(ActionType.Write)]
    [InlineData(ActionType.Learn)]
    public void DailyAction_Type_CanBeSetToAnyValue(ActionType actionType)
    {
        // Arrange & Act
        var action = new DailyAction { Type = actionType };

        // Assert
        Assert.Equal(actionType, action.Type);
    }
}
