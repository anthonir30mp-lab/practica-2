using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using practica_2.Models;

namespace practica_2.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext(options)
{
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<SolicitudCredito> SolicitudesCredito => Set<SolicitudCredito>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ── Cliente ──────────────────────────────────────────────
        builder.Entity<Cliente>(entity =>
        {
            entity.HasKey(c => c.Id);

            entity.Property(c => c.IngresosMensuales)
                  .HasColumnType("decimal(18,2)")
                  .IsRequired();

            // Relación Cliente → IdentityUser (uno a uno / muchos a uno)
            entity.HasOne(c => c.Usuario)
                  .WithMany()
                  .HasForeignKey(c => c.UsuarioId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Índice único: un usuario solo puede tener un registro de Cliente
            entity.HasIndex(c => c.UsuarioId).IsUnique();
        });

        // ── SolicitudCredito ─────────────────────────────────────
        builder.Entity<SolicitudCredito>(entity =>
        {
            entity.HasKey(s => s.Id);

            entity.Property(s => s.MontoSolicitado)
                  .HasColumnType("decimal(18,2)")
                  .IsRequired();

            entity.Property(s => s.FechaSolicitud)
                  .IsRequired();

            entity.Property(s => s.Estado)
                  .IsRequired()
                  .HasConversion<string>()       // almacenar como texto legible
                  .HasMaxLength(20);

            entity.Property(s => s.MotivoRechazo)
                  .HasMaxLength(500);

            // Relación SolicitudCredito → Cliente
            entity.HasOne(s => s.Cliente)
                  .WithMany(c => c.Solicitudes)
                  .HasForeignKey(s => s.ClienteId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Índice único filtrado: un cliente solo puede tener
            // una solicitud en estado Pendiente a la vez.
            // SQLite no soporta HasFilter, por lo que esta restricción
            // se aplica en SaveChanges / SaveChangesAsync.
        });
    }

    // ══════════════════════════════════════════════════════════════
    //  Reglas de negocio validadas en el DbContext antes de persistir
    // ══════════════════════════════════════════════════════════════

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ValidarReglasDeNegocio();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ValidarReglasDeNegocio();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ValidarReglasDeNegocio()
    {
        var solicitudesModificadas = ChangeTracker.Entries<SolicitudCredito>()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified)
            .Select(e => e.Entity)
            .ToList();

        foreach (var solicitud in solicitudesModificadas)
        {
            // Regla 1: Un cliente solo puede tener una solicitud Pendiente a la vez.
            if (solicitud.Estado == EstadoSolicitud.Pendiente)
            {
                var yaExistePendiente = SolicitudesCredito
                    .AsNoTracking()
                    .Any(s => s.ClienteId == solicitud.ClienteId
                           && s.Estado == EstadoSolicitud.Pendiente
                           && s.Id != solicitud.Id);

                if (yaExistePendiente)
                {
                    throw new InvalidOperationException(
                        $"El cliente {solicitud.ClienteId} ya tiene una solicitud en estado Pendiente.");
                }
            }

            // Regla 2: No se puede aprobar si MontoSolicitado > 5 × IngresosMensuales.
            if (solicitud.Estado == EstadoSolicitud.Aprobado)
            {
                // Intentamos obtener el cliente desde el tracker o la BD.
                var cliente = ChangeTracker.Entries<Cliente>()
                    .FirstOrDefault(e => e.Entity.Id == solicitud.ClienteId)?.Entity
                    ?? Clientes.AsNoTracking().FirstOrDefault(c => c.Id == solicitud.ClienteId);

                if (cliente is null)
                {
                    throw new InvalidOperationException(
                        $"No se encontró el cliente {solicitud.ClienteId} para validar la aprobación.");
                }

                if (solicitud.MontoSolicitado > 5 * cliente.IngresosMensuales)
                {
                    throw new InvalidOperationException(
                        $"No se puede aprobar la solicitud: el monto solicitado ({solicitud.MontoSolicitado:C}) " +
                        $"supera 5 veces los ingresos mensuales del cliente ({cliente.IngresosMensuales:C}).");
                }
            }
        }
    }
}
