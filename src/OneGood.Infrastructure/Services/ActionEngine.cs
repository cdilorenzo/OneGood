using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using OneGood.Core;
using OneGood.Core.Enums;
using OneGood.Core.Interfaces;
using OneGood.Core.Models;

namespace OneGood.Infrastructure.Services;

public class ActionEngine : IActionEngine
{
    private readonly ICauseRepository _causes;
    private readonly IUserRepository _users;
    private readonly IAiEngine _ai;
    private readonly IDistributedCache? _cache;
    private readonly IContentCache? _contentCache;
    private readonly ICauseNotifier? _notifier;
    private readonly ILogger<ActionEngine> _logger;

    public ActionEngine(
        ICauseRepository causes,
        IUserRepository users,
        IAiEngine ai,
        ILogger<ActionEngine> logger,
        IDistributedCache? cache = null,
        IContentCache? contentCache = null,
        ICauseNotifier? notifier = null)
    {
        _causes = causes;
        _users = users;
        _ai = ai;
        _logger = logger;
        _cache = cache;
        _contentCache = contentCache;
        _notifier = notifier;
    }

    public async Task<DailyAction?> GetTodaysActionAsync(Guid userId, string language = "en", string? category = null, Guid? excludeCurrent = null, string? actionType = null)
    {
        var isAnonymous = userId == Guid.Empty;

        // Parse category filter
        CauseCategory? selectedCategory = null;
        if (!string.IsNullOrEmpty(category) && Enum.TryParse<CauseCategory>(category, ignoreCase: true, out var parsed))
        {
            selectedCategory = parsed;
        }

        // Parse action type filter into source names
        var filterSources = ActionTypeMapping.SourcesForType(actionType);
        var excludeSources = ActionTypeMapping.IsShareType(actionType)
            ? ActionTypeMapping.ExcludeSourcesForShare
            : null;

        // Get skipped cause IDs early - needed to check against cached results
        var skippedCauseIds = await GetSkippedCauseIdsAsync(userId);

        // excludeCurrent is a transient exclusion (not persisted) — used by
        // "Show next cause" after opening, so the opened cause stays in
        // the rotation and reappears with the "Opened" indicator later.
        if (excludeCurrent.HasValue && excludeCurrent.Value != Guid.Empty
            && !skippedCauseIds.Contains(excludeCurrent.Value))
        {
            skippedCauseIds.Add(excludeCurrent.Value);
        }

        // For anonymous users with no filters, try pre-warmed cache
        if (isAnonymous && selectedCategory is null && actionType is null && _contentCache is not null)
        {
            var cached = await _contentCache.GetActionOfTheDayAsync(language);
            if (cached is not null && !skippedCauseIds.Contains(cached.CauseId))
            {
                var hydrated = await HydrateCachedAction(cached);
                if (hydrated is not null)
                {
                    _logger.LogDebug("Serving cached Action of the Day ({Language})", language);
                    return hydrated;
                }
                // Hydration failed (cause no longer in DB) — fall through to normal path
            }
        }

        // For logged-in users, check user-specific cache (include category in key)
        if (!isAnonymous && _cache is not null)
        {
            var cacheKey = $"action:today:{userId}:{language}:{category ?? "all"}:{actionType ?? "all"}";
            var cachedJson = await _cache.GetStringAsync(cacheKey);
            if (cachedJson is not null)
            {
                var cachedAction = JsonSerializer.Deserialize<DailyAction>(cachedJson);
                if (cachedAction is not null && !skippedCauseIds.Contains(cachedAction.CauseId))
                {
                    return cachedAction;
                }
            }
        }

        // Get user profile for personalisation
        var profile = !isAnonymous
            ? await _users.GetProfileAsync(userId)
            : null;

        // Find the highest-urgency cause the user hasn't seen recently
        var seenCauseIds = !isAnonymous
            ? await _users.GetRecentCauseIdsAsync(userId, days: 30)
            : [];

        // Try to find a cause that hasn't been skipped yet
        var cause = await _causes.GetBestCauseAsync(
            excludeIds: seenCauseIds.Concat(skippedCauseIds).Distinct(),
            preferredCategories: profile?.PreferredCategories ?? [],
            maxDonationAmount: profile?.MaxDonationPerAction ?? 5.00m,
            filterCategory: selectedCategory,
            filterSources: filterSources,
            excludeSources: excludeSources);

        // If all causes in this selection have been skipped, wrap around:
        // clear the skip list and show causes again
        if (cause is null && skippedCauseIds.Count > 0)
        {
            _logger.LogDebug("All causes skipped, wrapping around");
            await ClearSkippedCauseIdsAsync(userId);

            // Preserve transient excludeCurrent so the cause the user just
            // navigated away from doesn't immediately reappear after wrap-around.
            var transientExcludes = excludeCurrent.HasValue && excludeCurrent.Value != Guid.Empty
                ? new List<Guid> { excludeCurrent.Value }
                : new List<Guid>();

            cause = await _causes.GetBestCauseAsync(
                excludeIds: seenCauseIds.Concat(transientExcludes).Distinct(),
                preferredCategories: profile?.PreferredCategories ?? [],
                maxDonationAmount: profile?.MaxDonationPerAction ?? 5.00m,
                filterCategory: selectedCategory,
                filterSources: filterSources,
                excludeSources: excludeSources);

            // If still null and we were excluding the current cause, drop that
            // exclusion too — better to re-show the same cause than show nothing.
            if (cause is null && transientExcludes.Count > 0)
            {
                cause = await _causes.GetBestCauseAsync(
                    excludeIds: seenCauseIds,
                    preferredCategories: profile?.PreferredCategories ?? [],
                    maxDonationAmount: profile?.MaxDonationPerAction ?? 5.00m,
                    filterCategory: selectedCategory,
                    filterSources: filterSources,
                    excludeSources: excludeSources);
            }
        }

        // Last-resort fallback: if we're about to return nothing but causes
        // exist in the selected category, force-query with ZERO exclusions.
        // This handles edge cases like stale seenCauseIds, deserialization bugs,
        // or any other path that might incorrectly exclude all causes.
        if (cause is null)
        {
            var counts = await _causes.GetCauseCountsByCategoryAsync();
            var hasMatchingCauses = selectedCategory is not null
                ? counts.GetValueOrDefault(selectedCategory.Value) > 0
                : counts.Values.Sum() > 0;

            if (hasMatchingCauses)
            {
                _logger.LogWarning(
                    "Fallback triggered: no cause found but {Count} exist in {Category}. " +
                    "Clearing all exclusions and retrying.",
                    selectedCategory is not null
                        ? counts.GetValueOrDefault(selectedCategory.Value)
                        : counts.Values.Sum(),
                    selectedCategory?.ToString() ?? "All");

                // Nuclear option: clear everything and query with no exclusions
                await ClearSkippedCauseIdsAsync(userId);

                cause = await _causes.GetBestCauseAsync(
                    excludeIds: [],
                    preferredCategories: profile?.PreferredCategories ?? [],
                    maxDonationAmount: profile?.MaxDonationPerAction ?? 5.00m,
                    filterCategory: selectedCategory,
                    filterSources: filterSources,
                    excludeSources: excludeSources);
            }
        }

        if (cause is null)
        {
            _logger.LogWarning("No active causes found");
            return null;
        }

        // Get language-specific summary from cache if available (no on-demand AI generation)
        if (_contentCache is not null)
        {
            var cachedSummary = await _contentCache.GetSummaryAsync(cause.Id, language);
            if (cachedSummary is not null)
            {
                cause.Summary = cachedSummary;
            }
            else
            {
                // Cache miss — try DB translation (survives cache eviction)
                var dbSummary = await _causes.GetTranslatedSummaryAsync(cause.Id, language);
                if (dbSummary is not null)
                {
                    cause.Summary = dbSummary;
                }
            }
        }

        // If summary is still a truncated placeholder, use full description instead
        if (string.IsNullOrEmpty(cause.Summary) || cause.Summary.EndsWith("..."))
        {
            cause.Summary = cause.Description;
        }

        // Try to get cached action for this cause
        DailyAction? action = null;
        var isFromCache = false;
        if (_contentCache is not null)
        {
            var cachedAction = await _contentCache.GetDailyActionAsync(cause.Id, language);
            if (cachedAction is not null)
            {
                _logger.LogDebug("Using cached action for cause {CauseId}", cause.Id);
                action = await HydrateCachedAction(cachedAction);
                isFromCache = true;
            }
        }

        // Check DB for an existing action if not in cache
        if (action is null)
        {
            var existingAction = await _causes.GetLatestActionByCauseIdAsync(cause.Id, language);
            if (existingAction is not null)
            {
                _logger.LogDebug("Using existing DB action for cause {CauseId}", cause.Id);
                existingAction.Cause = cause;
                action = existingAction;
                isFromCache = true;
            }
        }

        // Generate action with AI if not cached
        // NEVER call AI in the request path — use fallback from source data immediately.
        // AI content is generated exclusively by the background worker.
        if (action is null)
        {
            var bestActionType = DetermineActionType(cause, profile);
            action = CreateFallbackAction(cause, bestActionType, language);
            _logger.LogDebug("No pre-generated action for cause {CauseId}, using source data fallback", cause.Id);
        }

        // Save only freshly generated actions (cached ones already exist in DB)
        if (!isFromCache)
        {
            await _causes.AddActionAsync(action);
        }

        // Cache for logged-in users
        if (!isAnonymous && _cache is not null)
        {
            var cacheKey = $"action:today:{userId}:{language}:{category ?? "all"}:{actionType ?? "all"}";
            var ttl = TimeUntilEndOfDay(profile?.UserId.ToString());
            await _cache.SetStringAsync(cacheKey,
                JsonSerializer.Serialize(action),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl });
        }

