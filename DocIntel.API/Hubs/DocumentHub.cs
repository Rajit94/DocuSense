using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace DocIntel.API.Hubs;

[Authorize]
public class DocumentHub : Hub
{

    public override async Task OnConnectedAsync()
    {
        var workspaceId = Context.User?
            .FindFirst("WorkspaceId")?.Value;

        if (!string.IsNullOrEmpty(workspaceId))
        {
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                workspaceId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var workspaceId = Context.User?
            .FindFirst("WorkspaceId")?.Value;

        if (!string.IsNullOrEmpty(workspaceId))
        {
            await Groups.RemoveFromGroupAsync(
                Context.ConnectionId,
                workspaceId);
        }

        await base.OnDisconnectedAsync(exception);
    }
}