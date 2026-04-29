using Microsoft.AspNetCore.SignalR;
using OneGood.Api.Hubs;
using OneGood.Core.Interfaces;

namespace OneGood.Api.Services;

/// <summary>
/// SignalR implementation of ICauseNotifier.
/// Pushes real-time updates to all connected clients.
/// </summary>
public class SignalRCauseNotifier : ICauseNotifier
{
    private readonly IHubContext<CauseHub> _hub;

    public SignalRCauseNotifier(IHubContext<CauseHub> hub) => _hub = hub;

    public async Task NotifyCauseUpdatedAsync(
        Guid causeId, string headline, string summary,
        string whyNow, string impactStatement, string language)
    {
        await _hub.Clients.All.SendAsync("CauseUpdated", new
        {
            causeId,
            headline,
            summary,
            whyNow,
            impactStatement,
            language
        });
    }

    public async Task NotifyCategoryCountsUpdatedAsync(Dictionary<string, int> counts)
    {
        await _hub.Clients.All.SendAsync("CategoryCountsUpdated", counts);
    }

    public async Task NotifyCausesReadyAsync(int count)
    {
        await _hub.Clients.All.SendAsync("CausesReady", new { count });
    }
}
