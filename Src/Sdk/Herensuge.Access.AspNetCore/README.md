# Herensuge.Access.AspNetCore

SDK de Herensuge para las APIs de las aplicaciones del SaaS (ASP.NET Core, .NET 10).

```csharp
builder.Services.AddHerensugeAccess(options =>
{
    options.Authority = "https://id.example.com/";
    options.ResourceServer = "facturas";      // "resourceServer" de herensuge.access.json → audiencia facturas-api
    options.ClientId = "facturas-app";        // cliente con el scope herensuge.access
    options.ClientSecret = builder.Configuration["Herensuge:ClientSecret"]!;
});

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/invoices", ...).RequirePermission("invoice:read");
```

| Pieza | Qué hace |
|---|---|
| `AddHerensugeAccess` | Valida los access tokens de Herensuge (OpenIddict.Validation: emisor, JWKS, audiencia `<app>-api`) |
| `.RequirePermission(p)` / `[RequirePermission(p)]` | `p` en el claim `permissions` → permite en local; si no → punto de decisión |
| `IHerensugeAccess.CheckAsync` | Pregunta al punto de decisión (AuthZEN) por un permiso, opcionalmente sobre un objeto, con la IP del cliente |
| *Step-up* | `step_up_required` → `401` con `WWW-Authenticate: Bearer error="insufficient_user_authentication"` (RFC 9470) |
| `IHerensugeAccess.GetUserInfoAsync(token)` | Nombre y email del usuario según Herensuge (`userinfo`): el access token no lleva datos personales |
| `BackchannelAddress` | Opcional (`http://herensuge-api:8080`): las llamadas a Herensuge van por la red interna, con `X-Forwarded-*`; el emisor sigue siendo el público |

El token `client_credentials` de la aplicación se cachea hasta un minuto antes de caducar. Si el punto de decisión no
responde en `Timeout` (5 s por defecto), se deniega.

Ejemplos: [`Samples/Facturas`](../../../Samples/Facturas/README.md) y la aplicación completa
[`FichajesApp`](../../../FichajesApp/README.md).
