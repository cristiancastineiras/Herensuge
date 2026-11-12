# Herensuge Identity API

Proveedor de identidad propio (OAuth 2.1 + OpenID Connect) del SaaS: .NET 10, 100% FOSS, monolito modular con
Clean Architecture, DDD, CQRS y Outbox/Inbox. El diseño completo está en [RESUMEN.md](RESUMEN.md).

**Estado:** Nivel 1 implementado: backend (Herensuge.Api + Herensuge.Bff) y webs React (`login` y `portal`, en `Src/Web`).

## Qué incluye

| Pieza | Qué hace |
|---|---|
| `Src/Hosts/Herensuge.Api` | IdP: endpoints OIDC (OpenIddict), API de login para la SPA, API de gestión, jobs de outbox/inbox |
| `Src/Hosts/Herensuge.Bff` | BFF del portal: cliente OIDC confidencial, sesión en Valkey, proxy YARP `/api/*` con Bearer |
| `Src/Modules/Identity` | Usuarios, contraseña (Argon2id), TOTP, SMS, passkeys (WebAuthn), Google/Microsoft, sesiones |
| `Src/Modules/Tenancy` | Organizaciones, miembros y roles, invitaciones, política de MFA obligatoria |
| `Src/Modules/Oidc` | Autorización OIDC, clientes, revocación de tokens por sesión, claves de firma |
| `Src/Modules/Audit` | Registro inmutable de todos los eventos (sin datos sensibles) |
| `Src/Modules/Notifications` | Email y SMS detrás de puertos; hoy el proveedor `Log` |
| `Src/Web` | Webs React (monorepo pnpm): `apps/login` (servida por la API) y `apps/portal` (servida por la BFF). Ver [Src/Web/README.md](Src/Web/README.md) |
| `Docker/` | Dockerfiles (compilan también las webs), `compose.yaml` (producción con Caddy/TLS) y `compose.dev.yaml` (dependencias de desarrollo) |

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
| `Bff:Authority` / `Bff:BackchannelAddress` / `Bff:ClientSecret` | Configuración del cliente OIDC de la BFF |
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
| `/verify-email?token=…` | Enlace del email | `POST /api/v1/auth/verify-email` |
| `/forgot-password`, `/reset-password?token=…` | Recuperar contraseña | `POST /api/v1/auth/password/forgot`, `…/reset` |
| `/confirm-email?token=…` | Cambio de email | `POST /api/v1/auth/email-change/confirm` |
| `/invitations/{token}` | Invitación | `GET`/`POST /api/v1/auth/invitations/{token}[/accept]` |

Toda petición que modifica datos a `/api/v1/auth/*` lleva `X-CSRF: 1` y JSON. Tras el login, la SPA navega a `returnUrl`
(solo rutas locales). `GET /api/v1/auth/session` devuelve el estado del inicio de sesión.

## Tests

```bash
dotnet test --project Tests/Herensuge.Modules.Identity.UnitTests/Herensuge.Modules.Identity.UnitTests.csproj
dotnet test --project Tests/Herensuge.Modules.Tenancy.UnitTests/Herensuge.Modules.Tenancy.UnitTests.csproj
dotnet test --project Tests/Herensuge.ArchitectureTests/Herensuge.ArchitectureTests.csproj
dotnet test --project Tests/Herensuge.IntegrationTests/Herensuge.IntegrationTests.csproj   # necesita Docker (Testcontainers)
```

- **Unitarios:** agregados e invariantes (bloqueo, último método de acceso, último owner, tokens de un solo uso, Argon2id).
- **Arquitectura:** regla de dependencias de Clean Architecture y aislamiento entre módulos.
- **Integración:** flujo OIDC completo (registro → verificación → login → organización → código → tokens → API → refresh), bloqueo y MFA con TOTP, contra PostgreSQL y Valkey reales.

## Migraciones

```bash
dotnet tool restore
dotnet dotnet-ef migrations add <Nombre> \
  --startup-project Src/Hosts/Herensuge.Api \
  --project Src/Modules/Identity/Herensuge.Modules.Identity.Infrastructure \
  --context IdentityDbContext --output-dir Database/Migrations
```

Un `DbContext` y un esquema por módulo: `identity`, `tenancy`, `oidc`, `audit`, `notifications` y `common` (claves de Data Protection).
En producción las aplica el servicio `herensuge-migrator` (`dotnet Herensuge.Api.dll --migrate`).

## Despliegue

Ver [Docker/README.md](Docker/README.md).
