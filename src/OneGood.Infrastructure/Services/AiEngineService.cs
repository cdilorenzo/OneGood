using System.Text.Json;
using Microsoft.Extensions.Logging;
using OneGood.Core.AI;
using OneGood.Core.Enums;
using OneGood.Core.Interfaces;
using OneGood.Core.Models;
using OneGood.Infrastructure.Translation;

namespace OneGood.Infrastructure.Services;

/// <summary>
/// AI Engine implementation using the configured AI provider (Groq, Gemini, or Anthropic).
/// Handles all AI-powered content generation for OneGood.
/// Uses dedicated translation service for titles (free, accurate).
/// Uses AI only for creative content (summaries, impact statements).
/// </summary>
public class AiEngineService : IAiEngine
{
    private readonly IAiService _aiService;
    private readonly ITranslationService _translationService;
    private readonly ILogger<AiEngineService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AiEngineService(
        IAiService aiService, 
        ITranslationService translationService,
        ILogger<AiEngineService> logger)
    {
        _aiService = aiService;
        _translationService = translationService;
        _logger = logger;
    }

    public async Task<DailyAction> GenerateActionAsync(
        Cause cause,
        ActionType type,
        UserProfile? userProfile,
        string language = "en")
    {
        // Determine target language name for the prompt
        var languageName = language switch
        {
            "de" => "German",
            "fr" => "French",
            "es" => "Spanish",
            "it" => "Italian",
            "nl" => "Dutch",
            _ => "English"
        };

        // Translate the title using free translation service (not AI)
        var translatedTitle = await _translationService.TranslateAsync(
            cause.Title, 
            "de",  // Source language (German for betterplace etc.)
            language); // Target language from user

        var systemPrompt = $"""
            You are the action writer for OneGood, an app that shows people one 
            impactful action per day. Your writing must be:
            - Warm, human, and never preachy
            - Specific and honest — never vague
            - Under 25 words per field
            - Focused on positive possibility, not guilt
            - Always showing the human impact, not abstract statistics
            - ALL TEXT MUST BE IN {languageName.ToUpperInvariant()}
            Respond ONLY in valid JSON matching the schema provided.
            """;

        var userPrompt = $@"Generate supporting content for this OneGood daily action.
IMPORTANT: All text fields must be written in {languageName}.

Cause Title: {translatedTitle}
Organisation: {cause.OrganisationName}
Description: {cause.Description}
Category: {cause.Category}
Action Type: {type}
Funding Gap: {cause.FundingGap?.ToString("C") ?? "N/A"}
Deadline: {cause.Deadline?.ToString("dd MMM yyyy") ?? "ongoing"}
People who can be impacted: {cause.ActionsToTippingPoint} more actions needed

Return JSON with these fields IN {languageName.ToUpperInvariant()} (DO NOT generate a headline - we already have the translated title):
- whyNow: one line — why timing matters right now
- impactStatement: one line — concrete human impact of taking this action (DO NOT mention specific money amounts like €25 or €10 — keep it general about what the action achieves)
- suggestedAmount: number in EUR if donation, else null
- preWrittenLetter: full letter text if type is Write, else null
- shareText: ready-to-post social text if type is Share, else null";

        var messages = new[]
        {
            new ChatMessage(ChatRole.System, systemPrompt),
            new ChatMessage(ChatRole.User, userPrompt)
        };

        var response = await _aiService.GetCompletionAsync(messages);
        var json = ExtractJson(response);
        var generated = JsonSerializer.Deserialize<GeneratedActionDto>(json, JsonOptions);

        return new DailyAction
        {
            Id = Guid.NewGuid(),
            CauseId = cause.Id,
            Cause = cause,
            Type = type,
            Headline = translatedTitle, // Use translated title, not AI-generated
            CallToAction = GetDefaultCallToAction(type),
            WhyNow = generated?.WhyNow ?? "Every moment counts",
            ImpactStatement = generated?.ImpactStatement ?? "Your action matters",
            SuggestedAmount = generated?.SuggestedAmount,
            PreWrittenLetter = generated?.PreWrittenLetter,
            ShareText = generated?.ShareText,
            ValidFrom = DateTime.UtcNow,
            ValidUntil = cause.Deadline ?? DateTime.UtcNow.AddHours(24),
            Status = ActionStatus.Active
        };
    }

    public async Task<ScoredCause> ScoreCauseUrgencyAsync(Cause cause)
    {
        var prompt = $@"Score this cause for urgency.

Cause: {cause.Title}
Deadline: {cause.Deadline}
Funding Gap: {cause.FundingGap}
Funding Goal: {cause.FundingGoal}
Description: {cause.Description}

Return JSON with: urgencyScore (0-100), leverageScore (0-100), actionsToTippingPoint (number), urgencyReason (sentence)";

        var response = await _aiService.GetCompletionAsync(prompt);
        var json = ExtractJson(response);

        try
        {
            var scored = JsonSerializer.Deserialize<ScoredCauseDto>(json, JsonOptions);
            return new ScoredCause(
                scored?.UrgencyScore ?? 50,
                scored?.LeverageScore ?? 50,
                scored?.ActionsToTippingPoint ?? 100,
                scored?.UrgencyReason ?? "Ongoing need");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse urgency score, using defaults");
            return new ScoredCause(50, 50, 100, "Ongoing need");
        }
    }

