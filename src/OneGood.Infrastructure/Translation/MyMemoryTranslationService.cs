using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Web;
using Microsoft.Extensions.Logging;

namespace OneGood.Infrastructure.Translation;

/// <summary>
/// Free translation service using MyMemory API.
/// Free tier: 1000 requests/day, no API key required.
/// https://mymemory.translated.net/doc/spec.php
/// </summary>
public class MyMemoryTranslationService : ITranslationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MyMemoryTranslationService> _logger;

    public MyMemoryTranslationService(
        IHttpClientFactory httpClientFactory,
        ILogger<MyMemoryTranslationService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("MyMemory");
        _httpClient.BaseAddress = new Uri("https://api.mymemory.translated.net/");
        _logger = logger;
    }

    public async Task<string> TranslateAsync(
        string text, 
        string sourceLang, 
        string targetLang, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        if (sourceLang == targetLang)
            return text;

        try
        {
            var langPair = $"{sourceLang}|{targetLang}";
            var encodedText = HttpUtility.UrlEncode(text);
            var url = $"get?q={encodedText}&langpair={langPair}";

            var response = await _httpClient.GetFromJsonAsync<MyMemoryResponse>(url, cancellationToken);

            if (response?.ResponseStatus == 200 && !string.IsNullOrEmpty(response.ResponseData?.TranslatedText))
            {
                return response.ResponseData.TranslatedText;
            }

            _logger.LogWarning("MyMemory translation failed: {Status}", response?.ResponseStatus);
            return text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Translation failed for text: {Text}", text[..Math.Min(50, text.Length)]);
            return text; // Return original on failure
        }
    }

    private class MyMemoryResponse
    {
        [JsonPropertyName("responseStatus")]
        public int ResponseStatus { get; set; }

        [JsonPropertyName("responseData")]
        public ResponseData? ResponseData { get; set; }
    }

    private class ResponseData
    {
        [JsonPropertyName("translatedText")]
        public string? TranslatedText { get; set; }
    }
}
