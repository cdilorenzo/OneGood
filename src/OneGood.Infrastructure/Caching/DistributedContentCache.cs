using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using OneGood.Core.Interfaces;

namespace OneGood.Infrastructure.Caching;

/// <summary>
/// Content cache implementation using IDistributedCache (Redis or in-memory).
/// Caches AI-generated content per cause for instant loading.
/// 
/// Cache key patterns:
/// - content:summary:{causeId}:{lang}     - AI summary for a cause
/// - content:action:{causeId}:{lang}      - Generated action for a cause
/// - content:aotd:{lang}                  - Action of the Day (pre-warmed)
/// - content:causes:active                - List of active cause IDs
/// </summary>
public class DistributedContentCache : IContentCache
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<DistributedContentCache> _logger;

    // Cache keys
    private const string SummaryKeyPrefix = "content:summary:";
    private const string ActionKeyPrefix = "content:action:";
    private const string AotdKeyPrefix = "content:aotd:";

    // Default TTLs
    private static readonly TimeSpan SummaryTtl = TimeSpan.FromDays(30); // Summaries rarely change
    private static readonly TimeSpan ActionTtl = TimeSpan.FromDays(7);   // Actions valid for a week

    public DistributedContentCache(IDistributedCache cache, ILogger<DistributedContentCache> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<string?> GetSummaryAsync(Guid causeId, string language)
    {
        var key = $"{SummaryKeyPrefix}{causeId}:{language}";
        try
        {
            return await _cache.GetStringAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get cached summary for {CauseId}", causeId);
            return null;
        }
    }

    public async Task SetSummaryAsync(Guid causeId, string language, string summary)
    {
        var key = $"{SummaryKeyPrefix}{causeId}:{language}";
        try
        {
            await _cache.SetStringAsync(key, summary, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = SummaryTtl
            });
            _logger.LogDebug("Cached summary for {CauseId} ({Language})", causeId, language);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache summary for {CauseId}", causeId);
        }
    }

    public async Task<CachedDailyAction?> GetDailyActionAsync(Guid causeId, string language)
    {
        var key = $"{ActionKeyPrefix}{causeId}:{language}";
        try
        {
            var json = await _cache.GetStringAsync(key);
            if (json is null) return null;
            return JsonSerializer.Deserialize<CachedDailyAction>(json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get cached action for {CauseId}", causeId);
            return null;
        }
    }

    public async Task SetDailyActionAsync(Guid causeId, string language, CachedDailyAction action)
    {
        var key = $"{ActionKeyPrefix}{causeId}:{language}";
        try
        {
            var json = JsonSerializer.Serialize(action);
            await _cache.SetStringAsync(key, json, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ActionTtl
            });
            _logger.LogDebug("Cached action for {CauseId} ({Language})", causeId, language);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache action for {CauseId}", causeId);
        }
    }

    public async Task<CachedDailyAction?> GetActionOfTheDayAsync(string language)
    {
        var key = $"{AotdKeyPrefix}{language}";
        try
        {
            var json = await _cache.GetStringAsync(key);
            if (json is null) return null;
            return JsonSerializer.Deserialize<CachedDailyAction>(json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get action of the day ({Language})", language);
            return null;
        }
    }

    public async Task SetActionOfTheDayAsync(string language, CachedDailyAction action, TimeSpan ttl)
    {
        var key = $"{AotdKeyPrefix}{language}";
        try
        {
            var json = JsonSerializer.Serialize(action);
            await _cache.SetStringAsync(key, json, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl
            });
            _logger.LogInformation("✅ Cached Action of the Day ({Language}) until {Expiry:HH:mm:ss}", 
                language, DateTime.UtcNow.Add(ttl));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache action of the day ({Language})", language);
        }
    }

    public async Task InvalidateCauseAsync(Guid causeId)
    {
        // Remove all cached content for this cause in all languages
        var languages = new[] { "en", "de" };
        foreach (var lang in languages)
        {
            try
            {
                await _cache.RemoveAsync($"{SummaryKeyPrefix}{causeId}:{lang}");
                await _cache.RemoveAsync($"{ActionKeyPrefix}{causeId}:{lang}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to invalidate cache for {CauseId}", causeId);
            }
        }
        _logger.LogDebug("Invalidated cache for cause {CauseId}", causeId);
    }

    public async Task CleanupStaleCausesAsync(IEnumerable<Guid> activeCauseIds)
    {
        // Note: This is a no-op for distributed cache as we rely on TTL expiration.
        // A more sophisticated implementation could track cached cause IDs and remove stale ones.
        _logger.LogDebug("Stale cause cleanup - relying on TTL expiration");
        await Task.CompletedTask;
    }
}
