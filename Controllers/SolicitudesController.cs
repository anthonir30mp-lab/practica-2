using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using practica_2.Data;
using practica_2.Models;
using practica_2.Services;

namespace practica_2.Controllers;

/// <summary>
/// Controlador para el catálogo de solicitudes de crédito del cliente autenticado.
/// </summary>
[Authorize]
public class SolicitudesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly SolicitudesCacheService _cacheService;

    public SolicitudesController(ApplicationDbContext context, SolicitudesCacheService cacheService)
    {
        _context = context;
        _cacheService = cacheService;
    }

    // ──────────────────────────────────────────────────────────────
    //  GET /Solicitudes?Estado=...&MontoMinimo=...&MontoMaximo=...&FechaInicio=...&FechaFin=...
    // ──────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] SolicitudFiltroViewModel filtro)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var cliente = await _context.Clientes
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UsuarioId == userId);

        var vm = new SolicitudesIndexViewModel { Filtro = filtro };

        if (cliente is null)
        {
            // El usuario no tiene perfil de cliente; devolver lista vacía.
            return View(vm);
        }

        // Determinar si hay filtros activos.
        bool hayFiltros = filtro.Estado.HasValue
                       || filtro.MontoMinimo.HasValue
                       || filtro.MontoMaximo.HasValue
                       || filtro.FechaInicio.HasValue
                       || filtro.FechaFin.HasValue;

        // ── Intentar cache (solo sin filtros) ────────────────────
        if (!hayFiltros)
        {
            var cacheado = await _cacheService.ObtenerListadoAsync<SolicitudCredito>(userId);
            if (cacheado is not null)
            {
                vm.Solicitudes = cacheado;
                return View(vm);
            }
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

        var resultados = await query
            .OrderByDescending(s => s.FechaSolicitud)
            .ToListAsync();

        vm.Solicitudes = resultados;

        // ── Guardar en cache (solo sin filtros) ──────────────────
        if (!hayFiltros)
        {
            await _cacheService.GuardarListadoAsync(userId, resultados);
        }

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

        // ── Guardar en sesión la última solicitud visitada ───────
        HttpContext.Session.SetInt32("UltimaSolicitudId", solicitud.Id);
        HttpContext.Session.SetString("UltimaSolicitudMonto", solicitud.MontoSolicitado.ToString("C"));

        return View(solicitud);
    }

    // ──────────────────────────────────────────────────────────────
    //  GET /Solicitudes/EstadoActual/5
    //  Usado por el cliente JS al reconectar el WebSocket, para
    //  recuperar el estado vigente por si hubo cambios mientras
    //  estuvo desconectado.
    // ──────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> EstadoActual(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var solicitud = await _context.SolicitudesCredito
            .AsNoTracking()
            .Include(s => s.Cliente)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (solicitud is null)
        {
            return NotFound();
        }

        if (solicitud.Cliente.UsuarioId != userId)
        {
            return Forbid();
        }

        return Json(new
        {
            solicitudId = solicitud.Id,
            estado = solicitud.Estado.ToString(),
            motivoRechazo = solicitud.MotivoRechazo
        });
    }

    // ──────────────────────────────────────────────────────────────
    //  GET /Solicitudes/Create
    // ──────────────────────────────────────────────────────────────
    [HttpGet]
    public IActionResult Create()
    {
        return View(new CrearSolicitudViewModel());
    }

    // ──────────────────────────────────────────────────────────────
    //  POST /Solicitudes/Create
    // ──────────────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CrearSolicitudViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            return View(vm);
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        // ── Obtener el cliente asociado al usuario ───────────────
        var cliente = await _context.Clientes
            .FirstOrDefaultAsync(c => c.UsuarioId == userId);

        if (cliente is null)
        {
            ModelState.AddModelError(string.Empty,
                "No se encontró un perfil de cliente asociado a su cuenta.");
            return View(vm);
        }

        // ── Regla: el cliente debe estar activo ──────────────────
        if (!cliente.Activo)
        {
            ModelState.AddModelError(string.Empty,
                "Su cuenta de cliente no está activa. No puede crear solicitudes.");
            return View(vm);
        }

        // ── Regla: no puede haber otra solicitud Pendiente ───────
        var tienePendiente = await _context.SolicitudesCredito
            .AnyAsync(s => s.ClienteId == cliente.Id
                        && s.Estado == EstadoSolicitud.Pendiente);

        if (tienePendiente)
        {
            ModelState.AddModelError(string.Empty,
                "Ya tiene una solicitud en estado Pendiente. " +
                "Debe esperar a que sea resuelta antes de crear otra.");
            return View(vm);
        }

        // ── Regla: monto ≤ 10 × IngresosMensuales ───────────────
        var montoLimite = 10 * cliente.IngresosMensuales;
        if (vm.MontoSolicitado > montoLimite)
        {
            ModelState.AddModelError(nameof(vm.MontoSolicitado),
                $"El monto solicitado no puede superar 10 veces sus ingresos mensuales " +
                $"({montoLimite:C}).");
            return View(vm);
        }

        // ── Crear la solicitud ───────────────────────────────────
        var solicitud = new SolicitudCredito
        {
            ClienteId = cliente.Id,
            MontoSolicitado = vm.MontoSolicitado,
            FechaSolicitud = DateTime.UtcNow,
            Estado = EstadoSolicitud.Pendiente
        };

        _context.SolicitudesCredito.Add(solicitud);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (InvalidOperationException ex)
        {
            // Captura las reglas de negocio del DbContext como fallback.
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(vm);
        }

        // ── Invalidar cache del listado ──────────────────────────
        await _cacheService.InvalidarListadoAsync(userId ?? string.Empty);

        TempData["Exito"] = "Su solicitud de crédito fue registrada exitosamente.";
        return RedirectToAction(nameof(Index));
    }
}