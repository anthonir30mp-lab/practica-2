using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using practica_2.Data;
using practica_2.Models;

namespace practica_2.Controllers;

/// <summary>
/// Controlador para el catálogo de solicitudes de crédito del cliente autenticado.
/// </summary>
[Authorize]
public class SolicitudesController : Controller
{
    private readonly ApplicationDbContext _context;

    public SolicitudesController(ApplicationDbContext context)
    {
        _context = context;
    }

    // ──────────────────────────────────────────────────────────────
    //  GET /Solicitudes?Estado=...&MontoMinimo=...&MontoMaximo=...&FechaInicio=...&FechaFin=...
    // ──────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] SolicitudFiltroViewModel filtro)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var cliente = await _context.Clientes
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UsuarioId == userId);

        var vm = new SolicitudesIndexViewModel { Filtro = filtro };

        if (cliente is null)
        {
            // El usuario no tiene perfil de cliente; devolver lista vacía.
            return View(vm);
        }

        // Consulta base: solo solicitudes del cliente autenticado.
        IQueryable<SolicitudCredito> query = _context.SolicitudesCredito
            .AsNoTracking()
            .Where(s => s.ClienteId == cliente.Id);

        // ── Filtro por Estado ────────────────────────────────────
        if (filtro.Estado.HasValue)
        {
            query = query.Where(s => s.Estado == filtro.Estado.Value);
        }

        // ── Filtro por rango de monto ────────────────────────────
        bool montoMinimoInvalido = filtro.MontoMinimo.HasValue && filtro.MontoMinimo.Value < 0;
        bool montoMaximoInvalido = filtro.MontoMaximo.HasValue && filtro.MontoMaximo.Value < 0;

        if (montoMinimoInvalido || montoMaximoInvalido)
        {
            vm.ErroresFiltro.Add("El monto mínimo y máximo no pueden ser negativos. Se ignoró el filtro de monto.");
        }
        else
        {
            if (filtro.MontoMinimo.HasValue)
            {
                query = query.Where(s => s.MontoSolicitado >= filtro.MontoMinimo.Value);
            }

            if (filtro.MontoMaximo.HasValue)
            {
                query = query.Where(s => s.MontoSolicitado <= filtro.MontoMaximo.Value);
            }
        }

        // ── Filtro por rango de fechas ───────────────────────────
        if (filtro.FechaInicio.HasValue && filtro.FechaFin.HasValue
            && filtro.FechaInicio.Value > filtro.FechaFin.Value)
        {
            vm.ErroresFiltro.Add("La fecha de inicio no puede ser mayor a la fecha de fin. Se ignoró el filtro de fechas.");
        }
        else
        {
            if (filtro.FechaInicio.HasValue)
            {
                query = query.Where(s => s.FechaSolicitud >= filtro.FechaInicio.Value);
            }

            if (filtro.FechaFin.HasValue)
            {
                // Incluir todo el día de FechaFin.
                var finDelDia = filtro.FechaFin.Value.Date.AddDays(1);
                query = query.Where(s => s.FechaSolicitud < finDelDia);
            }
        }

        vm.Solicitudes = await query
            .OrderByDescending(s => s.FechaSolicitud)
            .ToListAsync();

        return View(vm);
    }

    // ──────────────────────────────────────────────────────────────
    //  GET /Solicitudes/Details/5
    // ──────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Details(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var solicitud = await _context.SolicitudesCredito
            .AsNoTracking()
            .Include(s => s.Cliente)
            .FirstOrDefaultAsync(s => s.Id == id.Value);

        if (solicitud is null)
        {
            return NotFound();
        }

        // Verificar que la solicitud pertenece al usuario autenticado.
        if (solicitud.Cliente.UsuarioId != userId)
        {
            return Forbid();
        }

        return View(solicitud);
    }
}
