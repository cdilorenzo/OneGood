using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using OneGood.Core.Enums;
using OneGood.Core.Models;

namespace OneGood.Infrastructure.ExternalApis;

/// <summary>
/// Client for betterplace.org API - Germany's largest donation platform.
/// FREE API - no API key required!
/// API docs: https://api.betterplace.org/de/api_v4/documentation
/// </summary>
public class BetterplaceClient
{
    private readonly HttpClient _http;
    private readonly ILogger<BetterplaceClient> _logger;

    public BetterplaceClient(HttpClient http, ILogger<BetterplaceClient> logger)
    {
        _http = http;
        _logger = logger;
        _http.BaseAddress = new Uri("https://api.betterplace.org/de/api_v4/");
    }

    /// <summary>
    /// Gets projects that are close to their funding goal (high leverage).
    /// </summary>
    public async Task<IEnumerable<Cause>> GetNearlyFundedProjectsAsync(CancellationToken ct = default)
    {
        try
        {
            // Get active projects sorted by progress (closest to goal first)
            var url = "projects.json?order=progress_percentage:desc&per_page=50";
            var response = await _http.GetFromJsonAsync<BetterplaceResponse>(url, ct);

            if (response?.Data is null)
            {
                _logger.LogWarning("betterplace.org returned no data");
                return [];
            }

            _logger.LogInformation("betterplace.org raw response: {Count} projects", response.Data.Count);

            // Less strict filter - get projects that have some funding and need more
            var nearlyFunded = response.Data
                .Where(p => p.ProgressPercentage >= 30 && p.ProgressPercentage < 100) // 30-99% funded
                .Where(p => p.OpenAmountInCents > 0) // Still needs funding
                .OrderByDescending(p => p.ProgressPercentage) // Closest to goal first
                .Take(15)
                .Select(MapToCause)
                .ToList();

            _logger.LogInformation("Fetched {Count} projects from betterplace.org (after filter)", nearlyFunded.Count);
            return nearlyFunded;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch betterplace.org projects");
            return [];
        }
    }

    /// <summary>
    /// Gets projects in a specific category.
    /// </summary>
    public async Task<IEnumerable<Cause>> GetProjectsByCategoryAsync(string category, CancellationToken ct = default)
    {
        try
        {
            var url = $"projects.json?order=progress_percentage:desc&category={category}&per_page=20";
            var response = await _http.GetFromJsonAsync<BetterplaceResponse>(url, ct);

            return response?.Data?
                .Where(p => p.ProgressPercentage >= 20 && p.ProgressPercentage < 100)
                .Take(10)
                .Select(MapToCause) ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch betterplace.org projects for category {Category}", category);
            return [];
        }
    }

    /// <summary>
    /// Gets popular projects (most donations).
    /// </summary>
    public async Task<IEnumerable<Cause>> GetUrgentProjectsAsync(CancellationToken ct = default)
    {
        try
        {
            // Most popular projects by donation count
            var url = "projects.json?order=donations_count:desc&per_page=30";
            var response = await _http.GetFromJsonAsync<BetterplaceResponse>(url, ct);

            var projects = response?.Data?
                .Where(p => p.ProgressPercentage >= 20 && p.ProgressPercentage < 100)
                .Take(10)
                .Select(MapToCause)
                .ToList() ?? [];

            _logger.LogInformation("Fetched {Count} popular projects from betterplace.org", projects.Count);
            return projects;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch popular betterplace.org projects");
            return [];
        }
    }

