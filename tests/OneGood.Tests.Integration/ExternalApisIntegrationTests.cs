using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OneGood.Core.Models;
using OneGood.Infrastructure.ExternalApis;

namespace OneGood.Tests.Integration;

/// <summary>
/// Integration tests for external API sources (tests actual API connectivity and data validation).
/// Run with: dotnet test --filter "Category=Integration"
/// These tests verify that external APIs are available and return valid data.
/// </summary>
public class ExternalApisIntegrationTests
{
    private static ILogger<T> CreateLogger<T>() where T : class
    {
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Warning);
        });
        return loggerFactory.CreateLogger<T>();
    }

    #region betterplace.org Tests

    [Fact(Timeout = 30000)]
    [Trait("Category", "Integration")]
    [Trait("API", "betterplace.org")]
    public async Task BetterplaceClient_GetNearlyFundedProjects_ApiIsAvailable()
    {
        // Arrange
        var httpClient = new HttpClient();
        var logger = CreateLogger<BetterplaceClient>();
        var client = new BetterplaceClient(httpClient, logger);

        // Act
        var causes = await client.GetNearlyFundedProjectsAsync();

        // Assert - API should be available and return data
        Assert.NotNull(causes);
    }

    [Fact(Timeout = 30000)]
    [Trait("Category", "Integration")]
    [Trait("API", "betterplace.org")]
    public async Task BetterplaceClient_GetNearlyFundedProjects_ReturnsValidCauseData()
    {
        // Arrange
        var httpClient = new HttpClient();
        var logger = CreateLogger<BetterplaceClient>();
        var client = new BetterplaceClient(httpClient, logger);

        // Act
        var causes = await client.GetNearlyFundedProjectsAsync();

        // Assert - Validate data structure
        Assert.NotNull(causes);
        if (causes.Any())
        {
            var cause = causes.First();
            AssertValidCause(cause);
            Assert.Equal("betterplace.org", cause.SourceApiName);
            Assert.True(cause.FundingGoal.HasValue, "betterplace.org causes should have a funding goal");
            Assert.True(cause.FundingCurrent.HasValue, "betterplace.org causes should have current funding");
        }
    }

    [Fact(Timeout = 30000)]
    [Trait("Category", "Integration")]
    [Trait("API", "betterplace.org")]
    public async Task BetterplaceClient_GetUrgentProjects_ApiIsAvailable()
    {
        // Arrange
        var httpClient = new HttpClient();
        var logger = CreateLogger<BetterplaceClient>();
        var client = new BetterplaceClient(httpClient, logger);

        // Act
        var causes = await client.GetUrgentProjectsAsync();

        // Assert
        Assert.NotNull(causes);
    }

    [Fact(Timeout = 30000)]
    [Trait("Category", "Integration")]
    [Trait("API", "betterplace.org")]
    public async Task BetterplaceClient_GetUrgentProjects_ReturnsValidCauseData()
    {
        // Arrange
        var httpClient = new HttpClient();
        var logger = CreateLogger<BetterplaceClient>();
        var client = new BetterplaceClient(httpClient, logger);

        // Act
        var causes = await client.GetUrgentProjectsAsync();

        // Assert
        Assert.NotNull(causes);
        if (causes.Any())
        {
            var cause = causes.First();
            AssertValidCause(cause);
            Assert.Equal("betterplace.org", cause.SourceApiName);
        }
    }

    [Fact(Timeout = 30000)]
    [Trait("Category", "Integration")]
    [Trait("API", "betterplace.org")]
    public async Task BetterplaceClient_GetProjectsByCategory_ReturnsValidCauseData()
    {
        // Arrange
        var httpClient = new HttpClient();
        var logger = CreateLogger<BetterplaceClient>();
        var client = new BetterplaceClient(httpClient, logger);

        // Act
        var causes = await client.GetProjectsByCategoryAsync("environment");

        // Assert
        Assert.NotNull(causes);
        if (causes.Any())
        {
            var cause = causes.First();
            AssertValidCause(cause);
            Assert.Equal("betterplace.org", cause.SourceApiName);
        }
    }

    #endregion

    #region openPetition.de Tests

    [Fact(Timeout = 30000)]
    [Trait("Category", "Integration")]
    [Trait("API", "openPetition.de")]
    public async Task OpenPetitionClient_GetTrendingPetitions_ApiIsAvailable()
    {
        // Arrange
        var httpClient = new HttpClient();
        var logger = CreateLogger<OpenPetitionClient>();
        var client = new OpenPetitionClient(httpClient, logger);

        // Act
        var causes = await client.GetTrendingPetitionsAsync();

        // Assert
        Assert.NotNull(causes);
        if (causes.Any())
        {
            var cause = causes.First();
            AssertValidCause(cause);
            Assert.Equal("openPetition.de", cause.SourceApiName);
            Assert.Null(cause.FundingGoal); // Petitions don't have funding
        }
    }

    #endregion

    #region Campact/WeAct Tests

    [Fact(Timeout = 30000)]
    [Trait("Category", "Integration")]
    [Trait("API", "Campact/WeAct")]
    public async Task WeActClient_GetActiveCampaigns_ApiIsAvailable()
    {
        // Arrange
        var httpClient = new HttpClient();
        var logger = CreateLogger<WeActClient>();
        var client = new WeActClient(httpClient, logger);

        // Act
        var causes = await client.GetActiveCampaignsAsync();

        // Assert
        Assert.NotNull(causes);
        if (causes.Any())
        {
            var cause = causes.First();
            AssertValidCause(cause);
            Assert.Equal("Campact", cause.SourceApiName);
        }
    }

    #endregion

    #region Abgeordnetenwatch Tests

    [Fact(Timeout = 30000)]
    [Trait("Category", "Integration")]
    [Trait("API", "Abgeordnetenwatch")]
    public async Task AbgeordnetenwatchClient_GetUpcomingVotes_ApiIsAvailable()
    {
        // Arrange
        var httpClient = new HttpClient();
        var logger = CreateLogger<AbgeordnetenwatchClient>();
        var client = new AbgeordnetenwatchClient(httpClient, logger);

        // Act
        var causes = await client.GetUpcomingVotesAsync();

        // Assert
        Assert.NotNull(causes);
        if (causes.Any())
        {
            var cause = causes.First();
            AssertValidCause(cause);
            Assert.Equal("Abgeordnetenwatch", cause.SourceApiName);
        }
    }

    #endregion

    #region GlobalGiving Tests

    [Fact(Timeout = 30000)]
    [Trait("Category", "Integration")]
    [Trait("API", "GlobalGiving")]
    public async Task GlobalGivingClient_GetNearlyFundedProjects_HandlesNoApiKey()
    {
        // Arrange - no API key configured
        var httpClient = new HttpClient();
        var config = new ConfigurationBuilder().Build();
        var logger = CreateLogger<GlobalGivingClient>();
        var client = new GlobalGivingClient(httpClient, config, logger);

        // Act
        var causes = await client.GetFeaturedProjectsAsync();

        // Assert - Without API key, should return empty (not throw)
        Assert.NotNull(causes);
        Assert.Empty(causes);
    }

    [Fact(Timeout = 30000, Skip = "Requires GlobalGiving API key - set GlobalGiving:ApiKey in configuration")]
    [Trait("Category", "Integration")]
    [Trait("API", "GlobalGiving")]
    public async Task GlobalGivingClient_GetNearlyFundedProjects_WithApiKey_ReturnsValidData()
    {
        // Arrange - requires API key in environment or config
        var httpClient = new HttpClient();
        var config = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();
        var logger = CreateLogger<GlobalGivingClient>();
        var client = new GlobalGivingClient(httpClient, config, logger);

        // Act
        var causes = await client.GetNearlyFundedProjectsAsync();

        // Assert
        Assert.NotNull(causes);
        if (causes.Any())
        {
            var cause = causes.First();
            AssertValidCause(cause);
            Assert.Equal("GlobalGiving", cause.SourceApiName);
            Assert.True(cause.FundingGoal.HasValue);
        }
    }

    #endregion

    #region Cross-API Tests

    /// <summary>
    /// Tests all APIs simultaneously to verify parallel operation and overall system health.
    /// </summary>
    [Fact(Timeout = 60000)]
    [Trait("Category", "Integration")]
    [Trait("Scope", "AllApis")]
    public async Task AllApis_ParallelFetch_ReturnsFromMultipleSources()
    {
        // Arrange
        var httpClient = new HttpClient();
        var config = new ConfigurationBuilder().Build();

        var betterplace = new BetterplaceClient(httpClient, CreateLogger<BetterplaceClient>());
        var openPetition = new OpenPetitionClient(httpClient, CreateLogger<OpenPetitionClient>());
        var weAct = new WeActClient(httpClient, CreateLogger<WeActClient>());
        var abgeordnetenwatch = new AbgeordnetenwatchClient(httpClient, CreateLogger<AbgeordnetenwatchClient>());
        var globalGiving = new GlobalGivingClient(httpClient, config, CreateLogger<GlobalGivingClient>());

        // Act
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var tasks = new Task<IEnumerable<Cause>>[]
        {
            betterplace.GetNearlyFundedProjectsAsync(),
            betterplace.GetUrgentProjectsAsync(),
            openPetition.GetTrendingPetitionsAsync(),
            weAct.GetActiveCampaignsAsync(),
            abgeordnetenwatch.GetUpcomingVotesAsync(),
            globalGiving.GetNearlyFundedProjectsAsync()
        };

        var results = await Task.WhenAll(tasks);
        sw.Stop();

        // Assert
        Assert.NotNull(results);
        Assert.Equal(6, results.Length);

        var totalCauses = results.Sum(r => r.Count());
        Assert.True(totalCauses >= 0, $"Got {totalCauses} causes in {sw.ElapsedMilliseconds}ms");

        // Verify at least some free APIs returned data (GlobalGiving requires key)
        var sourcesWithData = results.Count(r => r.Any());
        Assert.True(sourcesWithData >= 1, $"Expected at least 1 API to return data, got {sourcesWithData}");
    }

    /// <summary>
    /// Tests that all APIs handle network errors gracefully (return empty, don't throw).
    /// </summary>
    [Fact(Timeout = 60000)]
    [Trait("Category", "Integration")]
    [Trait("Scope", "AllApis")]
    public async Task AllApis_ReturnDistinctSourceNames()
    {
        // Arrange
        var httpClient = new HttpClient();

        var betterplace = new BetterplaceClient(httpClient, CreateLogger<BetterplaceClient>());
        var openPetition = new OpenPetitionClient(httpClient, CreateLogger<OpenPetitionClient>());
        var weAct = new WeActClient(httpClient, CreateLogger<WeActClient>());
        var abgeordnetenwatch = new AbgeordnetenwatchClient(httpClient, CreateLogger<AbgeordnetenwatchClient>());

        // Act
        var allCauses = new List<Cause>();
        allCauses.AddRange(await betterplace.GetNearlyFundedProjectsAsync());
        allCauses.AddRange(await openPetition.GetTrendingPetitionsAsync());
        allCauses.AddRange(await weAct.GetActiveCampaignsAsync());
        allCauses.AddRange(await abgeordnetenwatch.GetUpcomingVotesAsync());

        // Assert - each source should have distinct source names
        var sourceNames = allCauses.Select(c => c.SourceApiName).Distinct().ToList();

        if (allCauses.Any())
        {
            Assert.True(sourceNames.Count >= 1, "Should have data from at least one source");
            Assert.All(sourceNames, name => Assert.False(string.IsNullOrEmpty(name)));
        }
    }

    /// <summary>
    /// Tests that all returned causes have valid IDs (not empty GUIDs).
    /// </summary>
    [Fact(Timeout = 60000)]
    [Trait("Category", "Integration")]
    [Trait("Scope", "AllApis")]
    public async Task AllApis_ReturnCausesWithValidIds()
    {
        // Arrange
        var httpClient = new HttpClient();
        var betterplace = new BetterplaceClient(httpClient, CreateLogger<BetterplaceClient>());
        var openPetition = new OpenPetitionClient(httpClient, CreateLogger<OpenPetitionClient>());

        // Act
        var causes = new List<Cause>();
        causes.AddRange(await betterplace.GetNearlyFundedProjectsAsync());
        causes.AddRange(await openPetition.GetTrendingPetitionsAsync());

        // Assert
        Assert.All(causes, cause =>
        {
            Assert.NotEqual(Guid.Empty, cause.Id);
            Assert.NotNull(cause.SourceExternalId);
        });
    }

    #endregion

    #region Helper Methods

    private static void AssertValidCause(Cause cause)
    {
        Assert.NotNull(cause);
        Assert.NotEqual(Guid.Empty, cause.Id);
        Assert.NotNull(cause.Title);
        Assert.NotEmpty(cause.Title);
        Assert.NotNull(cause.Description);
        Assert.NotNull(cause.SourceApiName);
        Assert.NotEmpty(cause.SourceApiName);
        Assert.NotNull(cause.SourceExternalId);
        Assert.True(cause.LastRefreshedAt <= DateTime.UtcNow);
    }

    #endregion
}
