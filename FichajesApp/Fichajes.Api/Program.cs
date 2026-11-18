using System.Security.Claims;
using Fichajes.Api;
using Herensuge.Access.AspNetCore;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

// Fichajes.Api: la API de una aplicación del SaaS protegida por Herensuge. Solo la llama su BFF (Herensuge.Bff con
// la configuración de Fichajes), que le añade el access token del usuario: aquí no hay cookies ni sesiones.
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// 1. Herensuge: valida cada token (emisor, firma con su JWKS, audiencia "fichajes-api") y decide los permisos del
//    manifiesto (herensuge.access.json). Sin token válido: 401. Sin permiso: 403.
builder.Services.AddHerensugeAccess(options => builder.Configuration.GetSection("Herensuge").Bind(options));

// 2. La base de datos de la aplicación.
builder.Services.AddDbContext<FichajesDb>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("Fichajes")));
builder.Services.AddProblemDetails();

// 3. La IP del usuario llega al punto de decisión (redes de confianza de su organización). La API solo es accesible
//    desde la BFF, por la red interna de Docker: se confía en su X-Forwarded-For (un salto).
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
    options.ForwardLimit = 1;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// 4. Trazas, métricas y logs a Grafana (OTEL_EXPORTER_OTLP_ENDPOINT): un clic se sigue de la BFF a esta API, a su
//    base de datos y a Herensuge.
OpenTelemetryBuilder telemetry = builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("fichajes-api"))
    .WithTracing(tracing => tracing.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddNpgsql())
    .WithMetrics(metrics => metrics.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation())
    .WithLogging();
if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
{
    telemetry.UseOtlpExporter();
}

WebApplication app = builder.Build();

// Crea la tabla al arrancar si no existe (aplicación de ejemplo: sin migraciones).
await using (AsyncServiceScope scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<FichajesDb>().Database.EnsureCreatedAsync();
}

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

// Todo exige un usuario de una organización (tenant_id). Cada consulta filtra por ella: nadie ve otra.
RouteGroupBuilder shifts = app.MapGroup("/api/shifts")
    .RequireAuthorization(policy => policy.RequireClaim(HerensugeClaims.TenantId));

// Mis jornadas: las 30 últimas.
shifts.MapGet("/mine", async (ClaimsPrincipal user, FichajesDb db, CancellationToken cancellationToken) =>
{
    Employee me = Employee.From(user);
    List<Shift> mine = await db.Shifts.AsNoTracking()
        .Where(shift => shift.TenantId == me.TenantId && shift.UserId == me.UserId)
        .OrderByDescending(shift => shift.ClockIn)
        .Take(30)
        .ToListAsync(cancellationToken);
    return mine.Select(ShiftResponse.From);
}).RequirePermission("shift:read");

// Fichar: cierra la jornada abierta o abre una nueva.
shifts.MapPost("/clock", async (
    ClaimsPrincipal user,
    HttpRequest request,
    FichajesDb db,
    IHerensugeAccess herensuge,
    TimeProvider clock,
    CancellationToken cancellationToken) =>
{
    Employee me = Employee.From(user);
    Shift? shift = await db.Shifts.SingleOrDefaultAsync(
        open => open.TenantId == me.TenantId && open.UserId == me.UserId && open.ClockOut == null,
        cancellationToken);

    if (shift is not null)
    {
        shift.ClockOut = clock.GetUtcNow();
    }
    else
    {
        // El nombre lo da Herensuge con el mismo token del usuario: el navegador no puede ponerse otro.
        HerensugeUserInfo info = await herensuge.GetUserInfoAsync(BearerToken(request), cancellationToken);
        shift = new Shift
        {
            Id = Guid.CreateVersion7(),
            TenantId = me.TenantId,
            UserId = me.UserId,
            Name = info.Name ?? info.Email ?? me.UserId,
            ClockIn = clock.GetUtcNow(),
        };
        db.Shifts.Add(shift);
    }

    try
    {
        await db.SaveChangesAsync(cancellationToken);
    }
    catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
    {
        return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Ya hay una jornada abierta.");
    }

    return Results.Ok(ShiftResponse.From(shift));
}).RequirePermission("shift:create");

// El equipo: las jornadas de toda la organización en los últimos 7 días (rol Responsable).
shifts.MapGet("/team", async (ClaimsPrincipal user, FichajesDb db, TimeProvider clock, CancellationToken cancellationToken) =>
{
    Employee me = Employee.From(user);
    DateTimeOffset since = clock.GetUtcNow().AddDays(-7);
    List<Shift> team = await db.Shifts.AsNoTracking()
        .Where(shift => shift.TenantId == me.TenantId && shift.ClockIn >= since)
        .OrderByDescending(shift => shift.ClockIn)
        .Take(200)
        .ToListAsync(cancellationToken);
    return team.Select(ShiftResponse.From);
}).RequirePermission("shift:read-all");

// Borrar una jornada errónea. El manifiesto pide "recent_auth": sin un inicio de sesión de hace menos de 10 minutos,
// el SDK responde 401 con el reto de step-up (RFC 9470) y la web pide la contraseña otra vez.
shifts.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, FichajesDb db, CancellationToken cancellationToken) =>
{
    Employee me = Employee.From(user);
    int deleted = await db.Shifts
        .Where(shift => shift.Id == id && shift.TenantId == me.TenantId)
        .ExecuteDeleteAsync(cancellationToken);
    return deleted == 0 ? Results.NotFound() : Results.NoContent();
}).RequirePermission("shift:delete");

await app.RunAsync();

// El access token que ha validado el SDK (Authorization: Bearer …).
static string BearerToken(HttpRequest request)
{
    string header = request.Headers.Authorization.ToString();
    return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
        ? header["Bearer ".Length..].Trim()
        : throw new InvalidOperationException("The request has no bearer token.");
}
