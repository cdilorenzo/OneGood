using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using OneGood.Core.Enums;
using OneGood.Core.Models;

namespace OneGood.Infrastructure.ExternalApis;

/// <summary>
/// Client for openPetition.de - German petition platform.
/// FREE - no key required!
/// Uses HTML scraping (RSS feed was deprecated by openPetition.de).
/// </summary>
public class OpenPetitionClient
{
    private readonly HttpClient _http;
    private readonly ILogger<OpenPetitionClient> _logger;

    public OpenPetitionClient(HttpClient http, ILogger<OpenPetitionClient> logger)
    {
        _http = http;
        _logger = logger;
        // Don't set BaseAddress - we'll use full URLs for flexibility
    }

    /// <summary>
    /// Gets trending petitions from openPetition.de via HTML scraping.
    /// (RSS feed was deprecated by openPetition.de)
    /// </summary>
    public async Task<IEnumerable<Cause>> GetTrendingPetitionsAsync(CancellationToken ct = default)
    {
        return await GetFromHtmlScrapingAsync(ct);
    }

    private async Task<IEnumerable<Cause>> GetFromHtmlScrapingAsync(CancellationToken ct)
    {
        try
        {
            // Add a User-Agent to avoid being blocked
            var request = new HttpRequestMessage(HttpMethod.Get, "https://www.openpetition.de/petitionen");
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            request.Headers.Add("Accept", "text/html");
            request.Headers.Add("Accept-Language", "de-DE,de;q=0.9,en;q=0.8");

            var response = await _http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync(ct);

            var causes = ParsePetitionsFromHtml(html);
            _logger.LogInformation("Scraped {Count} petitions from openPetition.de HTML", causes.Count);
            return causes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scrape openPetition.de petitions");
            return [];
        }
    }

