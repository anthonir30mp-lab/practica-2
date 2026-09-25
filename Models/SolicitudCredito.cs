using System.ComponentModel.DataAnnotations;

namespace practica_2.Models;

/// <summary>
/// Representa una solicitud de crédito realizada por un cliente.
/// </summary>
public class SolicitudCredito
{
    public int Id { get; set; }

    /// <summary>
    /// FK al cliente solicitante.
    /// </summary>
    [Required]
    public int ClienteId { get; set; }

    /// <summary>
    /// Navegación al cliente.
    /// </summary>
    public Cliente Cliente { get; set; } = null!;

    /// <summary>
    /// Monto solicitado en la solicitud de crédito. Debe ser mayor a 0.
    /// </summary>
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "MontoSolicitado debe ser mayor a 0.")]
    public decimal MontoSolicitado { get; set; }

    /// <summary>
    /// Fecha en la que se creó la solicitud.
    /// </summary>
    [Required]
    public DateTime FechaSolicitud { get; set; }

    /// <summary>
    /// Estado actual de la solicitud.
    /// </summary>
    [Required]
    public EstadoSolicitud Estado { get; set; }

    /// <summary>
    /// Motivo por el que la solicitud fue rechazada (null si no aplica).
    /// </summary>
    public string? MotivoRechazo { get; set; }
}
