using Microsoft.AspNetCore.Mvc;
using OneGood.Core.Interfaces;

namespace OneGood.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ActionsController : ControllerBase
{
    private readonly IActionEngine _actionEngine;
    private readonly ICauseRepository _causeRepository;

    public ActionsController(IActionEngine actionEngine, ICauseRepository causeRepository)
    {
        _actionEngine = actionEngine;
        _causeRepository = causeRepository;
    }

    /// <summary>
    /// GET /api/actions/today
    /// Returns the single action for this user today
    /// </summary>
    [HttpGet("today")]
    public async Task<ActionResult<TodaysActionResponse>> GetTodaysAction([FromQuery] string? lang = "en", [FromQuery] string? category = null, [FromQuery] Guid? excludeCurrent = null, [FromQuery] string? type = null)
    {
        var userId = GetUserIdOrAnonymous();
        var action = await _actionEngine.GetTodaysActionAsync(userId, lang ?? "en", category, excludeCurrent, type);

        if (action is null)
        {
            return Ok(new TodaysActionResponse { HasAction = false });
        }

        var hasAiSummary = !string.IsNullOrEmpty(action.Cause.Summary)
            && action.Cause.Summary != action.Cause.Description
            && !action.Cause.Summary.EndsWith("...");

        return Ok(new TodaysActionResponse
        {
            HasAction = true,
            ActionId = action.Id,
            CauseId = action.CauseId,
            Type = action.Type.ToString(),
            Headline = action.Headline,
            CallToAction = action.CallToAction,
            WhyNow = action.WhyNow,
            WhyYou = action.WhyYou,
            ImpactStatement = action.ImpactStatement,
            CauseCategory = action.Cause.Category.ToString(),
            CauseOrganisation = action.Cause.OrganisationName,
            CauseUrl = action.Cause.OrganisationUrl,
            CauseImageUrl = action.Cause.ImageUrl,
            CauseSummary = hasAiSummary
                ? action.Cause.Summary
                : action.Cause.Description,
            IsAiSummary = hasAiSummary,
            IsAiGenerated = action.IsAiGenerated,
            CauseDescription = action.Cause.Description,
            // Donation-specific
            SuggestedAmount = action.SuggestedAmount,
            PaymentLinkUrl = action.StripePaymentLinkUrl,
            // Letter-specific
            PreWrittenLetter = action.PreWrittenLetter,
            RecipientName = action.RecipientName,
            RecipientEmail = action.RecipientEmail,
            // Share-specific
            ShareText = action.ShareText,
            ShareUrl = action.ShareUrl,
            // Meta
            ValidUntil = action.ValidUntil,
            GlobalCompletionCount = action.TimesCompleted
        });
    }

    /// <summary>
    /// POST /api/actions/{actionId}/complete
    /// Called when user completes the action
    /// </summary>
    [HttpPost("{actionId}/complete")]
    public async Task<ActionResult<CompleteActionResponse>> CompleteAction(
        Guid actionId,
        [FromBody] CompleteActionRequestDto request)
    {
        var userId = GetUserIdOrAnonymous();
        var coreRequest = new CompleteActionRequest(
            actionId,
            request.AmountDonated,
            request.UserNote);

        var result = await _actionEngine.CompleteActionAsync(userId, actionId, coreRequest);

        return Ok(new CompleteActionResponse
        {
            Success = true,
            ImpactMessage = result.ImpactMessage,
            TotalContributors = result.TotalContributors,
            OutcomeUrl = result.OutcomeUrl
        });
    }

    /// <summary>
    /// POST /api/actions/skip
    /// Called when user skips the current cause
    /// </summary>
    [HttpPost("skip")]
    public async Task<IActionResult> SkipAction([FromBody] SkipActionRequestDto request)
    {
        var userId = GetUserIdOrAnonymous();
        await _actionEngine.SkipActionAsync(userId, request.CauseId);
        return Ok();
    }

