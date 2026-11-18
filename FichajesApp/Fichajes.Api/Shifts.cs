using System.Security.Claims;
using Herensuge.Access.AspNetCore;
using Microsoft.EntityFrameworkCore;

namespace Fichajes.Api;

/// <summary>Una jornada: de la entrada (fichar) a la salida (fichar otra vez). Siempre de una organización.</summary>
internal sealed class Shift
{
    public Guid Id { get; init; }

    /// <summary>tenant_id del token: la organización. Todas las consultas filtran por ella.</summary>
    public Guid TenantId { get; init; }

    /// <summary>sub del token: el usuario en Herensuge.</summary>
    public required string UserId { get; init; }

    /// <summary>Nombre del usuario al fichar la entrada, según Herensuge (userinfo), nunca según el navegador.</summary>
    public required string Name { get; init; }

    public DateTimeOffset ClockIn { get; init; }

    public DateTimeOffset? ClockOut { get; set; }
}

/// <param name="Minutes">Minutos trabajados (los calcula el servidor); null mientras la jornada está abierta.</param>
internal sealed record ShiftResponse(Guid Id, string Name, DateTimeOffset ClockIn, DateTimeOffset? ClockOut, int? Minutes)
{
    public static ShiftResponse From(Shift shift) => new(
        shift.Id,
        shift.Name,
        shift.ClockIn,
        shift.ClockOut,
        shift.ClockOut is { } end ? (int)(end - shift.ClockIn).TotalMinutes : null);
}

/// <summary>Quién llama: el usuario (sub) en su organización (tenant_id), del access token de Herensuge.</summary>
internal sealed record Employee(Guid TenantId, string UserId)
{
    public static Employee From(ClaimsPrincipal user) => new(
        Guid.Parse(user.FindFirstValue(HerensugeClaims.TenantId)!),
        user.FindFirstValue(HerensugeClaims.Subject)!);
}

internal sealed class FichajesDb(DbContextOptions<FichajesDb> options) : DbContext(options)
{
    public DbSet<Shift> Shifts => Set<Shift>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<Shift>(shift =>
    {
        shift.ToTable("shifts");
        shift.Property(s => s.UserId).HasMaxLength(100);
        shift.Property(s => s.Name).HasMaxLength(200);

        // Una sola jornada abierta por persona: dos clics a la vez no abren dos.
        shift.HasIndex(s => new { s.TenantId, s.UserId }).IsUnique().HasFilter("\"ClockOut\" IS NULL");
        shift.HasIndex(s => new { s.TenantId, s.ClockIn });
    });
}
