using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace practica_2.Hubs
{
    [Authorize]
    public class SolicitudesHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            var usuarioId = Context.UserIdentifier;

            if (!string.IsNullOrEmpty(usuarioId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, usuarioId);
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var usuarioId = Context.UserIdentifier;

            if (!string.IsNullOrEmpty(usuarioId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, usuarioId);
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}