    /// <summary>
    /// GET /api/actions/{actionId}/outcome
    /// Returns the outcome of a past action
    /// </summary>
    [HttpGet("{actionId}/outcome")]
    public async Task<ActionResult<OutcomeResponse?>> GetOutcome(Guid actionId)
    {
        var outcome = await _actionEngine.GetOutcomeAsync(actionId);
        if (outcome is null)
        {
            return Ok(new OutcomeResponse { HasOutcome = false });
        }

        return Ok(new OutcomeResponse
        {
            HasOutcome = true,
            Headline = outcome.Headline,
            Story = outcome.Story,
            PhotoUrl = outcome.PhotoUrl,
            IsPositive = outcome.IsPositive,
            PeopleImpacted = outcome.PeopleImpacted,
            TotalContributors = outcome.TotalActionsContributed
        });
    }

    /// <summary>
    /// GET /api/actions/category-counts
    /// Returns the number of active causes per category
    /// </summary>
    [HttpGet("category-counts")]
    public async Task<ActionResult<Dictionary<string, int>>> GetCategoryCounts()
    {
        var counts = await _causeRepository.GetCauseCountsByCategoryAsync();
        return Ok(counts.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value));
    }

    /// <summary>
    /// GET /api/actions/type-counts
    /// Returns the number of active causes per action type (Donate, Sign, Write, Share)
    /// </summary>
    [HttpGet("type-counts")]
    public async Task<ActionResult<Dictionary<string, int>>> GetTypeCounts()
    {
        var counts = await _causeRepository.GetCauseCountsByActionTypeAsync();
        return Ok(counts);
    }

    /// <summary>
    /// GET /api/actions/causes
    /// Returns all available causes grouped by category and source
    /// </summary>
    [HttpGet("causes")]
    public async Task<ActionResult<List<CauseSummaryDto>>> GetAllCauses()
    {
        var causes = await _causeRepository.GetAllCausesAsync();
        var result = causes.Select(MapToCauseSummary).ToList();
        return Ok(result);
    }

    /// <summary>
    /// GET /api/actions/causes/{causeId}
    /// Returns a specific cause to select as today's action
    /// </summary>
    [HttpGet("causes/{causeId}")]
    public async Task<ActionResult<TodaysActionResponse>> SelectCause(Guid causeId)
    {
        var cause = await _causeRepository.GetByIdAsync(causeId);
        if (cause is null)
        {
            return NotFound();
        }

        // Generate an action for this specific cause
        var userId = GetUserIdOrAnonymous();
        var action = await _actionEngine.GenerateActionForCauseAsync(userId, causeId);

        if (action is null)
        {
            return Ok(new TodaysActionResponse { HasAction = false });
        }

        var hasAiSummary2 = !string.IsNullOrEmpty(action.Cause.Summary) 
            && action.Cause.Summary != action.Cause.Description;

        return Ok(new TodaysActionResponse
        {
            HasAction = true,
            ActionId = action.Id,
            CauseId = action.CauseId,
            Type = action.Type.ToString(),
            Headline = action.Headline,
            CallToAction = action.CallToAction,
            WhyNow = action.WhyNow,
            WhyYou = action.WhyYou,
            ImpactStatement = action.ImpactStatement,
            CauseCategory = action.Cause.Category.ToString(),
            CauseOrganisation = action.Cause.OrganisationName,
            CauseUrl = action.Cause.OrganisationUrl,
            CauseImageUrl = action.Cause.ImageUrl,
            CauseSummary = hasAiSummary2
                ? action.Cause.Summary
                : action.Cause.Description,
            IsAiSummary = hasAiSummary2,
            IsAiGenerated = action.IsAiGenerated,
            CauseDescription = action.Cause.Description,
            SuggestedAmount = action.SuggestedAmount,
            PaymentLinkUrl = action.StripePaymentLinkUrl,
            PreWrittenLetter = action.PreWrittenLetter,
            RecipientName = action.RecipientName,
            RecipientEmail = action.RecipientEmail,
            ShareText = action.ShareText,
            ShareUrl = action.ShareUrl,
            ValidUntil = action.ValidUntil,
            GlobalCompletionCount = action.TimesCompleted
        });
    }

    private static CauseSummaryDto MapToCauseSummary(Core.Models.Cause c) => new()
    {
        Id = c.Id,
        Title = c.Title,
        Summary = !string.IsNullOrEmpty(c.Summary) ? c.Summary : c.Description,
        Category = c.Category.ToString(),
        SourceApiName = c.SourceApiName,
        OrganisationName = c.OrganisationName,
        OrganisationUrl = c.OrganisationUrl,
        ImageUrl = c.ImageUrl,
        FundingGoal = c.FundingGoal,
        FundingCurrent = c.FundingCurrent,
        UrgencyScore = c.UrgencyScore,
        LeverageScore = c.LeverageScore
    };

    private Guid GetUserIdOrAnonymous()
    {
        // TODO: Extract from JWT when auth is implemented

        // For anonymous users, use a per-session ID from a cookie so that
        // skip tracking is per-browser, not shared across all anonymous users.
        const string sessionCookieName = "onegood_session";
        if (Request.Cookies.TryGetValue(sessionCookieName, out var sessionId)
            && Guid.TryParse(sessionId, out var parsed))
        {
            return parsed;
        }

        var newSessionId = Guid.NewGuid();
        Response.Cookies.Append(sessionCookieName, newSessionId.ToString(), new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            MaxAge = TimeSpan.FromDays(1),
            IsEssential = true
        });
        return newSessionId;
    }
}

