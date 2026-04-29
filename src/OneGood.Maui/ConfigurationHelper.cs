using System.IO;
using System.Text.Json;

namespace OneGood.Maui;

public static class ConfigurationHelper
{
    public static string? GetApiBaseUrl()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(configPath)) return null;
        using var stream = File.OpenRead(configPath);
        using var doc = JsonDocument.Parse(stream);
        if (doc.RootElement.TryGetProperty("Api", out var apiSection) &&
            apiSection.TryGetProperty("BaseUrl", out var baseUrlProp))
        {
            return baseUrlProp.GetString();
        }
        return null;
    }
}
