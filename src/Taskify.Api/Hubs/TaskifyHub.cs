using Microsoft.AspNetCore.SignalR;

namespace Taskify.Api.Hubs;

public class TaskifyHub : Hub
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
