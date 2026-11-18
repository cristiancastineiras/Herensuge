# <img src="logo.svg" alt="Logo" height="32" /> Herensuge

Proveedor de identidad y autorización propio

## Requisitos

- .NET SDK 10 (`global.json` admite cualquier 10.0.x a partir de 10.0.100)
- Node 24 + pnpm 10 (solo para desarrollar las webs)
- Docker con Compose v2 (para las dependencias o para levantarlo todo)

## Levantarlo todo con Docker

```bash
cp Docker/.env.example Docker/.env      # y cambia los secretos
docker compose -f Docker/compose.yaml up -d --build
```

Portal en `https://portal.herensuge.localhost` y login en `https://id.herensuge.localhost` (detalles en
[Docker/README.md](Docker/README.md)).

## Desarrollo local

```bash
# 1. Dependencias (PostgreSQL 17 y Valkey) con las credenciales de appsettings.Development.json
docker compose -f Docker/compose.dev.yaml up -d

# 2. Certificado HTTPS de desarrollo (cookies __Host- y OpenIddict exigen HTTPS)
dotnet dev-certs https --trust

# 3. Webs (Vite; se abren a través de los hosts .NET, no directamente)
pnpm -C Src/Web install && pnpm -C Src/Web dev

# 4. IdP y BFF (en dos terminales)
dotnet run --project Src/Hosts/Herensuge.Api --launch-profile https   # https://localhost:5001
dotnet run --project Src/Hosts/Herensuge.Bff --launch-profile https   # https://localhost:5002
```

En `Development` la API:

- aplica las migraciones al arrancar;
- crea el cliente `herensuge-portal` y el scope `herensuge.api`;
- genera los certificados OIDC en `Src/Hosts/Herensuge.Api/App_Data/oidc-keys` (ignorado por git);
- publica el contrato OpenAPI en `https://localhost:5001/openapi/v1.json`;
- escribe los emails y SMS en la consola, con sus enlaces y códigos.

El usuario que verifique el email `admin@herensuge.local` (`Identity:BootstrapPlatformAdminEmail`) pasa a ser administrador de plataforma.

> Sin Valkey, en `Development` se puede vaciar `ConnectionStrings:Valkey`: la API y la BFF usan caché en memoria (una sola instancia).

## Configuración principal

| Clave | Uso |
|---|---|
| `ConnectionStrings:Database` / `ConnectionStrings:Valkey` | PostgreSQL y Valkey |
| `Oidc:Issuer` | URL pública del IdP (`https://id.<dominio>/`) |
| `Oidc:Keys:Directory` / `Oidc:Keys:Password` | Certificados de firma y cifrado (se generan si faltan) |
| `Oidc:PortalClient:*` | Secreto y URIs del cliente de la BFF (se sincronizan al arrancar) |
| `Identity:Passkeys:RelyingPartyId` / `Origins` | Dominio registrable y orígenes permitidos para WebAuthn |
| `Identity:ExternalProviders:Google|Microsoft` | Credenciales OAuth (vacías = botón oculto) |
| `Notifications:Email:Provider` / `Sms:Provider` | `Log` hoy; no arranca con `Log` en `Production` |
| `Notifications:PublicBaseUrl` | Base de los enlaces de los emails (la SPA de login) |
| `Herensuge:TrustForwardedHeaders` | `true` detrás de Caddy (nunca expuesto sin proxy) |
| `Herensuge:AllowedReturnOrigins` | Orígenes absolutos permitidos como `returnUrl` (el portal) |
| `Access:OpenFga:ApiUrl` | OpenFGA para permisos por objeto (vacío = desactivados: `relationships_disabled`) |
| `Identity:Support:Clients` | Clientes que aceptan sesiones de soporte (por defecto, el portal) |
| `Bff:Authority` / `Bff:BackchannelAddress` / `Bff:ClientSecret` | Configuración del cliente OIDC de la BFF |
| `Bff:Scopes` | `openid` y el scope de **una** aplicación (`herensuge.api` en el portal, `fichajes.api` en Fichajes) |
| `Bff:InstanceName` | Prefijo de su estado en Valkey y nombre de servicio en la telemetría (`herensuge-bff`); cada aplicación con su BFF en el mismo Valkey usa el suyo |
| `RateLimiting:*`, `Messaging:*`, `Audit:RetentionDays` | Límites, outbox/inbox y retención |