// DTOs
public class TodaysActionResponse
{
    public bool HasAction { get; set; }
    public Guid ActionId { get; set; }
    public Guid CauseId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Headline { get; set; } = string.Empty;
    public string CallToAction { get; set; } = string.Empty;
    public string WhyNow { get; set; } = string.Empty;
    public string? WhyYou { get; set; }
    public string ImpactStatement { get; set; } = string.Empty;
    public string CauseCategory { get; set; } = string.Empty;
    public string CauseOrganisation { get; set; } = string.Empty;
    public string? CauseUrl { get; set; }
    public string? CauseImageUrl { get; set; }

    /// <summary>
    /// AI-generated short summary of the cause (1-2 sentences).
    /// Empty if no AI summary is available for the user's language.
    /// </summary>
    public string CauseSummary { get; set; } = string.Empty;

    /// <summary>
    /// Whether CauseSummary is an AI-generated summary (true) or raw description fallback (false).
    /// </summary>
    public bool IsAiSummary { get; set; }

    /// <summary>
    /// Whether the headline, whyNow, and impactStatement are AI-generated (true)
    /// or original source data fallback (false).
    /// </summary>
    public bool IsAiGenerated { get; set; }

    /// <summary>
    /// Original full description from the source.
    /// </summary>
    public string CauseDescription { get; set; } = string.Empty;

    public decimal? SuggestedAmount { get; set; }
    public string? PaymentLinkUrl { get; set; }
    public string? PreWrittenLetter { get; set; }
    public string? RecipientName { get; set; }
    public string? RecipientEmail { get; set; }
    public string? ShareText { get; set; }
    public string? ShareUrl { get; set; }
    public DateTime ValidUntil { get; set; }
    public int GlobalCompletionCount { get; set; }
}

public class CompleteActionRequestDto
{
    public decimal? AmountDonated { get; set; }
    public string? UserNote { get; set; }
}

public class SkipActionRequestDto
{
    public Guid CauseId { get; set; }
}

public class CompleteActionResponse
{
    public bool Success { get; set; }
    public string ImpactMessage { get; set; } = string.Empty;
    public int TotalContributors { get; set; }
    public string OutcomeUrl { get; set; } = string.Empty;
}

public class OutcomeResponse
{
    public bool HasOutcome { get; set; }
    public string Headline { get; set; } = string.Empty;
    public string Story { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public bool IsPositive { get; set; }
    public int PeopleImpacted { get; set; }
    public int TotalContributors { get; set; }
}

public class BrowseCausesResponse
{
    public int TotalCount { get; set; }
    public Dictionary<string, List<CauseSummaryDto>> ByCategory { get; set; } = new();
    public Dictionary<string, List<CauseSummaryDto>> BySource { get; set; } = new();
}

public class CauseSummaryDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string SourceApiName { get; set; } = string.Empty;
    public string OrganisationName { get; set; } = string.Empty;
    public string? OrganisationUrl { get; set; }
    public string? ImageUrl { get; set; }
    public decimal? FundingGoal { get; set; }
    public decimal? FundingCurrent { get; set; }
    public double UrgencyScore { get; set; }
    public double LeverageScore { get; set; }
}
