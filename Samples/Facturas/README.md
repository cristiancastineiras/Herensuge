# Facturas: una aplicación del SaaS con Herensuge

Ejemplo mínimo de una aplicación que delega en Herensuge el inicio de sesión **y** los permisos:

| Pieza | Qué es |
|---|---|
| `herensuge.access.json` | Manifiesto: permisos (`invoice:read`, `invoice:approve` con MFA, `invoice:delete` con login reciente) y plantillas de rol |
| `Facturas.Api` | API mínima con el SDK `Herensuge.Access.AspNetCore` |
| `bff.appsettings.json` | Configuración para usar `Herensuge.Bff` como BFF de Facturas (sesión, refresco, `permissions-changed`) |

## 1. Alta en Herensuge

1. **Cliente OIDC** (portal → Plataforma → Clientes OIDC): `facturas-app`, confidencial, redirect
   `https://<bff-de-facturas>/bff/callback`, scopes `openid profile email offline_access facturas.api herensuge.access`.
   Guarda el secreto.
2. **Manifiesto**: pégalo en Plataforma → Aplicaciones, o publícalo desde la CI de la aplicación con su propio token:

   ```bash
   TOKEN=$(curl -s https://id.example.com/connect/token \
     -d grant_type=client_credentials -d scope=herensuge.access \
     -d client_id=facturas-app -d client_secret="$SECRET" | jq -r .access_token)
   curl -X PUT https://id.example.com/access/v1/manifest \
     -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
     --data @herensuge.access.json
   ```

   Herensuge crea el scope `facturas.api` y la audiencia `facturas-api`. Publicar el mismo manifiesto otra vez no
   cambia nada (hash canónico).

## 2. La API

```csharp
builder.Services.AddHerensugeAccess(options => builder.Configuration.GetSection("Herensuge").Bind(options));
...
api.MapGet("/", ...).RequirePermission("invoice:read");
```

- Valida el token (emisor, firma con el JWKS de Herensuge, audiencia `facturas-api`).
- **Regla del SDK:** si el permiso está en el claim `permissions`, permite sin llamar a nadie. Si no (condicional, sobre
  un objeto o no concedido), pregunta al punto de decisión (`/access/v1/evaluation`) con su token
  `client_credentials`, cacheado hasta un minuto antes de caducar. Si Herensuge no responde, **deniega**.
- Un *step-up* pendiente (`invoice:approve` sin MFA) responde `401` con
  `WWW-Authenticate: Bearer error="insufficient_user_authentication"` (RFC 9470). La SPA llama a `reauthenticate()`
  de `@herensuge/react`.
- Para un objeto concreto: `IHerensugeAccess.CheckAsync(user, "invoice:delete", id, ip)`.

`ClientSecret` va en secretos de usuario o en variables de entorno (`Herensuge__ClientSecret`), nunca en el
repositorio.

## 3. La BFF y la SPA

Despliega `Herensuge.Bff` con `bff.appsettings.json` (más `Bff__ClientSecret`, Valkey y la clave de Data Protection
como en el portal). Usa la misma clave de ruta (`herensuge-api`) que el `appsettings.json` de la BFF para sustituir su
destino: `/api/*` va a `Facturas.Api`, no a Herensuge. En la SPA:

```tsx
import { Can } from "@herensuge/react"

<Can permission="invoice:approve">
	<button onClick={approve}>Aprobar</button>
</Can>
```

Si un administrador cambia los roles de alguien, la BFF lo detecta (contadores de versión en Valkey), refresca la
sesión y emite `permissions-changed`: la SPA se actualiza sin recargar.

## Saltos de proxy y la IP del cliente

El punto de decisión recibe la IP del cliente (`context.ip`) para las redes de confianza de cada organización. La API
la lee de `X-Forwarded-For`, así que solo debe confiar en los saltos que controla:

- `Herensuge:ProxyHops` = número de proxies propios delante de la API (la BFF es uno; un balanceador, otro).
- Si la API es accesible sin pasar por ellos, restringe `KnownProxies`/`KnownIPNetworks` a sus direcciones:
  si no, cualquiera podría falsear la IP con la cabecera.
