using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using OneGood.Core.AI;
using OneGood.Core.Classification;
using OneGood.Core.Enums;

namespace OneGood.Infrastructure.Classification;

/// <summary>
/// AI-powered cause classifier. Uses the configured LLM provider to classify charitable
/// causes into categories with high accuracy across languages and domains.
///
/// - Results are cached in-memory by content hash so identical causes are never re-classified.
/// - The system prompt is designed for minimal token usage (single-word response).
/// - If the AI call fails or returns an unparseable result, the caller-provided fallback is used.
/// </summary>
public sealed class AiCauseClassifier : ICauseClassifier
{
    private readonly IAiService _aiService;
    private readonly ILogger<AiCauseClassifier> _logger;

    /// <summary>In-memory cache: content hash -> classified category.</summary>
    private readonly ConcurrentDictionary<string, CauseCategory> _cache = new();

    private const string SystemPrompt = """
        You are a precise text classifier for a charitable causes app.
        Classify the given cause into exactly ONE of these categories:

        ClimateAndNature  - Environment, climate change, nature conservation, forests, oceans, sustainability, renewable energy, biodiversity
        HumanRights       - Human rights, social justice, equality, humanitarian aid, development aid, child protection, gender equality
        Peace             - Peace, conflict resolution, disarmament, reconciliation, violence prevention
        Education         - Education, schools, youth development, literacy, scholarships, sport clubs, training, coaching, skill building, culture
        CleanWater        - Clean water access, sanitation, wells, water supply, hygiene
        FoodSecurity      - Food, hunger, agriculture, nutrition, food banks, farming
        AnimalWelfare     - Animal rescue, shelters, wildlife, veterinary, animal protection, pets
        MentalHealth      - Mental health, physical health, medical care, hospitals, therapy, healthcare
        Refugees          - Refugees, asylum, displaced people, migration, integration of refugees
        Democracy         - Democracy, political participation, elections, parliament, press freedom, petitions, transparency, civic engagement

        Rules:
        - Respond with ONLY the category name (e.g. "Education"), nothing else.
        - If a cause involves sport, youth clubs, or skill development, classify as Education.
        - If unclear, pick the closest match based on the primary intent of the cause.
        - Never refuse to classify. Always return exactly one category name.
        """;

    public AiCauseClassifier(IAiService aiService, ILogger<AiCauseClassifier> logger)
    {
        _aiService = aiService;
        _logger = logger;
    }

    public async Task<CauseCategory> ClassifyAsync(
        string? title,
        string? description = null,
        CauseCategory fallback = CauseCategory.HumanRights,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(description))
            return fallback;

        // Check cache first (keyed by content hash)
        var cacheKey = ComputeContentHash(title, description);
        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            _logger.LogDebug("Classification cache hit for '{Title}' -> {Category}", Truncate(title, 50), cached);
            return cached;
        }

        try
        {
            var result = await CallAiAsync(title, description, cancellationToken);
            if (result is not null)
            {
                _cache.TryAdd(cacheKey, result.Value);
                _logger.LogInformation("Classified '{Title}' as {Category}", Truncate(title, 60), result.Value);
                return result.Value;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI classification failed for '{Title}', using fallback {Fallback}",
                Truncate(title, 60), fallback);
        }

        // AI returned unparseable response or threw — use fallback
        _cache.TryAdd(cacheKey, fallback);
        return fallback;
    }

    private async Task<CauseCategory?> CallAiAsync(
        string? title,
        string? description,
        CancellationToken cancellationToken)
    {
        // Build a minimal user prompt — keep token count low
        var userPrompt = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(title))
            userPrompt.Append($"Title: {title}");
        if (!string.IsNullOrWhiteSpace(description))
        {
            // Truncate very long descriptions to save tokens
            var desc = description.Length > 500 ? description[..500] + "..." : description;
            userPrompt.Append($"\nDescription: {desc}");
        }

        var messages = new[]
        {
            new ChatMessage(ChatRole.System, SystemPrompt),
            new ChatMessage(ChatRole.User, userPrompt.ToString())
        };

        var response = await _aiService.GetCompletionAsync(messages, cancellationToken);
        return ParseCategory(response);
    }

    /// <summary>
    /// Parses the AI response into a <see cref="CauseCategory"/>.
    /// Handles various response formats: exact match, quoted, with explanation, etc.
    /// </summary>
    private static CauseCategory? ParseCategory(string? response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return null;

        // Clean up — AI might return quotes, periods, or extra text
        var cleaned = response
            .Trim()
            .Trim('"', '\'', '.', ',', ' ', '\n', '\r')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()?
            .Trim();

        if (string.IsNullOrEmpty(cleaned))
            return null;

        // Try exact enum parse
        if (Enum.TryParse<CauseCategory>(cleaned, ignoreCase: true, out var exact))
            return exact;

        // Handle multi-word format: "Climate And Nature", "Animal Welfare", "Clean_Water"
        var normalized = cleaned.Replace(" ", "").Replace("_", "").Replace("-", "");
        if (Enum.TryParse<CauseCategory>(normalized, ignoreCase: true, out var fuzzy))
            return fuzzy;

        // Find a category name anywhere in the response: "The category is Education."
        foreach (var cat in Enum.GetValues<CauseCategory>())
        {
            if (cleaned.Contains(cat.ToString(), StringComparison.OrdinalIgnoreCase))
                return cat;
        }

        return null;
    }

    private static string ComputeContentHash(string? title, string? description)
    {
        var input = Encoding.UTF8.GetBytes(
            $"{title?.ToLowerInvariant()}|{description?.ToLowerInvariant()}");
        var hash = SHA256.HashData(input);
        return Convert.ToHexStringLower(hash[..8]);
    }

    private static string Truncate(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return "(empty)";
        return text.Length <= maxLength ? text : text[..(maxLength - 3)] + "...";
    }
}
