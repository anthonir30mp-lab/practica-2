using System.Text.Json;
using RabbitMQ.Client;

namespace practica_2.Services;

public class RabbitMqPublisher : IRabbitMqPublisher, IAsyncDisposable
{
    private readonly string _connectionString;
    private readonly string _queueName;
    private readonly ILogger<RabbitMqPublisher> _logger;
    private IConnection? _connection;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public RabbitMqPublisher(IConfiguration configuration, ILogger<RabbitMqPublisher> logger)
    {
        _connectionString = configuration["RabbitMq:ConnectionString"]
            ?? throw new InvalidOperationException("Falta RabbitMq:ConnectionString en la configuración.");
        _queueName = configuration["RabbitMq:QueueName"] ?? "solicitudes.notificaciones";
        _logger = logger;
    }

    private async Task<IConnection> ObtenerConexionAsync()
    {
        if (_connection is { IsOpen: true })
        {
            return _connection;
        }

        await _lock.WaitAsync();
        try
        {
            if (_connection is { IsOpen: true })
            {
                return _connection;
            }

            var factory = new ConnectionFactory { Uri = new Uri(_connectionString) };
            _connection = await factory.CreateConnectionAsync();
            return _connection;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> PublicarSolicitudRegistradaAsync(int solicitudId, string usuarioId)
    {
        try
        {
            var connection = await ObtenerConexionAsync();

            var channelOptions = new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true);

            await using var channel = await connection.CreateChannelAsync(channelOptions);

            await channel.QueueDeclareAsync(
                queue: _queueName,
                durable: true,
                exclusive: false,
                autoDelete: false);

            var messageId = Guid.NewGuid();
            var payload = new
            {
                MessageId = messageId,
                SolicitudId = solicitudId,
                UsuarioId = usuarioId,
                FechaEventoUtc = DateTime.UtcNow
            };

            var body = JsonSerializer.SerializeToUtf8Bytes(payload);

            var props = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                MessageId = messageId.ToString()
            };

            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: _queueName,
                mandatory: true,
                basicProperties: props,
                body: body);

            _logger.LogInformation(
                "Mensaje SolicitudRegistrada publicado: {MessageId} para solicitud {SolicitudId}",
                messageId, solicitudId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error al publicar el evento SolicitudRegistrada para la solicitud {SolicitudId}. No se pudo encolar la notificación.",
                solicitudId);
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.CloseAsync();
            _connection.Dispose();
        }
    }
}