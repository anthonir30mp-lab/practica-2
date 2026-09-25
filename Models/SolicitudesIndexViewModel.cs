namespace practica_2.Models;

/// <summary>
/// ViewModel compuesto para la vista Index de solicitudes.
/// Contiene los filtros aplicados, los resultados y los mensajes de error de validación.
/// </summary>
public class SolicitudesIndexViewModel
{
    /// <summary>
    /// Parámetros de filtro actuales (para mantener estado en el formulario).
    /// </summary>
    public SolicitudFiltroViewModel Filtro { get; set; } = new();

    /// <summary>
    /// Solicitudes que cumplen con los filtros aplicados.
    /// </summary>
    public IReadOnlyList<SolicitudCredito> Solicitudes { get; set; } = [];

    /// <summary>
    /// Mensajes de error de validación de los filtros (no rompen la página,
    /// simplemente informan que cierto filtro fue ignorado).
    /// </summary>
    public List<string> ErroresFiltro { get; set; } = [];
}
