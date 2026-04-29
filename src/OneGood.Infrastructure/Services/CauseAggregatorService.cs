using Microsoft.Extensions.Logging;
using OneGood.Core.Classification;
using OneGood.Core.Interfaces;
using OneGood.Core.Models;
using OneGood.Infrastructure.ExternalApis;

namespace OneGood.Infrastructure.Services;

/// <summary>
/// Aggregates causes from multiple external APIs and scores them with AI.
/// Called by the background worker every 6 hours.
/// 
/// Data Sources (ALL FREE - no API keys required!):
/// - betterplace.org (German donations)
/// - openPetition.de (German petitions) 
/// - Campact/WeAct (German activism)
/// - Abgeordnetenwatch (German parliament - write to MPs)
/// - GlobalGiving (international donations - requires API key)
/// </summary>
public class CauseAggregatorService
{
    private readonly BetterplaceClient _betterplace;
    private readonly OpenPetitionClient _openPetition;
    private readonly WeActClient _weAct;
    private readonly GlobalGivingClient _globalGiving;
    private readonly AbgeordnetenwatchClient _abgeordnetenwatch;
    private readonly ICauseClassifier _classifier;
    private readonly IAiEngine _aiEngine;
    private readonly ICauseRepository _causeRepo;
    private readonly ILogger<CauseAggregatorService> _logger;

    public CauseAggregatorService(
        BetterplaceClient betterplace,
        OpenPetitionClient openPetition,
        WeActClient weAct,
        GlobalGivingClient globalGiving,
        AbgeordnetenwatchClient abgeordnetenwatch,
        ICauseClassifier classifier,
        IAiEngine aiEngine,
        ICauseRepository causeRepo,
        ILogger<CauseAggregatorService> logger)
    {
        _betterplace = betterplace;
        _openPetition = openPetition;
        _weAct = weAct;
        _globalGiving = globalGiving;
        _abgeordnetenwatch = abgeordnetenwatch;
        _classifier = classifier;
        _aiEngine = aiEngine;
        _causeRepo = causeRepo;
        _logger = logger;
    }

    /// <summary>
    /// Refreshes all causes from external APIs.
    /// </summary>
    public async Task RefreshCausesAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("=== CAUSE AGGREGATION STARTING ===");
        _logger.LogInformation("Fetching from 7 external API sources in parallel...");

        // Fetch from all sources in parallel, tracking which source each result came from
        var tasks = new List<(string Source, Task<IEnumerable<Core.Models.Cause>> Task)>
        {
            // === DONATIONS (Donate action) ===
            ("betterplace.org - Nearly Funded", _betterplace.GetNearlyFundedProjectsAsync(ct)),
            ("betterplace.org - Urgent", _betterplace.GetUrgentProjectsAsync(ct)),
            ("betterplace.org - Community Events", _betterplace.GetTrendingFundraisingEventsAsync(ct)),
            ("GlobalGiving", _globalGiving.GetNearlyFundedProjectsAsync(ct)),

            // === PETITIONS (Sign action) ===
            ("openPetition.de", _openPetition.GetTrendingPetitionsAsync(ct)),
            ("Campact/WeAct", _weAct.GetActiveCampaignsAsync(ct)),

            // === PARLIAMENT (Write action) ===
            ("Abgeordnetenwatch", _abgeordnetenwatch.GetUpcomingVotesAsync(ct))
        };

        await Task.WhenAll(tasks.Select(t => t.Task));

        // Log per-source results
        _logger.LogInformation("--- API Source Breakdown ---");
        foreach (var (source, task) in tasks)
        {
            var count = task.Result.Count();
            _logger.LogInformation("  {Source,-35} → {Count,3} causes", source, count);
        }

        var allCauses = tasks
            .SelectMany(t => t.Task.Result)
            .DistinctBy(c => $"{c.SourceApiName}:{c.SourceExternalId}")
            .ToList();

        _logger.LogInformation("--- Aggregation Summary ---");
        _logger.LogInformation("Total (after dedup): {Count} causes", allCauses.Count);
        _logger.LogInformation("  - Donation causes: {D}", allCauses.Count(c => c.FundingGoal.HasValue));
        _logger.LogInformation("  - Petition causes: {P}", allCauses.Count(c => c.SourceApiName is "openPetition.de" or "Campact"));
        _logger.LogInformation("  - Parliament causes: {W}", allCauses.Count(c => c.SourceApiName == "Abgeordnetenwatch"));

        if (allCauses.Count == 0)
        {
            _logger.LogWarning("❌ NO causes fetched from APIs - this may indicate API failures");
            _logger.LogWarning("   Will attempt to use seed data as fallback");
            return;
        }

