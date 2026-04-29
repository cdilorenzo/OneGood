using Microsoft.AspNetCore.SignalR;

namespace OneGood.Api.Hubs;

/// <summary>
/// SignalR hub for pushing real-time updates to the UI.
/// Used by the background worker to notify clients when AI content
/// has been generated for a cause, so the UI updates live.
/// </summary>
public class CauseHub : Hub
{
}
