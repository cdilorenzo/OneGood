namespace OneGood.Infrastructure.Translation;

/// <summary>
/// Simple translation service for literal text translation.
/// Uses dedicated translation APIs (not AI) for accuracy and cost efficiency.
/// </summary>
public interface ITranslationService
{
    /// <summary>
    /// Translate text from source language to target language.
    /// </summary>
    Task<string> TranslateAsync(string text, string sourceLang, string targetLang, CancellationToken cancellationToken = default);
}
