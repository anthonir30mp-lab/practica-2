using Microsoft.AspNetCore.SignalR;
using practica_2.Hubs;

namespace practica_2.Services
{
    public class NotificadorSolicitudes : INotificadorSolicitudes
    {
        private readonly IHubContext<SolicitudesHub> _hubContext;

        public NotificadorSolicitudes(IHubContext<SolicitudesHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task NotificarCambioEstadoAsync(string usuarioId, int solicitudId, string estado, string? motivoRechazo)
        {
            await _hubContext.Clients.Group(usuarioId).SendAsync(
                "SolicitudEstadoActualizado",
                new
                {
                    SolicitudId = solicitudId,
                    Estado = estado,
                    MotivoRechazo = motivoRechazo
                });
        }
    }
}