        _logger.LogInformation("✅ Successfully fetched {Count} causes", allCauses.Count);

        // Phase 1: Simple scoring (fast, no AI)
        
        
        _logger.LogInformation("Applying simple scoring...");

        foreach (var cause in allCauses)
        {
            // Simple scoring based on funding progress
            if (cause.FundingGoal.HasValue && cause.FundingGoal.Value > 0 && cause.FundingCurrent.HasValue)
            {
                var progress = (double)(cause.FundingCurrent.Value / cause.FundingGoal.Value);
                cause.UrgencyScore = Math.Min(100, progress * 100 + 10);
                cause.LeverageScore = cause.FundingGap switch
                {
                    <= 50 => 98,
                    <= 100 => 95,
                    <= 500 => 85,
                    <= 1000 => 70,
                    _ => 50
                };
            }
            else
            {
                // Petitions/parliament - default high scores
                cause.UrgencyScore = 80;
                cause.LeverageScore = 85;
            }

            // Use truncated description as summary - AI summary generated on-demand
            cause.Summary = cause.Description.Length > 150
                ? cause.Description[..147] + "..."
                : cause.Description;
        }

        // === Phase 2: AI-powered classification ===
        // Only classify causes that are NEW (not already in the DB with a saved category).
        // This avoids expensive sequential AI calls (~3-5s each) on every startup.
        var existingCauses = await _causeRepo.GetAllCausesAsync();
        var existingKeys = existingCauses
            .GroupBy(c => $"{c.SourceApiName}:{c.SourceExternalId}")
            .ToDictionary(g => g.Key, g => g.First().Category);

        var newCauses = allCauses
            .Where(c => !existingKeys.ContainsKey($"{c.SourceApiName}:{c.SourceExternalId}"))
            .ToList();

        // Reuse stored categories for known causes
        foreach (var cause in allCauses.Except(newCauses))
        {
            var key = $"{cause.SourceApiName}:{cause.SourceExternalId}";
            if (existingKeys.TryGetValue(key, out var storedCategory))
            {
                cause.Category = storedCategory;
            }
        }

        _logger.LogInformation(
            "Classifying {New} NEW causes with AI (skipping {Existing} already classified)...",
            newCauses.Count, allCauses.Count - newCauses.Count);
        var reclassified = 0;

        foreach (var cause in newCauses)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                var oldCategory = cause.Category;
                cause.Category = await _classifier.ClassifyAsync(
                    cause.Title, cause.Description, cancellationToken: ct);

                if (cause.Category != oldCategory)
                {
                    reclassified++;
                    _logger.LogDebug(
                        "Classified '{Title}': {Old} -> {New}",
                        cause.Title, oldCategory, cause.Category);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI classification failed for '{Title}', keeping default category", cause.Title);
            }
        }

        _logger.LogInformation(
            "AI classification complete: {Reclassified}/{New} new causes classified",
            reclassified, newCauses.Count);

        // Log category distribution
        var categoryGroups = allCauses
            .GroupBy(c => c.Category)
            .OrderByDescending(g => g.Count())
            .Select(g => $"{g.Key}: {g.Count()}");
        _logger.LogInformation("Category distribution: {Categories}", string.Join(", ", categoryGroups));

        // Save to database
        await _causeRepo.UpsertBatchAsync(allCauses);
        _logger.LogInformation("✅ Cause aggregation complete - {Count} causes saved to database", allCauses.Count);

        // Deactivate causes that were not returned by any API in this refresh cycle
        var refreshThreshold = DateTime.UtcNow.AddMinutes(-5);
        var deactivated = await _causeRepo.DeactivateStaleCausesAsync(refreshThreshold);
        if (deactivated > 0)
        {
            _logger.LogInformation("🗑️ Deactivated {Count} stale causes no longer returned by APIs", deactivated);
        }

        _logger.LogInformation("=== CAUSE AGGREGATION FINISHED ===");
    }

    /// <summary>
    /// Process a single cause with AI (can be called on-demand or in background).
    /// </summary>
    public async Task ProcessCauseWithAiAsync(Cause cause, CancellationToken ct = default)
    {
        try
        {
            var scored = await _aiEngine.ScoreCauseUrgencyAsync(cause);
            cause.UrgencyScore = scored.UrgencyScore;
            cause.LeverageScore = scored.LeverageScore;
            cause.ActionsToTippingPoint = scored.ActionsToTippingPoint;

            if (cause.Description.Length > 150)
            {
                cause.Summary = await _aiEngine.SummarizeDescriptionAsync(
                    cause.Title, cause.Description, cause.Category);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI processing failed for {Title}", cause.Title);
        }
    }
}
