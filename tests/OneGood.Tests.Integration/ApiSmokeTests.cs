using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using OneGood.Infrastructure.ExternalApis;

namespace OneGood.Tests.Integration;

/// <summary>
/// Smoke tests to verify basic API connectivity.
/// These are quick tests that can run as part of CI/CD pipelines.
/// </summary>
public class ApiSmokeTests
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

    [Fact(Timeout = 15000)]
    [Trait("Category", "Smoke")]
    public async Task BetterplaceApi_IsReachable()
    {
        // Arrange
        var httpClient = new HttpClient();
        var logger = CreateLogger<BetterplaceClient>();
        var client = new BetterplaceClient(httpClient, logger);

        // Act & Assert - should not throw
        var result = await client.GetNearlyFundedProjectsAsync();
        Assert.NotNull(result);
    }

    [Fact(Timeout = 15000)]
    [Trait("Category", "Smoke")]
    public async Task OpenPetitionApi_IsReachable()
    {
        // Arrange
        var httpClient = new HttpClient();
        var logger = CreateLogger<OpenPetitionClient>();
        var client = new OpenPetitionClient(httpClient, logger);

        // Act & Assert - should not throw
        var result = await client.GetTrendingPetitionsAsync();
        Assert.NotNull(result);
    }

    [Fact(Timeout = 15000)]
    [Trait("Category", "Smoke")]
    public async Task CampactApi_IsReachable()
    {
        // Arrange
        var httpClient = new HttpClient();
        var logger = CreateLogger<WeActClient>();
        var client = new WeActClient(httpClient, logger);

        // Act & Assert - should not throw
        var result = await client.GetActiveCampaignsAsync();
        Assert.NotNull(result);
    }

    [Fact(Timeout = 15000)]
    [Trait("Category", "Smoke")]
    public async Task AbgeordnetenwatchApi_IsReachable()
    {
        // Arrange
        var httpClient = new HttpClient();
        var logger = CreateLogger<AbgeordnetenwatchClient>();
        var client = new AbgeordnetenwatchClient(httpClient, logger);

        // Act & Assert - should not throw
        var result = await client.GetUpcomingVotesAsync();
        Assert.NotNull(result);
    }

    [Fact(Timeout = 30000)]
    [Trait("Category", "Smoke")]
    public async Task GeminiApi_WithGemmaModel_ReturnsValidResponse()
    {
        // Arrange - test the Gemma 3 27B model via Gemini API
        var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta/");

        // Load API key from Api project's appsettings - find project root first
        var currentDir = Directory.GetCurrentDirectory();
        var projectRoot = currentDir;

        // Navigate up to find the solution root (contains src folder)
        while (!Directory.Exists(Path.Combine(projectRoot, "src")) && projectRoot != Path.GetPathRoot(projectRoot))
        {
            projectRoot = Directory.GetParent(projectRoot)?.FullName ?? projectRoot;
        }

        var apiConfigPath = Path.Combine(projectRoot, "src", "OneGood.Api", "appsettings.json");

        if (!File.Exists(apiConfigPath))
        {
            Assert.Fail($"Config file not found at {apiConfigPath} (searched from {currentDir})");
            return;
        }

        var configJson = await File.ReadAllTextAsync(apiConfigPath);
        var config = System.Text.Json.JsonDocument.Parse(configJson);
        var aiSection = config.RootElement.GetProperty("AI");
        var geminiSection = aiSection.GetProperty("Gemini");
        var apiKey = geminiSection.GetProperty("ApiKey").GetString();
        var model = geminiSection.GetProperty("Model").GetString() ?? "gemma-3-27b-it";

        if (string.IsNullOrEmpty(apiKey))
        {
            Assert.Fail("Gemini API key not configured in appsettings.json");
            return;
        }

        // Build request matching GeminiAiService format
        var request = new
        {
            contents = new[]
            {
                new { role = "user", parts = new[] { new { text = "Say 'Hello' in one word only." } } }
            },
            generationConfig = new { maxOutputTokens = 50 }
        };

        var url = $"models/{model}:generateContent?key={apiKey}";

        // Act
        var response = await httpClient.PostAsJsonAsync(url, request);
        var responseContent = await response.Content.ReadAsStringAsync();

        // Assert
        if (!response.IsSuccessStatusCode)
        {
            Assert.Fail($"Gemini API returned {response.StatusCode}: {responseContent}");
        }

        Assert.True(response.IsSuccessStatusCode, $"Expected success but got {response.StatusCode}: {responseContent}");
        Assert.Contains("candidates", responseContent); // Valid Gemini response structure
    }
}