    private List<Cause> ParsePetitionsFromHtml(string html)
    {
        var causes = new List<Cause>();

        try
        {
            // Pattern to match petition cards/links
            // Looking for: <a href="/petition/online/PETITION-SLUG" ...>TITLE</a>
            var petitionPattern = new Regex(
                @"<a[^>]*href=""(/petition/online/[^""]+)""[^>]*>([^<]+)</a>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            // Also try to extract from card structures
            var cardPattern = new Regex(
                @"<article[^>]*class=""[^""]*petition[^""]*""[^>]*>.*?<a[^>]*href=""(/petition/online/[^""]+)""[^>]*>.*?<h[23][^>]*>([^<]+)</h[23]>.*?<p[^>]*>([^<]*)</p>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            var matches = petitionPattern.Matches(html);
            var seen = new HashSet<string>();

            foreach (Match match in matches)
            {
                var path = match.Groups[1].Value;
                var title = CleanHtml(match.Groups[2].Value);

                // Skip if we've already seen this petition or if title is too short
                if (string.IsNullOrWhiteSpace(title) || title.Length < 10 || seen.Contains(path))
                    continue;

                seen.Add(path);
                var url = $"https://www.openpetition.de{path}";

                causes.Add(MapToCause(title, "", url));

                if (causes.Count >= 15) break;
            }

            // If we didn't find enough, try a simpler pattern
            if (causes.Count < 5)
            {
                var simplePattern = new Regex(
                    @"href=""(/petition/online/([^""]+))""[^>]*title=""([^""]+)""",
                    RegexOptions.IgnoreCase);

                foreach (Match match in simplePattern.Matches(html))
                {
                    var path = match.Groups[1].Value;
                    var title = CleanHtml(match.Groups[3].Value);

                    if (string.IsNullOrWhiteSpace(title) || title.Length < 10 || seen.Contains(path))
                        continue;

                    seen.Add(path);
                    var url = $"https://www.openpetition.de{path}";

                    causes.Add(MapToCause(title, "", url));

                    if (causes.Count >= 15) break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error parsing openPetition HTML");
        }

        return causes;
    }

    private Cause MapToCause(string title, string description, string link) => new()
    {
        Id = Guid.NewGuid(),
        Title = CleanHtml(title),
        Description = string.IsNullOrEmpty(description) 
            ? $"Eine Petition auf openPetition.de: {CleanHtml(title)}"
            : CleanHtml(description),
        OrganisationName = "openPetition",
        OrganisationUrl = link,
        ImageUrl = null,
        Category = CauseCategory.Democracy, // Default — reclassified by AI in CauseAggregatorService
        FundingGoal = null, // Petitions don't have funding
        FundingCurrent = null,
        Deadline = DateTime.UtcNow.AddDays(30), // Most petitions have ~30 day windows
        UrgencyScore = 80, // Petitions are time-sensitive
        LeverageScore = 85, // Each signature counts
        ActionsToTippingPoint = 100,
        SourceApiName = "openPetition.de",
        SourceExternalId = ExtractIdFromUrl(link),
        IsActive = true,
        LastRefreshedAt = DateTime.UtcNow
    };

    private static string CleanHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return "";
        // Remove HTML tags and decode entities
        var text = Regex.Replace(html, "<.*?>", "");
        text = System.Net.WebUtility.HtmlDecode(text);
        return text.Trim();
    }

    private static string ExtractIdFromUrl(string url)
    {
        var parts = url.Split('/');
        return parts.Length > 0 ? parts[^1] : Guid.NewGuid().ToString();
    }
}

/// <summary>
/// Client for WeAct by Campact - German activism platform.
/// Uses RSS feed (no API key required).
/// </summary>
public class WeActClient
{
    private readonly HttpClient _http;
    private readonly ILogger<WeActClient> _logger;

    public WeActClient(HttpClient http, ILogger<WeActClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    /// <summary>
    /// Gets active campaigns from WeAct/Campact.
    /// </summary>
    public async Task<IEnumerable<Cause>> GetActiveCampaignsAsync(CancellationToken ct = default)
    {
        try
        {
            // Campact blog RSS for campaigns
            var response = await _http.GetStringAsync("https://blog.campact.de/feed/", ct);
            var xml = XDocument.Parse(response);

            var items = xml.Descendants("item")
                .Take(10)
                .Select(item => new
                {
                    Title = item.Element("title")?.Value ?? "",
                    Description = item.Element("description")?.Value ?? "",
                    Link = item.Element("link")?.Value ?? ""
                })
                .Where(x => !string.IsNullOrEmpty(x.Title))
                .Select(x => MapToCause(x.Title, x.Description, x.Link))
                .ToList();

            _logger.LogInformation("Fetched {Count} campaigns from Campact", items.Count);
            return items;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch Campact campaigns");
            return [];
        }
    }

    private Cause MapToCause(string title, string description, string link) => new()
    {
        Id = Guid.NewGuid(),
        Title = CleanHtml(title),
        Description = CleanHtml(description).Length > 500 
            ? CleanHtml(description)[..500] + "..." 
            : CleanHtml(description),
        OrganisationName = "Campact / WeAct",
        OrganisationUrl = link,
        Category = CauseCategory.Democracy, // Default — reclassified by AI in CauseAggregatorService
        FundingGoal = null,
        FundingCurrent = null,
        Deadline = DateTime.UtcNow.AddDays(14),
        UrgencyScore = 85,
        LeverageScore = 80,
        ActionsToTippingPoint = 500,
        SourceApiName = "Campact",
        SourceExternalId = ExtractIdFromUrl(link),
        IsActive = true,
        LastRefreshedAt = DateTime.UtcNow
    };

    private static string CleanHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return "";
        return System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", "").Trim();
    }

    private static string ExtractIdFromUrl(string url)
    {
        var parts = url.Split('/');
        return parts.Length > 0 ? parts[^1] : Guid.NewGuid().ToString();
    }
}

/// <summary>
/// Client for Abgeordnetenwatch.de - German parliament transparency platform.
/// FREE API - no key required!
/// Use for "Write" action type - letters to German MPs.
/// API docs: https://www.abgeordnetenwatch.de/api
/// </summary>
public class AbgeordnetenwatchClient
{
    private readonly HttpClient _http;
    private readonly ILogger<AbgeordnetenwatchClient> _logger;

    public AbgeordnetenwatchClient(HttpClient http, ILogger<AbgeordnetenwatchClient> logger)
    {
        _http = http;
        _logger = logger;
        _http.BaseAddress = new Uri("https://www.abgeordnetenwatch.de/api/v2/");
    }

    /// <summary>
    /// Gets upcoming votes in Bundestag that users could write to their MPs about.
    /// </summary>
    public async Task<IEnumerable<Cause>> GetUpcomingVotesAsync(CancellationToken ct = default)
    {
        try
        {
            // Get recent polls/votes from current legislature
            var url = "polls?range_end=50";
            var response = await _http.GetFromJsonAsync<AbgeordnetenwatchResponse>(url, ct);

            if (response?.Data is null)
                return [];

            var causes = response.Data
                .Where(p => !string.IsNullOrEmpty(p.Label))
                .Take(10)
                .Select(MapToCause)
                .ToList();

            _logger.LogInformation("Fetched {Count} parliamentary topics from Abgeordnetenwatch", causes.Count);
            return causes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch Abgeordnetenwatch data");
            return [];
        }
    }

    private Cause MapToCause(AbgeordnetenwatchPoll p) => new()
    {
        Id = Guid.NewGuid(),
        Title = p.Label ?? "Parliamentary Decision",
        Description = p.FieldIntro ?? $"A vote in the German Bundestag on: {p.Label}",
        OrganisationName = "Deutscher Bundestag",
        OrganisationUrl = !string.IsNullOrEmpty(p.AbgeordnetenwatchUrl) 
            ? p.AbgeordnetenwatchUrl 
            : "https://www.abgeordnetenwatch.de",
        Category = CauseCategory.Democracy, // Default — reclassified by AI in CauseAggregatorService
        FundingGoal = null,
        FundingCurrent = null,
        Deadline = DateTime.UtcNow.AddDays(7),
        UrgencyScore = 85,
        LeverageScore = 90,
        ActionsToTippingPoint = 100,
        SourceApiName = "Abgeordnetenwatch",
        SourceExternalId = p.Id.ToString(),
        IsActive = true,
        LastRefreshedAt = DateTime.UtcNow
    };
}

// Abgeordnetenwatch API models
public class AbgeordnetenwatchResponse
{
    [JsonPropertyName("data")]
    public List<AbgeordnetenwatchPoll>? Data { get; set; }
}

public class AbgeordnetenwatchPoll
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("field_intro")]
    public string? FieldIntro { get; set; }

    [JsonPropertyName("abgeordnetenwatch_url")]
    public string? AbgeordnetenwatchUrl { get; set; }
}