## Rutas de la SPA de login

La API redirige o enlaza a estas rutas del mismo origen que el IdP:

| Ruta | Para qué | API que usa |
|---|---|---|
| `/login?returnUrl=…&error=…` | Contraseña, passkey, Google/Microsoft | `POST /api/v1/auth/sign-in`, `/passkeys/*`, `GET /api/v1/auth/external/{provider}` |
| `/mfa?returnUrl=…` | Segundo factor | `GET`/`POST /api/v1/auth/sign-in/mfa`, `POST …/mfa/sms` |
| `/mfa/setup?returnUrl=…` | Alta de TOTP exigida por la organización | `POST /api/v1/auth/mfa/totp`, `…/confirm` |
| `/select-tenant?returnUrl=…` | Elegir organización | `GET /api/v1/auth/tenants`, `POST …/tenants/select` |
| `/onboarding?returnUrl=…` | Crear la primera organización | `POST /api/v1/auth/tenants` |
| `/complete-profile?returnUrl=…` | Datos que pide la aplicación y al usuario le faltan | `PUT /api/v1/auth/profile-fields` |
| `/verify-email?token=…` | Enlace del email | `POST /api/v1/auth/verify-email` |
| `/forgot-password`, `/reset-password?token=…` | Recuperar contraseña | `POST /api/v1/auth/password/forgot`, `…/reset` |
| `/confirm-email?token=…` | Cambio de email | `POST /api/v1/auth/email-change/confirm` |
| `/invitations/{token}` | Invitación | `GET`/`POST /api/v1/auth/invitations/{token}[/accept]` |
| `/support#token` | Canjear un enlace de soporte con la passkey del admin (ventana privada) | `POST /api/v1/auth/support/redeem` |

Toda petición que modifica datos a `/api/v1/auth/*` lleva `X-CSRF: 1` y JSON. Tras el login, la SPA navega a `returnUrl`
(solo rutas locales). `GET /api/v1/auth/session` devuelve el estado del inicio de sesión.

## Pantalla de login de cada aplicación

Cada cliente OIDC (cada aplicación del SaaS) tiene su pantalla de login, en el portal: **Plataforma → Clientes OIDC →
Pantalla de login**. La SPA de login la aplica cuando el `returnUrl` es `/connect/authorize?client_id=…`
(`GET /api/v1/auth/clients/{clientId}/login-page`, anónimo).

| Qué | Cómo se aplica |
|---|---|
| **Marca**: logo (imagen en línea, ≤ 100 KB), color de los botones, enlaces a condiciones y privacidad | Solo visual |
| **Métodos**: email y contraseña, passkey, Google, Microsoft | La SPA oculta los demás y `/connect/authorize` lo comprueba con el `amr` de la sesión: si se entró con un método que la aplicación no admite, vuelve al login (`error=sign_in_method_not_allowed`). Google y Microsoft se comprueban también al empezar |
| **Registro abierto o cerrado** | Cerrado: sin "Crear cuenta", `POST /register` con ese `clientId` responde `Users.RegistrationDisabled` y Google/Microsoft no crean cuentas nuevas |
| **Campos extra** (texto, número, fecha, teléfono, lista, casilla; obligatorios u opcionales) | Se piden en el registro y, si a alguien le falta uno obligatorio (entró con Google o se registró en otra aplicación), `/connect/authorize` lo manda a `/complete-profile` antes de emitir el código |
| **Crear organizaciones** (sí o no) | No: quien entra sin organización ve «pide a tu empresa que te invite» en lugar de «Crea tu organización». Es la opción para aplicaciones cuyas empresas da de alta su administrador en el portal y cuya gente entra por invitación. No es una barrera de seguridad: una organización propia no da acceso a nada ajeno |
| **Dirección de la aplicación** | Adónde lleva «Ir a …» tras aceptar una invitación (si se llegó desde esa aplicación) |

- Los valores se guardan en el usuario (`identity.users.profile_fields`, JSON cifrado) y los comparten las aplicaciones:
  dos que piden `company` leen el mismo valor.
- Cada aplicación recibe **solo sus campos** en el claim `profile_fields` del ID token (scope `profile`), por ejemplo
  `{"company": "Acme"}`. La BFF lo pasa a la SPA en `/bff/user` (`profileFields`).
