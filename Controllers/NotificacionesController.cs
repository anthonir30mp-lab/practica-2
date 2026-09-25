using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using practica_2.Data;

namespace practica_2.Controllers;

[Authorize]
public class NotificacionesController : Controller
{
    private readonly ApplicationDbContext _context;

    public NotificacionesController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var notificaciones = await _context.Notificaciones
            .AsNoTracking()
            .Where(n => n.UsuarioId == userId)
            .OrderByDescending(n => n.FechaProcesamientoUtc)
            .ToListAsync();

        return View(notificaciones);
    }
}