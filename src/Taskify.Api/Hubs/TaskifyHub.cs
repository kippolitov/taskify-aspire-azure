using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Taskify.Shared.Realtime;

namespace Taskify.Api.Hubs;

/// <summary>
/// Live board updates. Connecting needs a valid identity (FR-028). An open connection outlives
/// its token: <c>CloseOnAuthenticationExpiration</c> stays off.
/// </summary>
[Authorize]
public class TaskifyHub : Hub<ITaskifyClient>
{
    public async Task JoinBoard(int projectId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"board-{projectId}");
    }

    public async Task LeaveBoard(int projectId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"board-{projectId}");
    }
}