        return action;
    }

    /// <summary>
    /// Pre-warms the Action of the Day cache. Called by background worker.
    /// </summary>
    public async Task WarmActionOfTheDayCacheAsync(string language = "en")
    {
        if (_contentCache is null)
        {
            _logger.LogWarning("Content cache not available - cannot warm AoTD");
            return;
        }

        _logger.LogInformation("Pre-warming Action of the Day ({Language})...", language);

        // Get the best cause (no exclusions for AoTD)
        var cause = await _causes.GetBestCauseAsync(
            excludeIds: [],
            preferredCategories: [],
            maxDonationAmount: 5.00m);

        if (cause is null)
        {
            _logger.LogWarning("No causes available for AoTD");
            return;
        }

        // Generate or get language-specific cached summary
        var cachedSummary = await _contentCache.GetSummaryAsync(cause.Id, language);
        if (cachedSummary is not null)
        {
            cause.Summary = cachedSummary;
        }
        else
        {
            cause.Summary = await _ai.SummarizeDescriptionAsync(
                cause.Title, cause.Description, cause.Category, language);
            await _contentCache.SetSummaryAsync(cause.Id, language, cause.Summary);
        }

        // Generate or get cached action
        var cachedAction = await _contentCache.GetDailyActionAsync(cause.Id, language);
        if (cachedAction is null)
        {
            var actionType = DetermineActionType(cause, null);
            var action = await _ai.GenerateActionAsync(cause, actionType, null, language);
            await _causes.AddActionAsync(action);
            cachedAction = CreateCachedAction(action);
            await _contentCache.SetDailyActionAsync(cause.Id, language, cachedAction);
        }

        // Set as Action of the Day (cache until 6 hours)
        await _contentCache.SetActionOfTheDayAsync(language, cachedAction, TimeSpan.FromHours(6));
        _logger.LogInformation("✅ Pre-warmed Action of the Day: {Title} ({Language})", 
            cachedAction.Headline, language);
    }

    /// <summary>
    /// Pre-generates AI summaries and action cards for all causes so users never wait.
    /// Called by background worker after cause refresh.
    /// </summary>
    public async Task WarmAllCausesCacheAsync(string language, CancellationToken ct = default)
    {
        if (_contentCache is null)
        {
            _logger.LogWarning("Content cache not available — cannot warm cause caches");
            return;
        }

        var allCauses = await _causes.GetAllCausesAsync();
        _logger.LogInformation("Pre-warming cache for {Count} causes ({Language})...", allCauses.Count, language);

        var warmed = 0;
        var skipped = 0;

        foreach (var cause in allCauses)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                // 1. Pre-generate AI summary if not cached
                var cachedSummary = await _contentCache.GetSummaryAsync(cause.Id, language);
                if (cachedSummary is null)
                {
                    var summary = await _ai.SummarizeDescriptionAsync(
                        cause.Title, cause.Description, cause.Category, language);
                    await _contentCache.SetSummaryAsync(cause.Id, language, summary);

                    // Persist AI summary to database so it survives cache eviction
                    await _causes.UpdateSummaryAsync(cause.Id, language, summary);
                }

                // 2. Pre-generate action card if not cached
                var cachedAction = await _contentCache.GetDailyActionAsync(cause.Id, language);
                if (cachedAction is null)
                {
                    cause.Summary = cachedSummary
                        ?? await _contentCache.GetSummaryAsync(cause.Id, language)
                        ?? cause.Description;

                    var actionType = DetermineActionType(cause, null);
                    var action = await _ai.GenerateActionAsync(cause, actionType, null, language);
                    action.IsAiGenerated = true;
                    action.Language = language;
                    await _causes.AddActionAsync(action);

                    var cached = CreateCachedAction(action);
                    await _contentCache.SetDailyActionAsync(cause.Id, language, cached);
                    warmed++;

                    // Push AI content to connected clients in real-time
                    if (_notifier is not null)
                    {
                        await _notifier.NotifyCauseUpdatedAsync(
                            cause.Id, action.Headline, cause.Summary,
                            action.WhyNow, action.ImpactStatement, language);
                    }
                }
                else
                {
                    skipped++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to warm cache for cause '{Title}'", cause.Title);
            }
        }

        _logger.LogInformation(
            "✅ Cache warming complete ({Language}): {Warmed} generated, {Skipped} already cached",
            language, warmed, skipped);
    }

    public async Task<DailyAction?> GenerateActionForCauseAsync(Guid userId, Guid causeId, string language = "en")
    {
        var cause = await _causes.GetByIdAsync(causeId);
        if (cause is null)
        {
            _logger.LogWarning("Cause {CauseId} not found", causeId);
            return null;
        }

        // Get user profile for personalisation
        var profile = userId != Guid.Empty
            ? await _users.GetProfileAsync(userId)
            : null;

        // Determine best action type for this cause
        var actionType = DetermineActionType(cause, profile);

        // Check DB for existing pre-generated action, otherwise use fallback
        var existingAction = await _causes.GetLatestActionByCauseIdAsync(causeId, language);
        DailyAction action;
        if (existingAction is not null)
        {
            existingAction.Cause = cause;
            action = existingAction;
        }
        else
        {
            action = CreateFallbackAction(cause, actionType, language);
        }

        // Save the action
        if (existingAction is null)
        {
            await _causes.AddActionAsync(action);
        }

        // Update cache with selected cause
        if (_cache is not null)
        {
            var cacheKey = $"action:today:{userId}:{language}";
            var ttl = TimeUntilEndOfDay(profile?.UserId.ToString());
            await _cache.SetStringAsync(cacheKey,
                JsonSerializer.Serialize(action),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl });
        }

        return action;
    }

    public async Task<CompleteActionResult> CompleteActionAsync(
        Guid userId,
        Guid actionId,
        CompleteActionRequest request)
    {
        var action = await _causes.GetActionByIdAsync(actionId);
        if (action is null)
        {
            throw new InvalidOperationException("Action not found");
        }

        // Record completion
        // Ensure the user exists in the database (anonymous users get auto-created)
        var existingUser = await _users.GetByIdAsync(userId);
        if (existingUser is null)
        {
            await _users.CreateAsync(new User
            {
                Id = userId,
                Email = string.Empty,
                DisplayName = "Anonymous",
                CreatedAt = DateTime.UtcNow,
                LastActiveAt = DateTime.UtcNow,
                IsAnonymous = true
            });
        }

        var userAction = new UserAction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DailyActionId = actionId,
            CompletedAt = DateTime.UtcNow,
            ActionType = action.Type,
            AmountDonated = request.AmountDonated,
            UserNote = request.UserNote
        };

        await _users.RecordActionAsync(userAction);

        // Increment counter
        action.TimesCompleted++;
        await _causes.UpdateActionAsync(action);

        // Clear cache so user sees "done" state
        if (_cache is not null && userId != Guid.Empty)
        {
            await _cache.RemoveAsync($"action:today:{userId}");
        }

        return new CompleteActionResult(
            ImpactMessage: GenerateImpactMessage(action),
            TotalContributors: action.TimesCompleted,
            OutcomeUrl: $"/outcomes/{actionId}");
    }

    public async Task<List<ActionHistoryItem>> GetHistoryAsync(Guid userId)
    {
        // This would need a proper query - simplified for now
        return [];
    }

    public async Task<Outcome?> GetOutcomeAsync(Guid actionId)
    {
        var action = await _causes.GetActionByIdAsync(actionId);
        return action?.Outcome;
    }

    public async Task SkipActionAsync(Guid userId, Guid causeId)
    {
        _logger.LogInformation("User {UserId} skipped cause {CauseId}", userId, causeId);

        // Track skipped cause to avoid showing it again today
        await AddSkippedCauseIdAsync(userId, causeId);

        if (_cache is not null)
        {
            // Clear all possible user-specific cached actions
            // The cache key format is: action:today:{userId}:{language}:{category}:{actionType}
            var languages = new[] { "en", "de" };
            var categories = new[] { "all" }
                .Concat(Enum.GetNames<CauseCategory>());
            var actionTypes = new[] { "all", "Donate", "Sign", "Write", "Share" };

            foreach (var lang in languages)
            {
                foreach (var cat in categories)
                {
                    foreach (var at in actionTypes)
                    {
                        await _cache.RemoveAsync($"action:today:{userId}:{lang}:{cat}:{at}");
                    }
                }
            }
        }
    }

    private async Task<List<Guid>> GetSkippedCauseIdsAsync(Guid userId)
    {
        if (_cache is null) return [];

        var cacheKey = $"skipped-causes:today:{userId}";
        var cached = await _cache.GetStringAsync(cacheKey);
        if (string.IsNullOrWhiteSpace(cached)) return [];

        return JsonSerializer.Deserialize<List<Guid>>(cached) ?? [];
    }

    private async Task AddSkippedCauseIdAsync(Guid userId, Guid causeId)
    {
        if (_cache is null) return;

        var cacheKey = $"skipped-causes:today:{userId}";

        // Accumulate all skipped causes so the user cycles through every cause
        // in the category before wrapping around. The wrap-around logic in
        // GetTodaysActionAsync clears this list once all causes are exhausted.
        var skippedIds = await GetSkippedCauseIdsAsync(userId);
        if (!skippedIds.Contains(causeId))
        {
            skippedIds.Add(causeId);
        }

        await _cache.SetStringAsync(cacheKey,
            JsonSerializer.Serialize(skippedIds),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) });
    }

    private async Task ClearSkippedCauseIdsAsync(Guid userId)
    {
        if (_cache is null) return;

        var cacheKey = $"skipped-causes:today:{userId}";
        await _cache.RemoveAsync(cacheKey);
    }

    private ActionType DetermineActionType(Cause cause, UserProfile? profile)
    {
        // Petition sources → Sign action
        if (cause.SourceApiName is "openPetition.de" or "Campact")
        {
            return ActionType.Sign;
        }

        // Parliament sources → Write action (letter to MP)
        if (cause.SourceApiName == "Abgeordnetenwatch"
            && (profile?.WillingToWrite ?? true))
        {
            return ActionType.Write;
        }

        // Donation platform sources → Donate action (if user is willing to donate)
        // betterplace.org is a donation/fundraising platform - always offer donation
        if (cause.SourceApiName is "betterplace.org" or "GoFundMe" or "PayPal Giving Fund"
            && cause.FundingGoal.HasValue
            && (profile?.WillingToDonate ?? true))
        {
            return ActionType.Donate;
        }

        // Donation sources with small funding gap → Donate action (if user is willing and amount is reasonable)
        if (cause.FundingGap is > 0 and < 500
            && (profile?.WillingToDonate ?? true)
            && (profile?.MaxDonationPerAction ?? 5) >= 2)
        {
            return ActionType.Donate;
        }

        // Donation source but user doesn't want to donate → Share instead
        if (cause.FundingGoal.HasValue)
        {
            return ActionType.Share;
        }

        // Default to share (zero barrier)
        return ActionType.Share;
    }

    private static string GenerateImpactMessage(DailyAction action) =>
        action.TimesCompleted switch
        {
            1 => "You're the first. That takes courage.",
            < 10 => $"You're one of {action.TimesCompleted} people who acted today.",
            < 100 => $"{action.TimesCompleted} people including you are making this happen.",
            _ => $"You joined {action.TimesCompleted:N0} people. This is a movement."
        };

    private static TimeSpan TimeUntilEndOfDay(string? timeZone)
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZone ?? "UTC");
            var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            var endOfDay = now.Date.AddDays(1);
            return endOfDay - now;
        }
        catch
        {
            return TimeSpan.FromHours(24);
        }
    }

    private CachedDailyAction CreateCachedAction(DailyAction action)
    {
        return new CachedDailyAction
        {
            ActionId = action.Id,
            CauseId = action.CauseId,
            Type = action.Type.ToString(),
            Headline = action.Headline,
            CallToAction = action.CallToAction,
            WhyNow = action.WhyNow,
            ImpactStatement = action.ImpactStatement,
            CauseCategory = action.Cause.Category.ToString(),
            CauseOrganisation = action.Cause.OrganisationName,
            CauseUrl = action.Cause.OrganisationUrl,
            CauseImageUrl = action.Cause.ImageUrl,
            CauseSummary = action.Cause.Summary,
            CauseDescription = action.Cause.Description,
            SuggestedAmount = action.SuggestedAmount,
            PaymentLinkUrl = action.StripePaymentLinkUrl,
            PreWrittenLetter = action.PreWrittenLetter,
            RecipientName = action.RecipientName,
            RecipientEmail = action.RecipientEmail,
            ShareText = action.ShareText,
            ShareUrl = action.ShareUrl,
            IsAiGenerated = action.IsAiGenerated,
            GeneratedAt = DateTime.UtcNow
        };
    }

    private async Task<DailyAction?> HydrateCachedAction(CachedDailyAction cached)
    {
        // Get the full cause from DB to have complete data
        var cause = await _causes.GetByIdAsync(cached.CauseId);
        if (cause is null) return null;

        // Use cached summary if available
        if (!string.IsNullOrEmpty(cached.CauseSummary))
        {
            cause.Summary = cached.CauseSummary;
        }

        return new DailyAction
        {
            Id = cached.ActionId,
            CauseId = cached.CauseId,
            Cause = cause,
            Type = Enum.Parse<ActionType>(cached.Type),
            Headline = cached.Headline,
            CallToAction = cached.CallToAction,
            WhyNow = cached.WhyNow,
            ImpactStatement = cached.ImpactStatement,
            SuggestedAmount = cached.SuggestedAmount,
            StripePaymentLinkUrl = cached.PaymentLinkUrl,
            PreWrittenLetter = cached.PreWrittenLetter,
            RecipientName = cached.RecipientName,
            RecipientEmail = cached.RecipientEmail,
            ShareText = cached.ShareText,
            ShareUrl = cached.ShareUrl,
            IsAiGenerated = cached.IsAiGenerated,
            ValidFrom = DateTime.UtcNow.Date,
            ValidUntil = DateTime.UtcNow.Date.AddDays(1),
            Status = ActionStatus.Active
        };
    }

    /// <summary>
    /// Creates a DailyAction using only original source data when AI generation fails.
    /// The UI will show source content immediately; AI content can be generated later.
    /// </summary>
    private static DailyAction CreateFallbackAction(Cause cause, ActionType actionType, string language = "en")
    {
        var buttonLabel = actionType switch
        {
            ActionType.Donate => "Donate",
            ActionType.Sign => "Sign",
            ActionType.Write => "Write",
            _ => "Take Action"
        };

        return new DailyAction
        {
            Id = Guid.NewGuid(),
            CauseId = cause.Id,
            Cause = cause,
            Type = actionType,
            Headline = cause.Title,
            CallToAction = buttonLabel,
            WhyNow = string.Empty,
            ImpactStatement = string.Empty,
            SuggestedAmount = cause.FundingGap is > 0 and <= 50 ? cause.FundingGap : 5.00m,
            StripePaymentLinkUrl = cause.OrganisationUrl,
            ShareUrl = cause.OrganisationUrl,
            IsAiGenerated = false,
            Language = language,
            ValidFrom = DateTime.UtcNow.Date,
            ValidUntil = DateTime.UtcNow.Date.AddDays(1),
            Status = ActionStatus.Active
        };
    }
}
