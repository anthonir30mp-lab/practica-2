namespace practica_2.Services
{
    public interface INotificadorSolicitudes
    {
        Task NotificarCambioEstadoAsync(string usuarioId, int solicitudId, string estado, string? motivoRechazo);
    }
}