    public async Task<string> PersonaliseWhyYouAsync(DailyAction action, UserProfile userProfile)
    {
        var prompt = $@"Write a single personalised sentence (max 15 words) connecting this person to this action. Sound like a thoughtful friend.

User interests: {userProfile.InferredInterestsSummary ?? "General good causes"}
Previous actions: {userProfile.ActionsCompleted}
Action: {action.Headline}
Category: {action.Cause.Category}

Return ONLY the sentence.";

        var response = await _aiService.GetCompletionAsync(prompt);
        return response.Trim().Trim('"');
    }

    public async Task<Outcome> GenerateOutcomeStoryAsync(DailyAction action, string rawOutcomeData)
    {
        var prompt = $@"Write an outcome story for a completed OneGood action. Be honest.

Original action: {action.Headline}
Raw outcome data: {rawOutcomeData}
Contributors: {action.TimesCompleted} people took action

Return JSON with: headline, story (2-3 sentences), isPositive (bool), peopleImpacted (number)";

        var response = await _aiService.GetCompletionAsync(prompt);
        var json = ExtractJson(response);
        var dto = JsonSerializer.Deserialize<OutcomeDto>(json, JsonOptions);

        return new Outcome
        {
            Id = Guid.NewGuid(),
            DailyActionId = action.Id,
            Headline = dto?.Headline ?? "Outcome received",
            Story = dto?.Story ?? "Thank you for taking action.",
            IsPositive = dto?.IsPositive ?? true,
            PeopleImpacted = dto?.PeopleImpacted ?? 0,
            TotalActionsContributed = action.TimesCompleted,
            OutcomeDate = DateTime.UtcNow
        };
    }

    public async Task<UserProfile> UpdateInferredPreferencesAsync(
        UserProfile profile,
        List<UserAction> recentActions)
    {
        var actionSummary = string.Join(", ",
            recentActions.Select(a => $"{a.ActionType}: {a.DailyAction?.Cause?.Category}"));

        var prompt = $@"Based on this user's recent actions, write a brief 1-2 sentence summary of what causes they seem to care about most.

Recent actions: {actionSummary}
Total actions: {profile.ActionsCompleted}

Return ONLY the summary.";

        var response = await _aiService.GetCompletionAsync(prompt);
        profile.InferredInterestsSummary = response.Trim().Trim('"');
        return profile;
    }

    public async Task<string> SummarizeDescriptionAsync(
        string title, 
        string description, 
        CauseCategory category,
        string language = "en")
    {
        // If description is already short, no need to summarize
        if (string.IsNullOrWhiteSpace(description) || description.Length < 150)
        {
            return description;
        }

        // Determine target language name
        var languageName = language switch
        {
            "de" => "German",
            "fr" => "French",
            "es" => "Spanish",
            "it" => "Italian",
            "nl" => "Dutch",
            _ => "English"
        };

        try
        {
            var prompt = $@"Summarize this charitable cause in 1-2 sentences (max 50 words) IN {languageName.ToUpperInvariant()}. 
Be warm, specific, and focus on the human impact. No jargon.

Title: {title}
Category: {category}
Description: {description}

Return ONLY the summary in {languageName}, nothing else.";

            var response = await _aiService.GetCompletionAsync(prompt);
            var summary = response.Trim().Trim('"');

            // Ensure it's not too long
            if (summary.Length > 250)
            {
                summary = summary[..247] + "...";
            }

            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to summarize description, using truncated original");
            // Fallback: truncate the original
            return description.Length > 150 
                ? description[..147] + "..." 
                : description;
        }
    }

    private static string ExtractJson(string response)
    {
        var jsonStart = response.IndexOf('{');
        var jsonEnd = response.LastIndexOf('}');
        if (jsonStart >= 0 && jsonEnd > jsonStart)
        {
            return response[jsonStart..(jsonEnd + 1)];
        }
        return response;
    }

    private static string GetDefaultCallToAction(ActionType type) => type switch
    {
        ActionType.Donate => "Donate now",
        ActionType.Sign => "Sign the petition",
        ActionType.Write => "Send your message",
        ActionType.Share => "Share this cause",
        _ => "Take action"
    };

    private record GeneratedActionDto(
        string? Headline,
        string? CallToAction,
        string? WhyNow,
        string? ImpactStatement,
        decimal? SuggestedAmount,
        string? PreWrittenLetter,
        string? ShareText);

    private record ScoredCauseDto(
        double UrgencyScore,
        double LeverageScore,
        int ActionsToTippingPoint,
        string? UrgencyReason);

    private record OutcomeDto(
        string? Headline,
        string? Story,
        bool IsPositive,
        int PeopleImpacted);
}
