using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using practica_2.Data;
using practica_2.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace practica_2.Services;

public class RabbitMqConsumerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RabbitMqConsumerService> _logger;

    private IConnection? _connection;
    private IChannel? _channel;

    public RabbitMqConsumerService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<RabbitMqConsumerService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        var habilitado = _configuration.GetValue<bool?>("RabbitMq:ConsumerEnabled") ?? true;

        if (!habilitado)
        {
            _logger.LogWarning("RabbitMqConsumerService deshabilitado por configuración (RabbitMq:ConsumerEnabled=false). No se consumirán mensajes.");
            return;
        }

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var habilitado = _configuration.GetValue<bool?>("RabbitMq:ConsumerEnabled") ?? true;
        if (!habilitado)
        {
            return;
        }

        var connectionString = _configuration["RabbitMq:ConnectionString"]
            ?? throw new InvalidOperationException("Falta RabbitMq:ConnectionString en la configuración.");
        var queueName = _configuration["RabbitMq:QueueName"] ?? "solicitudes.notificaciones";

        var factory = new ConnectionFactory { Uri = new Uri(connectionString) };

        _connection = await factory.CreateConnectionAsync(stoppingToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await _channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: stoppingToken);

        // Procesar un mensaje a la vez antes de recibir el siguiente ACK.
        await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            await ProcesarMensajeAsync(ea, stoppingToken);
        };

        await _channel.BasicConsumeAsync(
            queue: queueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        _logger.LogInformation("RabbitMqConsumerService iniciado, escuchando la cola '{QueueName}'.", queueName);

        // Mantener vivo el servicio hasta que se solicite detener.
        await Task.Delay(Timeout.Infinite, stoppingToken).ContinueWith(_ => { });
    }

    private async Task ProcesarMensajeAsync(BasicDeliverEventArgs ea, CancellationToken stoppingToken)
    {
        var body = ea.Body.ToArray();

        try
        {
            var json = Encoding.UTF8.GetString(body);
            var mensaje = JsonSerializer.Deserialize<SolicitudRegistradaMensaje>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (mensaje is null || mensaje.MessageId == Guid.Empty || mensaje.SolicitudId <= 0
                || string.IsNullOrWhiteSpace(mensaje.UsuarioId))
            {
                _logger.LogError("Mensaje inválido recibido (campos faltantes o vacíos): {Json}", json);
                await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, stoppingToken);
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // ── Control de duplicados: si el MessageId ya fue procesado, solo ACK ──
            var yaExiste = await context.Notificaciones
                .AsNoTracking()
                .AnyAsync(n => n.MessageId == mensaje.MessageId, stoppingToken);

            if (yaExiste)
            {
                _logger.LogInformation("MessageId {MessageId} ya fue procesado anteriormente. Confirmando sin insertar de nuevo.", mensaje.MessageId);
                await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false, stoppingToken);
                return;
            }

            var notificacion = new Notificacion
            {
                MessageId = mensaje.MessageId,
                SolicitudId = mensaje.SolicitudId,
                UsuarioId = mensaje.UsuarioId,
                Texto = "Recibimos tu solicitud de crédito y está pendiente de evaluación",
                FechaProcesamientoUtc = DateTime.UtcNow
            };

            context.Notificaciones.Add(notificacion);
            await context.SaveChangesAsync(stoppingToken);

            // ── ACK solo después de guardar exitosamente ──
            await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false, stoppingToken);

            _logger.LogInformation("Notificación guardada para MessageId {MessageId}, solicitud {SolicitudId}.",
                mensaje.MessageId, mensaje.SolicitudId);
        }
        catch (DbUpdateException dbEx) when (dbEx.InnerException?.Message.Contains("UNIQUE") == true)
        {
            // Redelivery justo en el borde de la comprobación AnyAsync: confirmar sin duplicar.
            _logger.LogWarning("Índice único de MessageId evitó una duplicación por redelivery. Confirmando mensaje.");
            await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false, stoppingToken);
        }
        catch (JsonException jsonEx)
        {
            _logger.LogError(jsonEx, "Mensaje con JSON inválido, no se puede deserializar. Se rechaza sin reencolar.");
            await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, stoppingToken);
        }
        catch (Exception ex)
        {
            // Falla de procesamiento: no confirmar como exitoso. No reencolar para
            // evitar reintentos infinitos; el reenvío se documenta como manual.
            _logger.LogError(ex, "Error al procesar el mensaje. Se rechaza sin reencolar; requiere reenvío manual con el mismo MessageId.");
            await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null)
        {
            await _channel.CloseAsync(cancellationToken);
        }

        if (_connection is not null)
        {
            await _connection.CloseAsync(cancellationToken);
        }

        await base.StopAsync(cancellationToken);
    }

    private class SolicitudRegistradaMensaje
    {
        public Guid MessageId { get; set; }
        public int SolicitudId { get; set; }
        public string UsuarioId { get; set; } = string.Empty;
        public DateTime FechaEventoUtc { get; set; }
    }
}