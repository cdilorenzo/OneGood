using Microsoft.AspNetCore.Mvc;
using OneGood.Core.AI;

namespace OneGood.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AiController : ControllerBase
{
    private readonly IAiService _aiService;

    public AiController(IAiService aiService)
    {
        _aiService = aiService;
    }

    /// <summary>
    /// POST /api/ai/regenerate
    /// Lightweight endpoint used by the Web client to ask the server to generate
    /// or translate a short summary from a longer source text. The server will
    /// call the configured IAiService (Groq/Gemini/Anthropic) to produce the text.
    /// </summary>
    [HttpPost("regenerate")]
    public async Task<ActionResult<RegenerateResponse>> Regenerate([FromBody] RegenerateRequest req)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Text))
        {
            var msg = ApiMessages.Error_MissingText;
            return BadRequest(msg);
        }

        // Build a clear prompt that asks for a short human-friendly summary.
        // Respect requested target language if provided.
        var target = string.IsNullOrWhiteSpace(req.TargetLang) ? "the original language" : req.TargetLang;

        var prompt = $@"Summarize the following text in 1-2 short sentences (max ~50 words). Be warm, human, and specific. Return ONLY the summary, nothing else. Output language: {target}.

Text:
{req.Text}";

        try
        {
            var resp = await _aiService.GetCompletionAsync(prompt);
            var summary = resp?.Trim().Trim('"') ?? string.Empty;
            return Ok(new RegenerateResponse { Summary = summary });
        }
        catch (Exception)
        {
            // Don't leak provider details to the client
            var msg = ApiMessages.Error_AIGenerationFailed;
            return StatusCode(500, msg);
        }
    }

    /// <summary>
    /// POST /api/ai/translate-metadata
    /// Translates action metadata (headline, description, urgency, impact) to a target language.
    /// Used by the Web client to provide localized content for different languages.
    /// </summary>
    [HttpPost("translate-metadata")]
    public async Task<ActionResult<TranslateMetadataResponse>> TranslateMetadata([FromBody] TranslateMetadataRequest req)
    {
        if (req is null)
        {
            var msg = ApiMessages.Error_MissingRequestBody;
            return BadRequest(msg);
        }

        var sourceLang = req.SourceLang ?? "de";
        var targetLang = req.TargetLang ?? "en";

        // If source and target are the same, return original values
        if (sourceLang.Equals(targetLang, StringComparison.OrdinalIgnoreCase))
        {
            return Ok(new TranslateMetadataResponse
            {
                Headline = req.Headline,
                Description = req.Description,
                Urgency = req.Urgency,
                Impact = req.Impact
            });
        }

        try
        {
            // Build a comprehensive translation prompt
            var prompt = BuildMetadataTranslationPrompt(req, sourceLang, targetLang);
            var response = await _aiService.GetCompletionAsync(prompt);

            // Parse the JSON response
            var translated = ParseTranslatedMetadata(response, req);
            return Ok(translated);
        }
        catch (Exception)
        {
            // Return original values on error
            return Ok(new TranslateMetadataResponse
            {
                Headline = req.Headline,
                Description = req.Description,
                Urgency = req.Urgency,
                Impact = req.Impact
            });
        }
    }

    private string BuildMetadataTranslationPrompt(TranslateMetadataRequest req, string sourceLang, string targetLang)
    {
        var prompt = $@"Translate the following action metadata from {sourceLang} to {targetLang}. 
Keep the translations concise, warm, and human-friendly. 
Maintain the original meaning and urgency.

Output ONLY a JSON object with these exact fields: headline, description, urgency, impact

Metadata to translate:
{(string.IsNullOrWhiteSpace(req.Headline) ? "" : $"Headline: {req.Headline}\n")}
{(string.IsNullOrWhiteSpace(req.Description) ? "" : $"Description: {req.Description}\n")}
{(string.IsNullOrWhiteSpace(req.Urgency) ? "" : $"Urgency: {req.Urgency}\n")}
{(string.IsNullOrWhiteSpace(req.Impact) ? "" : $"Impact: {req.Impact}\n")}

Return ONLY the JSON object, no markdown or extra text.";

        return prompt;
    }

    private TranslateMetadataResponse ParseTranslatedMetadata(string response, TranslateMetadataRequest original)
    {
        try
        {
            // Try to extract JSON from the response
            var jsonContent = response?.Trim() ?? "{}";

            // Remove markdown code blocks if present
            if (jsonContent.StartsWith("```json"))
                jsonContent = jsonContent.Replace("```json", "").Replace("```", "");
            else if (jsonContent.StartsWith("```"))
                jsonContent = jsonContent.Replace("```", "");

            jsonContent = jsonContent.Trim();

            // Parse as JSON
            using var doc = System.Text.Json.JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;

            return new TranslateMetadataResponse
            {
                Headline = root.TryGetProperty("headline", out var h) ? h.GetString() ?? original.Headline : original.Headline,
                Description = root.TryGetProperty("description", out var d) ? d.GetString() ?? original.Description : original.Description,
                Urgency = root.TryGetProperty("urgency", out var u) ? u.GetString() ?? original.Urgency : original.Urgency,
                Impact = root.TryGetProperty("impact", out var i) ? i.GetString() ?? original.Impact : original.Impact
            };
        }
        catch
        {
            // If parsing fails, return original values
            return new TranslateMetadataResponse
            {
                Headline = original.Headline,
                Description = original.Description,
                Urgency = original.Urgency,
                Impact = original.Impact
            };
        }
    }

    public class RegenerateRequest
    {
        public string Text { get; set; } = string.Empty;
        public string? SourceLang { get; set; }
        public string? TargetLang { get; set; }
        public string? Title { get; set; }
        public string? Category { get; set; }
    }

    public class RegenerateResponse
    {
        public string Summary { get; set; } = string.Empty;
    }

    public class TranslateMetadataRequest
    {
        public string? Headline { get; set; }
        public string? Description { get; set; }
        public string? Urgency { get; set; }
        public string? Impact { get; set; }
        public string? SourceLang { get; set; }
        public string? TargetLang { get; set; }
    }

    public class TranslateMetadataResponse
    {
        public string? Headline { get; set; }
        public string? Description { get; set; }
        public string? Urgency { get; set; }
        public string? Impact { get; set; }
    }
}
