using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace practica_2.Models;

/// <summary>
/// Representa un cliente del sistema de créditos.
/// </summary>
public class Cliente
{
    public int Id { get; set; }

    /// <summary>
    /// FK al usuario de ASP.NET Identity.
    /// </summary>
    [Required]
    public string UsuarioId { get; set; } = null!;

    /// <summary>
    /// Navegación al usuario de Identity.
    /// </summary>
    public IdentityUser Usuario { get; set; } = null!;

    /// <summary>
    /// Ingresos mensuales del cliente. Debe ser mayor a 0.
    /// </summary>
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "IngresosMensuales debe ser mayor a 0.")]
    public decimal IngresosMensuales { get; set; }

    /// <summary>
    /// Indica si el cliente está activo en el sistema.
    /// </summary>
    public bool Activo { get; set; }

    /// <summary>
    /// Solicitudes de crédito asociadas al cliente.
    /// </summary>
    public ICollection<SolicitudCredito> Solicitudes { get; set; } = new List<SolicitudCredito>();
}
