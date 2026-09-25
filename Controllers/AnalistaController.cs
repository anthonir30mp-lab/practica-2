using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using practica_2.Data;
using practica_2.Models;
using practica_2.Services;

namespace practica_2.Controllers;

/// <summary>
/// Panel de gestión para usuarios con rol Analista.
/// Permite ver solicitudes pendientes, aprobarlas o rechazarlas.
/// </summary>
[Authorize(Roles = "Analista")]
public class AnalistaController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly SolicitudesCacheService _cacheService;

    public AnalistaController(ApplicationDbContext context, SolicitudesCacheService cacheService)
    {
        _context = context;
        _cacheService = cacheService;
    }

    // ──────────────────────────────────────────────────────────────
    //  GET /Analista
    // ──────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var solicitudesPendientes = await _context.SolicitudesCredito
            .AsNoTracking()
            .Include(s => s.Cliente)
                .ThenInclude(c => c.Usuario)
            .Where(s => s.Estado == EstadoSolicitud.Pendiente)
            .OrderBy(s => s.FechaSolicitud)
            .ToListAsync();

        return View(solicitudesPendientes);
    }

    // ──────────────────────────────────────────────────────────────
    //  POST /Analista/Aprobar
    // ──────────────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Aprobar(int id)
    {
        var solicitud = await _context.SolicitudesCredito
            .Include(s => s.Cliente)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (solicitud is null)
        {
            TempData["Error"] = "La solicitud no fue encontrada.";
            return RedirectToAction(nameof(Index));
        }

        // ── Validar que siga en estado Pendiente ─────────────────
        if (solicitud.Estado != EstadoSolicitud.Pendiente)
        {
            TempData["Error"] = $"La solicitud #{id} ya fue procesada (estado actual: {solicitud.Estado}). No se puede aprobar nuevamente.";
            return RedirectToAction(nameof(Index));
        }

        // ── Validar regla de 5× ingresos ────────────────────────
        if (solicitud.MontoSolicitado > 5 * solicitud.Cliente.IngresosMensuales)
        {
            TempData["Error"] = $"No se puede aprobar la solicitud #{id}: el monto solicitado " +
                $"({solicitud.MontoSolicitado:C}) supera 5 veces los ingresos mensuales " +
                $"del cliente ({solicitud.Cliente.IngresosMensuales:C}).";
            return RedirectToAction(nameof(Index));
        }

        // ── Aprobar ──────────────────────────────────────────────
        solicitud.Estado = EstadoSolicitud.Aprobado;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }

        // Invalidar cache del listado del cliente afectado
        await _cacheService.InvalidarListadoAsync(solicitud.Cliente.UsuarioId);

        TempData["Exito"] = $"La solicitud #{id} fue aprobada exitosamente.";
        return RedirectToAction(nameof(Index));
    }

    // ──────────────────────────────────────────────────────────────
    //  POST /Analista/Rechazar
    // ──────────────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rechazar(int id, string motivoRechazo)
    {
        // ── Validar MotivoRechazo no vacío ───────────────────────
        if (string.IsNullOrWhiteSpace(motivoRechazo))
        {
            TempData["Error"] = $"Debe indicar un motivo de rechazo para la solicitud #{id}.";
            return RedirectToAction(nameof(Index));
        }

        var solicitud = await _context.SolicitudesCredito
            .Include(s => s.Cliente)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (solicitud is null)
        {
            TempData["Error"] = "La solicitud no fue encontrada.";
            return RedirectToAction(nameof(Index));
        }

        // ── Validar que siga en estado Pendiente ─────────────────
        if (solicitud.Estado != EstadoSolicitud.Pendiente)
        {
            TempData["Error"] = $"La solicitud #{id} ya fue procesada (estado actual: {solicitud.Estado}). No se puede rechazar nuevamente.";
            return RedirectToAction(nameof(Index));
        }

        // ── Rechazar ─────────────────────────────────────────────
        solicitud.Estado = EstadoSolicitud.Rechazado;
        solicitud.MotivoRechazo = motivoRechazo;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }

        // Invalidar cache del listado del cliente afectado
        await _cacheService.InvalidarListadoAsync(solicitud.Cliente.UsuarioId);

        TempData["Exito"] = $"La solicitud #{id} fue rechazada.";
        return RedirectToAction(nameof(Index));
    }
}