    private Cause MapToCause(BetterplaceProject p)
    {
        // Decode HTML entities from betterplace API (e.g. &amp; → &)
        var title = WebUtility.HtmlDecode(p.Title ?? "Untitled Project");
        var description = WebUtility.HtmlDecode(p.Summary ?? p.Description ?? string.Empty);

        return new Cause
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = description,
            OrganisationName = p.CarrierName ?? "Unknown",
            OrganisationUrl = $"https://www.betterplace.org/de/projects/{p.Id}",
            ImageUrl = null,
            Category = CauseCategory.HumanRights, // Default — reclassified by AI in CauseAggregatorService
            FundingGoal = p.RequestedAmountInCents / 100m,
            FundingCurrent = p.DonatedAmountInCents / 100m,
            Deadline = null,
            UrgencyScore = CalculateUrgency(p),
            LeverageScore = CalculateLeverage(p),
            ActionsToTippingPoint = (int)Math.Ceiling(p.OpenAmountInCents / 500m),
            SourceApiName = "betterplace.org",
            SourceExternalId = p.Id.ToString(),
            IsActive = true,
            LastRefreshedAt = DateTime.UtcNow
        };
    }

    private static double CalculateUrgency(BetterplaceProject p)
    {
        // Higher urgency for projects closer to goal
        return Math.Min(100, p.ProgressPercentage + 10);
    }

    private static double CalculateLeverage(BetterplaceProject p)
    {
        // Higher leverage when remaining amount is small
        var remaining = p.OpenAmountInCents / 100m;
        if (remaining <= 0) return 0;
        if (remaining < 50) return 98;
        if (remaining < 100) return 95;
        if (remaining < 500) return 85;
        if (remaining < 1000) return 70;
        return 50;
    }

    /// <summary>
    /// Gets trending community fundraising events (charity runs, birthday fundraisers, etc.).
    /// Different from org-led projects — these are grassroots, often more time-sensitive.
    /// </summary>
    public async Task<IEnumerable<Cause>> GetTrendingFundraisingEventsAsync(CancellationToken ct = default)
    {
        try
        {
            var url = "fundraising_events.json?order=donations_count:desc&per_page=30";
            var response = await _http.GetFromJsonAsync<BetterplaceFundraisingResponse>(url, ct);

            if (response?.Data is null)
            {
                _logger.LogWarning("betterplace.org fundraising events returned no data");
                return [];
            }

            var events = response.Data
                .Where(e => (e.ProgressPercentage ?? 0) >= 20 && (e.ProgressPercentage ?? 0) < 100)
                .Where(e => (e.OpenAmountInCents ?? 0) > 0)
                .Take(10)
                .Select(MapFundraisingEventToCause)
                .ToList();

            _logger.LogInformation("Fetched {Count} fundraising events from betterplace.org", events.Count);
            return events;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch betterplace.org fundraising events");
            return [];
        }
    }

    private Cause MapFundraisingEventToCause(BetterplaceFundraisingEvent e)
    {
        var title = WebUtility.HtmlDecode(e.Title ?? "Untitled Event");
        var description = WebUtility.HtmlDecode(e.Description ?? string.Empty);

        return new Cause
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = description,
            OrganisationName = e.OrganizerName ?? "Community",
            OrganisationUrl = $"https://www.betterplace.org/de/fundraising-events/{e.Id}",
            ImageUrl = null,
            Category = CauseCategory.HumanRights, // Default — reclassified by AI
            FundingGoal = (e.RequestedAmountInCents ?? 0) / 100m,
            FundingCurrent = (e.DonatedAmountInCents ?? 0) / 100m,
            Deadline = null,
            UrgencyScore = Math.Min(100, (e.ProgressPercentage ?? 0) + 15), // Slightly higher urgency for community events
            LeverageScore = CalculateLeverage(new BetterplaceProject
            {
                OpenAmountInCents = e.OpenAmountInCents ?? 0
            }),
            ActionsToTippingPoint = (int)Math.Ceiling((e.OpenAmountInCents ?? 0) / 500m),
            SourceApiName = "betterplace.org/events",
            SourceExternalId = e.Id.ToString(),
            IsActive = true,
            LastRefreshedAt = DateTime.UtcNow
        };
    }
}

// betterplace.org API response models
public class BetterplaceResponse
{
    [JsonPropertyName("data")]
    public List<BetterplaceProject>? Data { get; set; }
}

public class BetterplaceProject
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("requested_amount_in_cents")]
    public long RequestedAmountInCents { get; set; }

    [JsonPropertyName("donated_amount_in_cents")]
    public long DonatedAmountInCents { get; set; }

    [JsonPropertyName("open_amount_in_cents")]
    public long OpenAmountInCents { get; set; }

    [JsonPropertyName("progress_percentage")]
    public int ProgressPercentage { get; set; }

    [JsonPropertyName("donations_count")]
    public int DonationsCount { get; set; }

    [JsonPropertyName("actively_donatable")]
    public bool ActivelyDonatable { get; set; }

    // Simplified - just get carrier name as string from nested object
    [JsonPropertyName("carrier")]
    public JsonElement? Carrier { get; set; }

    public string? CarrierName => Carrier?.TryGetProperty("name", out var name) == true 
        ? name.GetString() 
        : null;

    // Skip complex nested objects to avoid JSON parsing issues
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

// betterplace.org fundraising events API response models
public class BetterplaceFundraisingResponse
{
    [JsonPropertyName("data")]
    public List<BetterplaceFundraisingEvent>? Data { get; set; }
}

public class BetterplaceFundraisingEvent
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("requested_amount_in_cents")]
    public long? RequestedAmountInCents { get; set; }

    [JsonPropertyName("donated_amount_in_cents")]
    public long? DonatedAmountInCents { get; set; }

    [JsonPropertyName("open_amount_in_cents")]
    public long? OpenAmountInCents { get; set; }

    [JsonPropertyName("progress_percentage")]
    public int? ProgressPercentage { get; set; }

    [JsonPropertyName("donations_count")]
    public int? DonationsCount { get; set; }

    [JsonPropertyName("organizer")]
    public JsonElement? Organizer { get; set; }

    public string? OrganizerName => Organizer?.TryGetProperty("name", out var name) == true
        ? name.GetString()
        : null;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
