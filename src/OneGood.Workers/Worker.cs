using OneGood.Core.Interfaces;
using OneGood.Infrastructure.Services;

namespace OneGood.Workers;

/// <summary>
/// Background worker that refreshes causes from external APIs every 6 hours,
/// classifies new causes with AI, pre-warms content caches, and pushes
/// real-time updates to connected UI clients via SignalR.
/// </summary>
public class CauseRefreshWorker : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<CauseRefreshWorker> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromHours(6);

    public CauseRefreshWorker(IServiceProvider services, ILogger<CauseRefreshWorker> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CauseRefreshWorker starting...");

        // Run immediately on startup (handles cold starts and wake-ups on Render)
        await RefreshAndWarmCacheAsync(stoppingToken);
        var lastRun = DateTime.UtcNow;

        // Then run every 6 hours, but also check if we overslept (e.g. Render sleep)
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }

            // If more time has passed than expected (service was suspended),
            // still refresh — this is the whole point of checking elapsed time
            var elapsed = DateTime.UtcNow - lastRun;
            _logger.LogInformation("CauseRefreshWorker woke up after {Elapsed:hh\\:mm\\:ss} (interval: {Interval:hh\\:mm\\:ss})", elapsed, _interval);

            await RefreshAndWarmCacheAsync(stoppingToken);
            lastRun = DateTime.UtcNow;
        }
    }

    private async Task RefreshAndWarmCacheAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _services.CreateScope();
            var causeRepo = scope.ServiceProvider.GetRequiredService<ICauseRepository>();
            var notifier = scope.ServiceProvider.GetService<ICauseNotifier>();

            // 1. Refresh causes from external APIs (includes AI classification of new causes)
            var aggregator = scope.ServiceProvider.GetRequiredService<CauseAggregatorService>();
            await aggregator.RefreshCausesAsync(ct);

            // 2. Notify UI that causes are available + push updated category counts
            var allCauses = await causeRepo.GetAllCausesAsync();
            if (notifier is not null)
            {
                await notifier.NotifyCausesReadyAsync(allCauses.Count);

                var counts = await causeRepo.GetCauseCountsByCategoryAsync();
                var countsDict = counts.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value);
                await notifier.NotifyCategoryCountsUpdatedAsync(countsDict);
            }

            // 3. Clean up cached content for deactivated causes
            var contentCache = scope.ServiceProvider.GetService<IContentCache>();
            if (contentCache is not null)
            {
                var activeCauseIds = allCauses.Select(c => c.Id);
                await contentCache.CleanupStaleCausesAsync(activeCauseIds);
            }

            // 4. Pre-warm caches for supported languages
            var actionEngine = scope.ServiceProvider.GetRequiredService<IActionEngine>();

            if (actionEngine is ActionEngine engine)
            {
                _logger.LogInformation("=== PRE-WARMING CACHE ===");

                await engine.WarmAllCausesCacheAsync("en", ct);
                await engine.WarmAllCausesCacheAsync("de", ct);

                await engine.WarmActionOfTheDayCacheAsync("en");
                await engine.WarmActionOfTheDayCacheAsync("de");

                _logger.LogInformation("=== CACHE WARMING COMPLETE ===");
            }

            // 5. Push final category counts (may have changed after cache warming)
            if (notifier is not null)
            {
                var finalCounts = await causeRepo.GetCauseCountsByCategoryAsync();
                var finalCountsDict = finalCounts.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value);
                await notifier.NotifyCategoryCountsUpdatedAsync(finalCountsDict);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh causes or warm cache");
        }
    }
}
