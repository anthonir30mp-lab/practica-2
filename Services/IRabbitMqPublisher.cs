namespace practica_2.Services;

public interface IRabbitMqPublisher
{
    Task<bool> PublicarSolicitudRegistradaAsync(int solicitudId, string usuarioId);
}