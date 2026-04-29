namespace OneGood.Core;

/// <summary>
/// Maps cause source API names to action types.
/// Used by repository filtering and action type counting.
/// </summary>
public static class ActionTypeMapping
{
    private static readonly string[] DonateSources =
        ["betterplace.org", "betterplace.org/events", "GoFundMe", "PayPal Giving Fund"];

    private static readonly string[] SignSources =
        ["openPetition.de", "Campact"];

    private static readonly string[] WriteSources =
        ["Abgeordnetenwatch"];

    /// <summary>
    /// Returns the action type string for a given source API name.
    /// </summary>
    public static string FromSource(string sourceApiName) => sourceApiName switch
    {
        "betterplace.org" or "betterplace.org/events" or "GoFundMe" or "PayPal Giving Fund" => "Donate",
        "openPetition.de" or "Campact" => "Sign",
        "Abgeordnetenwatch" => "Write",
        _ => "Share"
    };

    /// <summary>
    /// Returns the source API names that correspond to a given action type.
    /// Returns null if the action type is not recognized (show all).
    /// </summary>
    public static IEnumerable<string>? SourcesForType(string? actionType) => actionType?.ToLowerInvariant() switch
    {
        "donate" => DonateSources,
        "sign" => SignSources,
        "write" => WriteSources,
        _ => null
    };

    /// <summary>
    /// Returns source API names to EXCLUDE for the Share action type.
    /// Share is the fallback — causes NOT from donate/sign/write sources.
    /// </summary>
    public static IEnumerable<string> ExcludeSourcesForShare =>
        DonateSources.Concat(SignSources).Concat(WriteSources);

    /// <summary>
    /// Whether the given type string is the "Share" fallback type.
    /// </summary>
    public static bool IsShareType(string? actionType) =>
        string.Equals(actionType, "Share", StringComparison.OrdinalIgnoreCase);
}
