using Microsoft.AspNetCore.SignalR;

namespace LibationWeb.Hubs;

/// <summary>
/// SignalR hub for real-time progress updates during download and import operations.
/// Clients connect to /hubs/progress to receive live status messages.
/// </summary>
public class ProgressHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("Connected", Context.ConnectionId);
        await base.OnConnectedAsync();
    }
}
