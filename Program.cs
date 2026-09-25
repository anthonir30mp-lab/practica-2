using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using practica_2.Data;
using practica_2.Models;
using practica_2.Hubs;
using practica_2.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>()                     // ← habilitar roles
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddControllersWithViews();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
});

// ══════════════════════════════════════════════════════════════
//  Distributed Cache: Redis con fallback a memoria
// ══════════════════════════════════════════════════════════════
var redisConnectionString = builder.Configuration["Redis:ConnectionString"];

if (!string.IsNullOrWhiteSpace(redisConnectionString))
{
    try
    {
        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnectionString;
            options.InstanceName = "practica2_";
        });
    }
    catch
    {
        // Si la configuración falla, usar caché en memoria.
        builder.Services.AddDistributedMemoryCache();
    }
}
else
{
    // Sin Redis configurado → caché en memoria como fallback.
    builder.Services.AddDistributedMemoryCache();
}

// ── Servicio de cache de solicitudes ─────────────────────────
builder.Services.AddScoped<practica_2.Services.SolicitudesCacheService>();

// ══════════════════════════════════════════════════════════════
//  Sesión (Redis-backed o in-memory)
// ══════════════════════════════════════════════════════════════
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// ══════════════════════════════════════════════════════════════
//  SignalR: Hub de notificaciones de solicitudes
// ══════════════════════════════════════════════════════════════
builder.Services.AddSignalR();
builder.Services.AddScoped<INotificadorSolicitudes, NotificadorSolicitudes>();

var app = builder.Build();

// ══════════════════════════════════════════════════════════════
//  Seed de datos inicial
// ══════════════════════════════════════════════════════════════
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    await SeedDataAsync(services);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.UseSession();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

app.MapHub<SolicitudesHub>("/hubs/solicitudes");

app.Run();

// ══════════════════════════════════════════════════════════════
//  Método Seed
// ══════════════════════════════════════════════════════════════
static async Task SeedDataAsync(IServiceProvider services)
{
    var context = services.GetRequiredService<ApplicationDbContext>();
    var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

    // Asegurar que la BD está actualizada
    await context.Database.MigrateAsync();

    // ── 1. Crear rol "Analista" si no existe ────────────────────
    const string rolAnalista = "Analista";
    if (!await roleManager.RoleExistsAsync(rolAnalista))
    {
        await roleManager.CreateAsync(new IdentityRole(rolAnalista));
    }

    // ── 2. Crear usuario Analista si no existe ──────────────────
    const string analistaEmail = "analista@ejemplo.com";
    var analistaUser = await userManager.FindByEmailAsync(analistaEmail);
    if (analistaUser is null)
    {
        analistaUser = new IdentityUser
        {
            UserName = analistaEmail,
            Email = analistaEmail,
            EmailConfirmed = true
        };
        var result = await userManager.CreateAsync(analistaUser, "Analista123!");
        if (!result.Succeeded)
        {
            throw new Exception($"Error al crear usuario Analista: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
    }
    if (!await userManager.IsInRoleAsync(analistaUser, rolAnalista))
    {
        await userManager.AddToRoleAsync(analistaUser, rolAnalista);
    }

    // ── 3. Crear usuarios de ejemplo para los clientes ──────────
    const string cliente1Email = "cliente1@ejemplo.com";
    const string cliente2Email = "cliente2@ejemplo.com";

    var clienteUser1 = await userManager.FindByEmailAsync(cliente1Email);
    if (clienteUser1 is null)
    {
        clienteUser1 = new IdentityUser
        {
            UserName = cliente1Email,
            Email = cliente1Email,
            EmailConfirmed = true
        };
        await userManager.CreateAsync(clienteUser1, "Cliente1_Pass!");
    }

    var clienteUser2 = await userManager.FindByEmailAsync(cliente2Email);
    if (clienteUser2 is null)
    {
        clienteUser2 = new IdentityUser
        {
            UserName = cliente2Email,
            Email = cliente2Email,
            EmailConfirmed = true
        };
        await userManager.CreateAsync(clienteUser2, "Cliente2_Pass!");
    }

    // ── 4. Crear clientes de ejemplo ────────────────────────────
    if (!await context.Clientes.AnyAsync())
    {
        var cliente1 = new Cliente
        {
            UsuarioId = clienteUser1!.Id,
            IngresosMensuales = 15_000m,
            Activo = true
        };

        var cliente2 = new Cliente
        {
            UsuarioId = clienteUser2!.Id,
            IngresosMensuales = 25_000m,
            Activo = true
        };

        context.Clientes.AddRange(cliente1, cliente2);
        await context.SaveChangesAsync();

        // ── 5. Crear solicitudes de ejemplo ─────────────────────
        // Solicitud Pendiente para cliente 1
        var solicitudPendiente = new SolicitudCredito
        {
            ClienteId = cliente1.Id,
            MontoSolicitado = 30_000m,   // ≤ 5 × 15,000 = 75,000 ✓
            FechaSolicitud = DateTime.UtcNow,
            Estado = EstadoSolicitud.Pendiente
        };

        // Solicitud Aprobada para cliente 2
        var solicitudAprobada = new SolicitudCredito
        {
            ClienteId = cliente2.Id,
            MontoSolicitado = 50_000m,   // ≤ 5 × 25,000 = 125,000 ✓
            FechaSolicitud = DateTime.UtcNow.AddDays(-5),
            Estado = EstadoSolicitud.Aprobado
        };

        context.SolicitudesCredito.AddRange(solicitudPendiente, solicitudAprobada);
        await context.SaveChangesAsync();
    }
}