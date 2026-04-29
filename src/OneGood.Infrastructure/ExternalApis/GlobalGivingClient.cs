using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OneGood.Core.Enums;
using OneGood.Core.Models;

namespace OneGood.Infrastructure.ExternalApis;

/// <summary>
/// Client for GlobalGiving API - provides real charitable projects worldwide.
/// API docs: https://www.globalgiving.org/api/
/// Free API key: https://www.globalgiving.org/api/register/
/// </summary>
public class GlobalGivingClient
{
    private readonly HttpClient _http;
    private readonly ILogger<GlobalGivingClient> _logger;
    private readonly string? _apiKey;

    public GlobalGivingClient(
        HttpClient http,
        IConfiguration config,
        ILogger<GlobalGivingClient> logger)
    {
        _http = http;
        _logger = logger;
        _apiKey = config["GlobalGiving:ApiKey"];

        _http.BaseAddress = new Uri("https://api.globalgiving.org/api/public/");
        _http.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    /// <summary>
    /// Gets projects that are close to their funding goal (high leverage).
    /// These are the most impactful for OneGood - small donations complete projects.
    /// </summary>
    public async Task<IEnumerable<Cause>> GetNearlyFundedProjectsAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogWarning("GlobalGiving API key not configured. Get one at https://www.globalgiving.org/api/");
            return [];
        }

        try
        {
            // Get active projects, sorted by funding progress
            var url = $"projectservice/all/projects/active?api_key={_apiKey}";
            var response = await _http.GetFromJsonAsync<GlobalGivingResponse>(url, ct);

            if (response?.Projects?.Project is null)
                return [];

            // Filter to projects that are 70-99% funded (high leverage!)
            var nearlyFunded = response.Projects.Project
                .Where(p => p.Funding > 0.70m && p.Funding < 1.0m)
                .Where(p => p.RemainingFunding > 0 && p.RemainingFunding < 1000) // Small gap
                .OrderByDescending(p => p.Funding) // Closest to goal first
                .Take(20)
                .Select(MapToCause)
                .ToList();

            _logger.LogInformation("Fetched {Count} nearly-funded projects from GlobalGiving", nearlyFunded.Count);
            return nearlyFunded;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch GlobalGiving projects");
            return [];
        }
    }

    /// <summary>
    /// Gets featured/urgent projects highlighted by GlobalGiving.
    /// </summary>
    public async Task<IEnumerable<Cause>> GetFeaturedProjectsAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_apiKey))
            return [];

        try
        {
            var url = $"projectservice/featured/projects?api_key={_apiKey}";
            var response = await _http.GetFromJsonAsync<GlobalGivingResponse>(url, ct);

            return response?.Projects?.Project?
                .Take(10)
                .Select(MapToCause) ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch featured GlobalGiving projects");
            return [];
        }
    }

    private Cause MapToCause(GlobalGivingProject p) => new()
    {
        Id = Guid.NewGuid(),
        Title = p.Title ?? "Untitled Project",
        Description = p.Summary ?? p.Title ?? "",
        OrganisationName = p.Organization?.Name ?? "Unknown",
        OrganisationUrl = p.ProjectLink ?? "",
        ImageUrl = p.ImageLink,
        Category = CauseCategory.HumanRights, // Default — reclassified by AI in CauseAggregatorService
        FundingGoal = p.Goal,
        FundingCurrent = p.Goal - p.RemainingFunding,
        Deadline = null, // GlobalGiving doesn't always have deadlines
        // Initial scores - will be refined by AI
        UrgencyScore = CalculateUrgency(p),
        LeverageScore = CalculateLeverage(p),
        ActionsToTippingPoint = (int)Math.Ceiling(p.RemainingFunding / 5m), // Assume €5 average
        SourceApiName = "GlobalGiving",
        SourceExternalId = p.Id.ToString(),
        IsActive = p.Active,
        LastRefreshedAt = DateTime.UtcNow
    };

    private static double CalculateUrgency(GlobalGivingProject p)
    {
        // Higher urgency for projects closer to goal
        var fundingProgress = (double)p.Funding; // 0.0 to 1.0
        return Math.Min(100, fundingProgress * 100 + 20);
    }

    private static double CalculateLeverage(GlobalGivingProject p)
    {
        // Higher leverage when remaining amount is small
        if (p.RemainingFunding <= 0) return 0;
        if (p.RemainingFunding < 100) return 95;
        if (p.RemainingFunding < 500) return 85;
        if (p.RemainingFunding < 1000) return 70;
        return 50;
    }
}

// GlobalGiving API response models
public class GlobalGivingResponse
{
    [JsonPropertyName("projects")]
    public GlobalGivingProjects? Projects { get; set; }
}

public class GlobalGivingProjects
{
    [JsonPropertyName("project")]
    public List<GlobalGivingProject>? Project { get; set; }
}

public class GlobalGivingProject
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("themeName")]
    public string? ThemeName { get; set; }

    [JsonPropertyName("goal")]
    public decimal Goal { get; set; }

    [JsonPropertyName("funding")]
    public decimal Funding { get; set; } // Progress as decimal (0.85 = 85%)

    [JsonPropertyName("remaining")]
    public decimal RemainingFunding { get; set; }

    [JsonPropertyName("projectLink")]
    public string? ProjectLink { get; set; }

    [JsonPropertyName("imageLink")]
    public string? ImageLink { get; set; }

    [JsonPropertyName("active")]
    public bool Active { get; set; }

    [JsonPropertyName("organization")]
    public GlobalGivingOrganization? Organization { get; set; }
}

public class GlobalGivingOrganization
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}
