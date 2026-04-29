namespace OneGood.Core.Interfaces;

/// <summary>
/// Pushes real-time updates to connected UI clients when content
/// has been generated or refreshed in the background.
/// </summary>
public interface ICauseNotifier
{
    /// <summary>
    /// Notifies all connected clients that AI content for a cause has been updated.
    /// </summary>
    Task NotifyCauseUpdatedAsync(Guid causeId, string headline, string summary, string whyNow, string impactStatement, string language);

    /// <summary>
    /// Notifies all connected clients that category counts have changed
    /// (e.g. after cause refresh added/removed causes).
    /// </summary>
    Task NotifyCategoryCountsUpdatedAsync(Dictionary<string, int> counts);

    /// <summary>
    /// Notifies all connected clients that causes are now available in the DB.
    /// Used on first load when the DB was empty and the Worker just finished importing.
    /// </summary>
    Task NotifyCausesReadyAsync(int count);
}