- Los ajustes se guardan con el cliente (propiedad `herensuge.login` de OpenIddict): se borran con él. El portal (cliente
  de sistema) también se puede personalizar.

## Tests

```bash
dotnet test --project Tests/Herensuge.Modules.Identity.UnitTests/Herensuge.Modules.Identity.UnitTests.csproj
dotnet test --project Tests/Herensuge.Modules.Tenancy.UnitTests/Herensuge.Modules.Tenancy.UnitTests.csproj
dotnet test --project Tests/Herensuge.Modules.Oidc.UnitTests/Herensuge.Modules.Oidc.UnitTests.csproj
dotnet test --project Tests/Herensuge.Modules.Access.UnitTests/Herensuge.Modules.Access.UnitTests.csproj
dotnet test --project Tests/Herensuge.Access.AspNetCore.UnitTests/Herensuge.Access.AspNetCore.UnitTests.csproj
dotnet test --project Tests/Herensuge.ArchitectureTests/Herensuge.ArchitectureTests.csproj
dotnet test --project Tests/Herensuge.IntegrationTests/Herensuge.IntegrationTests.csproj   # necesita Docker (Testcontainers)
```

- **Unitarios:** agregados e invariantes (bloqueo, último método de acceso, último owner, tokens de un solo uso, Argon2id). Autorización: evaluador (roles, grupos, planes, condiciones, relaciones), redes de confianza, peticiones temporales, sesiones de soporte y el SDK .NET.
- **Arquitectura:** regla de dependencias de Clean Architecture y aislamiento entre módulos.
- **Integración:** flujo OIDC completo (registro → verificación → login → organización → código → tokens → API → refresh), bloqueo y MFA con TOTP, contra PostgreSQL y Valkey reales. Pantalla de login de una aplicación: campos obligatorios, `/complete-profile`, `profile_fields` en el ID token, método no admitido y registro cerrado. Autorización: permisos de una aplicación en su token y en la PDP, cuentas de servicio con planes y *token exchange*.

## Migraciones

```bash
dotnet tool restore
dotnet dotnet-ef migrations add <Nombre> \
  --startup-project Src/Hosts/Herensuge.Api \
  --project Src/Modules/Identity/Herensuge.Modules.Identity.Infrastructure \
  --context IdentityDbContext --output-dir Database/Migrations
```

Un `DbContext` y un esquema por módulo: `identity`, `tenancy`, `oidc`, `access`, `audit`, `notifications` y `common` (claves de
Data Protection). Para generar una migración hace falta `ASPNETCORE_ENVIRONMENT=Development` (fuera de Development la API
exige Valkey y secretos para arrancar).
En producción las aplica el servicio `herensuge-migrator` (`dotnet Herensuge.Api.dll --migrate`).
La CI comprueba que ningún módulo tenga cambios de modelo sin migración (`dotnet ef migrations has-pending-model-changes`).

## Autorización de las aplicaciones

Herensuge decide también qué puede hacer cada usuario en cada aplicación del SaaS: manifiesto de permisos por aplicación,
roles de la organización, grupos, planes, cuentas de servicio, permisos por objeto (OpenFGA, opcional), accesos temporales,
sesiones de soporte de solo lectura y *token exchange*. Punto de decisión con la forma de OpenID AuthZEN en
`/access/v1/evaluation(s)`.

- Diseño: [Docs/Arquitectura/Autorizacion.md](Docs/Arquitectura/Autorizacion.md) y el plan de implementación
  [Autorizacion-Implementacion.md](Docs/Arquitectura/Autorizacion-Implementacion.md) (decisiones ADR-A1 a ADR-A13 en
  [Docs/Decisiones](Docs/Decisiones)).
- APIs de las aplicaciones: SDK [Herensuge.Access.AspNetCore](Src/Sdk/Herensuge.Access.AspNetCore/README.md) y el ejemplo
  [Samples/Facturas](Samples/Facturas/README.md).
- SPAs: [`@herensuge/react`](Src/Web/paquetes/herensuge-react/README.md) (`useCan`, `<Can>`, `useEntitled`).
- Una aplicación completa (web, API, base de datos y BFF en Docker), con todos los pasos para darla de alta:
  [FichajesApp](FichajesApp/README.md).

## Despliegue

Ver [Docker/README.md](Docker/README.md).
