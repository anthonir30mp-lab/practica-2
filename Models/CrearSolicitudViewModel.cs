using System.ComponentModel.DataAnnotations;

namespace practica_2.Models;

/// <summary>
/// ViewModel para el formulario de creación de una nueva solicitud de crédito.
/// </summary>
public class CrearSolicitudViewModel
{
    /// <summary>
    /// Monto solicitado por el cliente. Debe ser mayor a 0.
    /// </summary>
    [Required(ErrorMessage = "El monto solicitado es obligatorio.")]
    [Range(0.01, double.MaxValue, ErrorMessage = "El monto solicitado debe ser mayor a 0.")]
    [Display(Name = "Monto solicitado")]
    public decimal MontoSolicitado { get; set; }
}
