namespace practica_2.Models;

/// <summary>
/// Parámetros de filtro para el catálogo de solicitudes.
/// Se vinculan vía query string (GET).
/// </summary>
public class SolicitudFiltroViewModel
{
    /// <summary>
    /// Filtro por estado. null = Todos.
    /// </summary>
    public EstadoSolicitud? Estado { get; set; }

    /// <summary>
    /// Monto mínimo solicitado.
    /// </summary>
    public decimal? MontoMinimo { get; set; }

    /// <summary>
    /// Monto máximo solicitado.
    /// </summary>
    public decimal? MontoMaximo { get; set; }

    /// <summary>
    /// Fecha de inicio del rango.
    /// </summary>
    public DateTime? FechaInicio { get; set; }

    /// <summary>
    /// Fecha de fin del rango.
    /// </summary>
    public DateTime? FechaFin { get; set; }
}
