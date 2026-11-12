# Herensuge Identity API

> **Estado:** diseño del Nivel 1 · **Última revisión:** 10/11/2026
> Este documento es la fuente de verdad de la arquitectura. Cada decisión importante tendrá su ADR en `Docs/Decisiones/`.

---

## 1. Qué es

Herensuge es un **proveedor de identidad propio** (OAuth 2.1 + OpenID Connect) sobre **.NET 10** y **100% FOSS**. Va **integrado en mi SaaS B2B multi-tenant** y se vende con él.

- Los clientes son **organizaciones** (tenants).
- Sus usuarios entran con **email + contraseña**, **Google** o **Microsoft**.
- Nivel objetivo: el de Auth0 o Clerk, sin depender de productos comerciales.

---

## 2. Alcance

### Nivel 1 (este documento)

Todo lo necesario para que el SaaS funcione de punta a punta con organizaciones reales. El detalle está en la [sección 19](#19-definición-de-hecho-del-nivel-1).

**Funciona al 100% en local sin ninguna API key:**
- El email y el SMS se escriben en texto plano en el log.
- Google y Microsoft se activan solos en cuanto se configuran sus credenciales.

### Fuera de alcance (Nivel 2, se diseñará más adelante)

SSO por organización (Entra ID, Okta, OIDC genérico), Home Realm Discovery, SAML, SCIM, dominios personalizados, roles personalizados, registro dinámico de clientes e idiomas distintos del español.

---

## 3. Principios

1. **FOSS siempre.** No se usa ninguna dependencia con licencia comercial (ver [15.4](#154-prohibidos)).
2. **No reinventar OAuth/OIDC**: el protocolo lo implementa OpenIddict.
3. **Monolito modular** con **Clean Architecture + DDD + CQRS + Outbox**.
4. **Dominio rico solo donde hay reglas de negocio** (Identity, Tenancy). Donde no las hay, sin ceremonia.
5. **Ningún proveedor externo en el núcleo**: email y SMS van detrás de puertos.
6. **Los tokens nunca llegan al navegador**: patrón BFF.
7. **Desarrollo con la misma topología que producción**: mismos orígenes, cookies y redirecciones.
8. **Un `docker compose up` levanta todo.**

---

## 4. Arquitectura de despliegue

```
                     ┌──────────────────────────────┐
                     │           Browser            │
                     └───────┬──────────────┬───────┘
   cookie __Host-herensuge-bff│              │cookie __Host-herensuge-idp
                             ▼              ▼
┌───────────────────────────────┐   ┌───────────────────────────────────┐
│ Herensuge.Bff                 │   │ Herensuge.Api                     │
│ portal.<dominio>              │   │ id.<dominio>                      │
│ · SPA portal (React)          │──►│ · OIDC / OAuth 2.1 (OpenIddict)   │──► Google
│ · Cliente OIDC (code + PKCE)  │   │ · SPA login (React)               │──► Microsoft
│ · YARP /api/* + Bearer        │──►│ · API de gestión /api/v1/*        │
│ · Tokens en Valkey            │   │ · 5 módulos + jobs outbox/inbox   │
└──────────────┬────────────────┘   └──────────┬───────────────┬────────┘
               ▼                               ▼               ▼
             Valkey ◄──────────────────────────┘          PostgreSQL

     Todos los servicios ──OTLP──► Grafana (Loki · Tempo · Prometheus)
```

| Componente | Responsabilidad | Desarrollo | Producción |
|---|---|---|---|
| `Herensuge.Api` | IdP: OIDC, SPA de login, API de gestión, módulos, jobs | `https://localhost:5001` | `https://id.<dominio>` |
| `Herensuge.Bff` | BFF: cliente OIDC, sesiones, proxy YARP, SPA portal | `https://localhost:5002` | `https://portal.<dominio>` |
| PostgreSQL | Toda la persistencia (un esquema por módulo) | `:5432` | Gestionado o contenedor |
| Valkey | Sesiones del BFF, retos WebAuthn, caché | `:6379` | Contenedor |
| Grafana stack | Logs, trazas y métricas | `grafana/otel-lgtm` en `:3000` | Alloy + Loki + Tempo + Prometheus + Grafana |

Los dos hosts comparten dominio registrable (`<dominio>`, o `localhost` en desarrollo). Así, el navegador trata como *same-site* el paso del IdP al BFF y viceversa.

---

## 5. Flujos

### 5.1 Login con contraseña (y MFA)

1. El usuario abre el portal. El SPA llama a `GET /bff/user` y recibe `401`.
2. El SPA navega a `/bff/login?returnUrl=/`. El BFF genera `state`, `nonce` y `code_verifier` y redirige a `/connect/authorize` (code + PKCE S256).
3. El endpoint *authorize* no encuentra sesión del IdP y redirige al SPA de login: `/login?returnUrl=<authorize>`. `returnUrl` solo admite rutas locales (protección contra *open redirect*).
4. El SPA envía las credenciales a `POST /api/v1/auth/sign-in`. Respuestas posibles: `signed-in` · `mfa-required` (con los métodos disponibles) · `email-not-verified` · `locked-out` · `invalid-credentials`.
5. Si la respuesta es `mfa-required`: se crea la cookie temporal `__Host-herensuge-mfa` (5 min) y el usuario completa `POST /api/v1/auth/sign-in/mfa` con TOTP, SMS o un código de recuperación.
6. Al completar el login se crean el agregado `UserSession` y la cookie `__Host-herensuge-idp`. Si el usuario no tiene MFA y alguna de sus organizaciones lo exige, debe dar de alta TOTP o una passkey antes de continuar.
7. Organización activa:
   - Ninguna → onboarding (crear organización) o aceptar una invitación pendiente.
   - Una → se elige automáticamente.
   - Varias → selector.
8. El SPA vuelve a `returnUrl`. *Authorize* emite el `code` con los claims `sub`, `sid`, `tenant_id`, `tenant_role`, `amr` y `auth_time`.
9. El BFF canjea el código en `/connect/token` (con `client_secret` + `code_verifier`), guarda los tokens en Valkey, emite `__Host-herensuge-bff` y redirige a `returnUrl`.

### 5.2 Login con passkey

1. `POST /api/v1/auth/passkeys/options` → `navigator.credentials.get()` con credencial *discoverable* y `userVerification: required`.
2. `POST /api/v1/auth/passkeys/sign-in`.

Una passkey con verificación de usuario **cuenta como MFA** (`amr: ["pop", "mfa"]`).

### 5.3 Login con Google o Microsoft (vinculación segura)

El login empieza en `GET /api/v1/auth/external/{google|microsoft}`. OpenIddict.Client hace el flujo con el proveedor y, al volver, se aplican estas reglas por orden:

| # | Situación | Resultado |
|---|---|---|
| 1 | Existe un login externo con `(proveedor, sub)` | Login |
| 2 | Hay un usuario autenticado vinculando desde el portal | Se vincula a ese usuario |
| 3 | El email ya pertenece a otra cuenta | **No se vincula.** Se pide entrar con el método existente y vincular desde el portal |
| 4 | El email es nuevo | Se crea el usuario. El email solo se da por verificado si el proveedor lo garantiza (Google `email_verified=true`). El email de Microsoft se trata como **no verificado** |

- La identidad externa se identifica por `(proveedor, sub)`, **nunca por el email**. Así se evita el fallo conocido como "nOAuth".
- El MFA de Herensuge se sigue aplicando después del login externo si el usuario lo tiene activado o su organización lo exige. En el Nivel 1 no se confía en el MFA del proveedor.

### 5.4 Llamada a la API y refresh

1. El SPA hace `fetch('/api/v1/...', { headers: { 'X-CSRF': '1' } })`.
2. El BFF valida la cookie y la cabecera.
3. Si el access token caduca en menos de 60 s, el BFF lo refresca con rotación, bajo un *lock* por sesión en Valkey.
4. YARP reenvía la petición con `Authorization: Bearer`.
5. `Herensuge.Api` valida el token (OpenIddict.Validation, en local) y aplica sus *policies*.

Si el refresh falla (token revocado), el BFF borra la sesión y responde `401`; el SPA redirige entonces a `/bff/login`.

### 5.5 Cambio de organización

`/bff/login?tenant=<id>&prompt=none`. Si la sesión del IdP sigue viva y el usuario es miembro de esa organización, se emiten tokens nuevos sin pedir credenciales.

### 5.6 Logout

1. Navegación a `/bff/logout?sid=<sid>` (el `sid` solo lo conoce el SPA, a través de `/bff/user`, y protege contra un logout provocado desde otro sitio). El BFF revoca el refresh token (`/connect/revocation`) y borra la sesión en Valkey.
2. El BFF redirige a `/connect/logout` con `id_token_hint`.
3. Herensuge revoca la `UserSession`, borra la cookie del IdP y redirige a `post_logout_redirect_uri`.

### 5.7 Revocar sesiones ("cerrar sesión en otros dispositivos")

1. `UserSession.Revoke()` emite el evento `SessionRevoked`.
2. El módulo Oidc revoca las autorizaciones y tokens con ese `sid`.
3. El siguiente refresh de ese BFF falla y el usuario queda fuera.

Desfase máximo: la vida de un access token (5 min). La cookie del IdP se revalida contra `UserSession` cada minuto.

### 5.8 Operaciones sensibles (re-autenticación)

Cambiar email o contraseña, gestionar MFA, passkeys o cuentas vinculadas y borrar la cuenta exigen `auth_time` de menos de 10 minutos.

Si no se cumple, la API responde `401` con `error=reauth_required` y el SPA redirige a `/bff/login?max_age=0&returnUrl=...`.

---

## 6. Backend: arquitectura

### 6.1 Módulos (bounded contexts)

| Módulo | Tipo DDD | Responsabilidad | Capas |
|---|---|---|---|
| **Identity** | Core | Usuarios, credenciales (contraseña, TOTP, SMS, passkeys, códigos de recuperación), cuentas externas, sesiones, dispositivos, tokens de un solo uso | Domain · Application · Infrastructure · Presentation · Contracts |
| **Tenancy** | Core | Organizaciones, miembros, roles, invitaciones, política de MFA | Domain · Application · Infrastructure · Presentation · Contracts |
| **Oidc** | Genérico | Endpoints OIDC, clientes y scopes, construcción de claims, revocación de tokens | Application · Infrastructure · Presentation |
| **Audit** | Soporte | Registro inmutable de eventos y consultas para administradores | Application · Infrastructure · Presentation |
| **Notifications** | Soporte | Email y SMS a partir de eventos, plantillas | Application · Infrastructure |

- **Por qué 5 módulos y no 10:** Users, Authentication, Mfa, Passkeys y Sessions comparten invariantes, y un solo login toca todos. Son un único *bounded context*. Separarlos obligaba a hacer llamadas entre módulos en cada login, justo el problema por el que se descartaron los microservicios.
- **Domain solo existe donde hay reglas.** Oidc delega su modelo en OpenIddict; Audit y Notifications solo reaccionan a eventos.

### 6.2 Capas y regla de dependencias

```
        ┌──────────────────┐
        │   Presentation   │  Endpoints (minimal APIs), Result → HTTP
        └────────┬─────────┘
                 ▼
        ┌──────────────────┐        ┌──────────────────────────────┐
        │   Application    │◄───────│        Infrastructure        │
        │ Commands, queries│        │ EF Core, Dapper, Argon2,     │
        │ handlers, puertos│        │ Fido2, Otp.NET, OpenIddict…  │
        └────────┬─────────┘        └──────────────┬───────────────┘
                 ▼                                 │
        ┌──────────────────┐                       │
        │      Domain      │◄──────────────────────┘
        │ Agregados, VOs,  │  Sin dependencias externas
        │ eventos, reglas  │
        └──────────────────┘
```

| Proyecto | Puede referenciar |
|---|---|
| `*.Domain` | `Herensuge.Common.Domain` |
| `*.Application` | Su `Domain`, `Herensuge.Common.Application`, `Contracts` de otros módulos |
| `*.Infrastructure` | Su `Application` y `Domain`, su `Contracts`, `Herensuge.Common.Infrastructure` |
| `*.Presentation` | Su `Application`, `Herensuge.Common.Presentation` |
| `*.Contracts` | `Herensuge.Common.Application` (solo tipos base de eventos) |
| Hosts | `Infrastructure` y `Presentation` de cada módulo |

**Nunca** un módulo referencia el `Domain`, `Application`, `Infrastructure` ni `Presentation` de otro módulo. Lo comprueban los tests de arquitectura.

### 6.3 Comunicación entre módulos

| Necesidad | Mecanismo | Ejemplo |
|---|---|---|
| Consultar algo de otro módulo | Interfaz pública en `Contracts`, llamada en proceso | Oidc pide los claims a `IIdentityModuleApi` y la membresía a `ITenancyModuleApi` |
| Reaccionar a algo que pasó en otro módulo | Integration event vía outbox → inbox | `UserRegistered` → Notifications envía la verificación y Audit lo registra |

Prohibido: leer tablas de otro módulo y hacer transacciones que abarquen varios módulos.

### 6.4 DDD: agregados e invariantes

**Identity**

| Agregado | Contiene | Invariantes |
|---|---|---|
| `User` | `Email`, `PasswordHash?`, `PhoneNumber?`, TOTP (secreto cifrado y último *time step*), `RecoveryCodes`, `ExternalLogins`, `PasskeyCredentials`, `KnownDevices`, `Lockout`, `IsPlatformAdmin` | Email único. No se puede quitar el último método de acceso. 5 fallos (contraseña o segundo factor) → bloqueo de 15 min. El login con contraseña exige email verificado. Un código TOTP no se puede reutilizar. Cambiar la contraseña revoca las demás sesiones. Los códigos de recuperación son de un solo uso |
| `UserSession` | `UserId`, `Amr`, `AuthTime`, `Ip`, `UserAgent`, `DeviceId`, `LastSeenAt`, `ExpiresAt`, `RevokedAt` | Una sesión revocada no se reactiva |
| `OneTimeToken` | `UserId`, `Purpose`, `TokenHash`, `ExpiresAt`, `ConsumedAt` | Un solo uso. Caducidad según el propósito: verificar email 24 h, cambio de email 1 h, reset 15 min, verificar teléfono 10 min, MFA por SMS 5 min |

**Tenancy**

| Agregado | Contiene | Invariantes |
|---|---|---|
| `Tenant` | `Name`, `Slug`, `Status`, `RequireMfa` | `Slug` único. Una organización desactivada no emite tokens |
| `Membership` | `TenantId`, `UserId`, `Role` (`Owner` · `Admin` · `Member`) | Una membresía por usuario y organización. Siempre queda al menos un `Owner`. Solo un `Owner` puede nombrar a otro `Owner` |
| `Invitation` | `TenantId`, `Email`, `Role`, `TokenHash`, `ExpiresAt`, `Status` | Caduca a los 7 días. Un solo uso. Solo la acepta un usuario con ese email verificado |

**Value objects:** `Email` (normalizado), `PhoneNumber` (E.164), `PasswordHash`, `TenantSlug`, `TenantRole`.

**Domain events:**
- **Identity:** `UserRegistered`, `EmailVerified`, `EmailChangeRequested`, `PasswordChanged`, `PasswordResetRequested`, `SignInFailed`, `UserLockedOut`, `SessionStarted`, `NewDeviceSignedIn`, `SessionRevoked`, `MfaEnabled`, `MfaDisabled`, `SmsMfaCodeRequested`, `PhoneVerificationRequested`, `PasskeyAdded`, `PasskeyRemoved`, `ExternalLoginLinked`, `ExternalLoginUnlinked`, `UserDeleted`.
- **Tenancy:** `TenantCreated`, `TenantDeactivated`, `TenantMfaPolicyChanged`, `MemberAdded`, `MemberRoleChanged`, `MemberRemoved`, `InvitationCreated`, `InvitationAccepted`, `InvitationRevoked`.

### 6.5 CQRS

- **Commands** modifican estado a través de agregados y repositorios (EF Core). Devuelven `Result` o `Result<T>`.
- **Queries** no tocan agregados: SQL con Dapper → DTO. Misma base de datos, modelos separados.
- **Sin mediador:** cada endpoint inyecta directamente su handler.
- **Decoradores** (Scrutor), en este orden: `Logging` → `Validation` (FluentValidation). Cada handler confirma con un único `SaveChangesAsync`: esa llamada es la transacción (estado + outbox).

```csharp
public interface ICommand { }
public interface ICommand<TResponse> { }
public interface IQuery<TResponse> { }

public interface ICommandHandler<in TCommand> where TCommand : ICommand
{
    Task<Result> Handle(TCommand command, CancellationToken ct);
}

public interface ICommandHandler<in TCommand, TResponse> where TCommand : ICommand<TResponse>
{
    Task<Result<TResponse>> Handle(TCommand command, CancellationToken ct);
}

public interface IQueryHandler<in TQuery, TResponse> where TQuery : IQuery<TResponse>
{
    Task<Result<TResponse>> Handle(TQuery query, CancellationToken ct);
}
```

### 6.6 Domain events, integration events y Outbox/Inbox

```
Command handler ─► Agregado.Raise(DomainEvent)
       │
       ▼ SaveChanges (UNA transacción)
[ tablas del módulo ] + [ outbox_messages ]
       │
       ▼ ProcessOutboxJob  (cada 1 s · lotes de 20 · FOR UPDATE SKIP LOCKED)
DomainEventHandlers del mismo módulo          ── idempotentes: outbox_message_consumers
       │ publican IntegrationEvent
       ▼
IEventBus ─► InMemoryEventBus ─► inbox_messages de cada módulo suscrito   (PK = id del evento)
       │
       ▼ ProcessInboxJob
IntegrationEventHandlers (Audit, Notifications, Oidc…)  ── idempotentes: inbox_message_consumers
```

- **Garantía:** entrega *al menos una vez* + handlers idempotentes = efecto *exactamente una vez*.
- **Varias instancias:** `FOR UPDATE SKIP LOCKED` permite escalar el host sin procesar un mensaje dos veces.
- **Reintentos** con backoff. Tras 10 fallos, el mensaje queda marcado con el error y salta una métrica de alerta.
- **Sobre de cada evento:** `id`, `type`, `occurredOnUtc`, `correlationId`, `traceparent`, `actor` (`userId`, `ip`, `userAgent`). Lo rellena el interceptor de EF a partir de la petición HTTP, así que el dominio no se entera.
- **Purga:** las filas procesadas se borran a los 7 días (pueden contener enlaces y códigos).
- **RabbitMQ:** se cambia `InMemoryEventBus` por `RabbitMqEventBus` el día que se extraiga un módulo. Los handlers no cambian.

### 6.7 Persistencia

- Una base de datos PostgreSQL, con **un esquema, un `DbContext` y unas migraciones por módulo** (en `Infrastructure/Database/Migrations`).
- Nombres en `snake_case` (EFCore.NamingConventions).
- Sin claves foráneas entre esquemas: solo ids.
- Concurrencia optimista con `xmin` en los agregados.
- **Desarrollo:** las migraciones se aplican al arrancar. **Producción:** *migration bundle* en el pipeline.

### 6.8 Validación y errores

- **Validación de entrada:** FluentValidation (decorador) → `400`.
- **Reglas de negocio:** devuelven `Result` con `Error(Code, Description, Type)`. Nunca se usan excepciones para el flujo de negocio.
- **Mapeo a HTTP** con `ProblemDetails` (RFC 9457):
  - `Validation` → 400
  - `Unauthorized` → 401
  - `Forbidden` → 403
  - `NotFound` → 404
  - `Conflict` → 409

  El `code` (por ejemplo `Users.EmailNotVerified`) lo traduce el SPA.
- **Excepciones inesperadas:** manejador global → `500` con `traceId`, sin detalles internos.

### 6.9 Composición del host

```csharp
builder.Services
    .AddCommonApplication()
    .AddCommonInfrastructure(builder.Configuration)
    .AddIdentityModule(builder.Configuration)
    .AddTenancyModule(builder.Configuration)
    .AddOidcModule(builder.Configuration)
    .AddAuditModule(builder.Configuration)
    .AddNotificationsModule(builder.Configuration);

app.MapEndpoints(); // descubre los IEndpoint de cada *.Presentation
```

---

## 7. Notificaciones: email y SMS sin proveedor

Los puertos viven en `Herensuge.Modules.Notifications.Application`:

```csharp
public sealed record EmailMessage(string To, string Subject, string TextBody);
public sealed record SmsMessage(string To, string Text);

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct);
}

public interface ISmsSender
{
    Task SendAsync(SmsMessage message, CancellationToken ct);
}
```

### Implementaciones (`Herensuge.Modules.Notifications.Infrastructure`)

| Proveedor | Estado | Qué hace |
|---|---|---|
| `Log` | **Nivel 1** | Escribe el mensaje completo en texto plano en el log |
| `Resend`, `AmazonSes`, `Mailgun`, `Twilio`, `Bird`, `Vonage`… | Futuro | Una clase por proveedor. No cambia nada más |

### Configuración

```json
"Notifications": {
  "Email": { "Provider": "Log", "From": "Herensuge <no-reply@herensuge.local>" },
  "Sms":   { "Provider": "Log", "From": "Herensuge" }
}
```

- Si el proveedor configurado no existe, la aplicación **no arranca**.
- `Log` en `Production` → la aplicación **no arranca**, porque los mensajes contienen enlaces y códigos.

Salida del proveedor `Log`:

```
[EMAIL] To: ana@empresa.com | Subject: Verifica tu email
Hola Ana,
Verifica tu email en este enlace (caduca en 24 horas):
https://localhost:5001/verify-email?token=...

[SMS] To: +34600000000 | Text: Tu código de Herensuge es 482915. Caduca en 5 minutos.
```

### Plantillas

Texto plano, en español. Una clase por plantilla, con *raw string literals*. No hay motor de plantillas.

### Disparadores

| Integration event | Canal | Mensaje |
|---|---|---|
| `UserRegistered`, `EmailVerificationRequested` | Email | Enlace de verificación (24 h) |
| `EmailChangeRequested` | Email | Confirmación al email nuevo (1 h) + aviso al antiguo |
| `PasswordResetRequested` | Email | Enlace de restablecimiento (15 min) |
| `PasswordChanged`, `MfaEnabled`, `MfaDisabled`, `PasskeyAdded`, `PasskeyRemoved`, `ExternalLoginLinked` | Email | Aviso de seguridad |
| `NewDeviceSignedIn` | Email | Aviso con IP, navegador y fecha |
| `UserLockedOut` | Email | Aviso + enlace de restablecimiento |
| `InvitationCreated` | Email | Enlace de invitación (7 días) |
| `PhoneVerificationRequested` | SMS | Código de 6 dígitos (10 min) |
| `SmsMfaCodeRequested` | SMS | Código de 6 dígitos (5 min) |

- **Latencia esperada:** unos 2 s entre la acción y el envío (dos saltos de polling de 1 s).
- **Fallos de envío:** los reintenta el job de inbox. Cuando un proveedor admita claves de idempotencia, se le pasa el id del evento.

---

## 8. Herensuge.Bff (YARP)

`Herensuge.Bff` es un cliente OIDC confidencial (OpenIddict.Client) con proxy YARP. Lo usa el portal y sirve de **plantilla para el BFF de cada app del SaaS**: solo cambian el `client_id`, el SPA y las rutas YARP.

| Ruta | Método | Qué hace |
|---|---|---|
| `/bff/login` | GET | Inicia OIDC (code + PKCE). Parámetros: `returnUrl` (solo local), `tenant`, `prompt`, `max_age` |
| `/bff/callback` | GET | Recibe el code, canjea los tokens y crea la sesión |
| `/bff/user` | GET | Claims para el SPA (`sub`, `name`, `email`, `tenant_id`, `tenant_role`, `amr`) o `401` |
| `/bff/logout?sid=…` | GET | Revoca el refresh token, borra la sesión y cierra la sesión del IdP (navegación de nivel superior) |
| `/bff/signout-callback` | GET | Vuelta del IdP tras el logout |
| `/api/{**}` | * | YARP → `Herensuge.Api` con `Authorization: Bearer` |
| `/{**}` | GET | SPA: ficheros estáticos en producción; YARP → Vite en desarrollo |

- **Sesión:** la cookie `__Host-herensuge-bff` (`HttpOnly`, `Secure`, `SameSite=Strict`) solo lleva la clave. El ticket (claims + tokens) vive en Valkey mediante `ITicketStore`, con un TTL igual a la vida del refresh token.
- **CSRF:** `/api/*` exige la cabecera `X-CSRF: 1`. Un formulario o enlace de otro sitio no puede ponerla, y un `fetch` cross-site necesitaría CORS, que no se habilita. `/bff/logout` es una navegación (redirige al IdP) y se protege con el parámetro `sid`.
- **Refresh:** lo hace un middleware del pipeline de YARP con un lock por sesión en Valkey. Dos refresh simultáneos activarían la detección de reutilización de OpenIddict, que revoca toda la cadena.
- **Tokens en reposo:** el ticket guardado en Valkey va cifrado con Data Protection.
- **Back-channel en contenedores:** con `Bff:BackchannelAddress` las llamadas a la URL pública del IdP van por la red interna con cabeceras `X-Forwarded-*`.
- **Respuestas `401` en `/api/*`:** nunca redirigen; el SPA decide qué hacer.
- **Data Protection** del BFF persistida en Valkey (el BFF no tiene base de datos).
- **Rutas YARP declarativas** en `appsettings.json`:

```json
"ReverseProxy": {
  "Routes": {
    "herensuge-api": { "ClusterId": "herensuge-api", "Match": { "Path": "/api/{**catch-all}" } }
  },
  "Clusters": {
    "herensuge-api": { "Destinations": { "primary": { "Address": "https://localhost:5001" } } }
  }
}
```

---

## 9. Webs React

### 9.1 Aplicaciones

| App | Servida por | Autenticación | Pantallas |
|---|---|---|---|
| `login` | `Herensuge.Api` (`id.<dominio>`) | Cookie del IdP + `X-CSRF` | Login, MFA, passkey, registro, verificar email, recuperar contraseña, selector de organización, onboarding, aceptar invitación, errores |
| `portal` | `Herensuge.Bff` (`portal.<dominio>`) | Cookie del BFF + `X-CSRF` | **Mi cuenta** (todos) · **Organización** (Owner/Admin) · **Plataforma** (admin de plataforma) |

El antiguo AccountPortal y el AdminPortal se unen en una sola app, `portal`, con secciones por rol. La sección de cada rol se carga *lazy*, así que la de plataforma no se descarga para usuarios normales.

### 9.2 Stack

| Pieza | Elección | Licencia |
|---|---|---|
| Lenguaje y bundler | TypeScript (strict) + Vite | Apache-2.0 / MIT |
| UI | React 19 | MIT |
| Routing | React Router (modo SPA) | MIT |
| Estado de servidor | TanStack Query | MIT |
| Formularios | React Hook Form + Zod | MIT |
| Estilos y componentes | Tailwind CSS v4 + shadcn/ui (Radix) | MIT |
| Cliente API tipado | openapi-typescript + openapi-fetch | MIT |
| WebAuthn | @simplewebauthn/browser | MIT |
| Tests | Vitest + Testing Library | MIT |
| Lint | ESLint + typescript-eslint + eslint-plugin-boundaries | MIT |
| Monorepo | pnpm workspaces (+ catálogo de versiones) | MIT |

### 9.3 Estructura

```
Src/Web/
├── package.json
├── pnpm-workspace.yaml                 # workspaces + catálogo de versiones
├── apps/
│   ├── login/
│   │   └── src/
│   │       ├── app/                    # main.tsx, router.tsx, providers.tsx, layouts
│   │       ├── features/
│   │       │   ├── sign-in/            # contraseña, MFA, passkey, Google/Microsoft
│   │       │   ├── sign-up/            # registro y verificación de email
│   │       │   ├── password-reset/
│   │       │   ├── tenant-selection/   # selector, onboarding, aceptar invitación
│   │       │   └── errors/
│   │       └── shared/
│   └── portal/
│       └── src/
│           ├── app/                    # main.tsx, router.tsx, providers.tsx, layouts, error boundary
│           ├── features/
│           │   ├── account/            # perfil, contraseña, MFA, teléfono, passkeys, sesiones, cuentas vinculadas
│           │   ├── organization/       # miembros, invitaciones, política MFA, auditoría
│           │   └── platform/           # organizaciones, clientes OIDC, auditoría global
│           │       ├── api/            # queries y mutations (TanStack Query)
│           │       ├── components/
│           │       ├── routes/         # páginas de la feature
│           │       ├── schemas/        # Zod
│           │       └── index.ts        # API pública de la feature (solo rutas)
│           └── shared/
│               ├── api/                # http-client.ts: openapi-fetch + X-CSRF + 401 → /bff/login
│               ├── auth/               # useCurrentUser() ← /bff/user, <RequireRole>
│               └── lib/                # errors.ts: código de ProblemDetails → texto
└── packages/
    ├── ui/                             # design system compartido (Tailwind v4 + shadcn/ui)
    ├── api-client/                     # tipos generados desde /openapi/v1.json
    └── config/                         # tsconfig, eslint y prettier compartidos
```

### 9.4 Reglas

- **Dirección de dependencias:** `app → features → shared → packages`. Una feature no importa a otra (lo impide `eslint-plugin-boundaries`).
- **Estado de servidor solo en TanStack Query.** Nada de Redux. El estado de UI va en `useState` o en la URL.
- **Ningún token en el navegador:** nada de auth en `localStorage` ni `sessionStorage`. El portal solo conoce `/bff/user`.
- **Toda petición que modifica datos** lleva `X-CSRF: 1` y JSON. El cliente HTTP lo añade siempre.
- **Formularios:** React Hook Form + Zod. Los errores de `ProblemDetails` se pintan en su campo.
- **Rutas por rol solo como UX.** La autorización real la aplica siempre la API.
- **Tipos de la API generados**, nunca escritos a mano: `pnpm api:generate`. La CI falla si están desactualizados.
- **Accesibilidad:** componentes Radix, labels en todos los campos, foco gestionado al navegar, contraste AA.
- **Seguridad del navegador:** la CSP sin `unsafe-inline` y sin `eval`. `@simplewebauthn/browser` para WebAuthn.

### 9.5 Desarrollo y build

- **Desarrollo:** `pnpm dev` arranca Vite para `login` (`:5173`) y `portal` (`:5174`). El navegador **no** entra por Vite, sino por los hosts .NET, que reenvían las rutas del SPA a Vite (HMR incluido) con YARP. Así se usan los mismos orígenes, cookies y redirecciones que en producción.
- **Build:** el Dockerfile de cada host compila su SPA y copia `dist/` a `wwwroot/`.

---

## 10. Estructura del repositorio

```
Herensuge/
├── Docker/
│   ├── compose.yaml                           # postgres, valkey, otel-lgtm, herensuge-api, herensuge-bff
│   ├── Herensuge.Api.Dockerfile               # compila la SPA login + la API
│   ├── Herensuge.Bff.Dockerfile               # compila la SPA portal + el BFF
│   └── .env.example
├── Docs/
│   ├── API/
│   ├── Arquitectura/
│   └── Decisiones/                            # ADR-0001-*.md …
├── Src/
│   ├── Hosts/
│   │   ├── Herensuge.Api/                     # IdP: compone módulos, OpenIddict, SPA login, jobs
│   │   └── Herensuge.Bff/                     # BFF: cliente OIDC, YARP, sesiones en Valkey, SPA portal
│   ├── Common/
│   │   ├── Herensuge.Common.Domain/           # Entity, AggregateRoot, ValueObject, IDomainEvent, Result, Error
│   │   ├── Herensuge.Common.Application/      # ICommand/IQuery + handlers, decoradores, IEventBus, IntegrationEvent
│   │   ├── Herensuge.Common.Infrastructure/   # Outbox/Inbox, InMemoryEventBus, EF base, Valkey, OpenTelemetry
│   │   └── Herensuge.Common.Presentation/     # IEndpoint, Result → ProblemDetails
│   ├── Modules/
│   │   ├── Identity/
│   │   │   ├── Herensuge.Modules.Identity.Domain/
│   │   │   ├── Herensuge.Modules.Identity.Application/
│   │   │   ├── Herensuge.Modules.Identity.Infrastructure/
│   │   │   ├── Herensuge.Modules.Identity.Presentation/
│   │   │   └── Herensuge.Modules.Identity.Contracts/
│   │   ├── Tenancy/                           # Domain · Application · Infrastructure · Presentation · Contracts
│   │   ├── Oidc/                              # Application · Infrastructure · Presentation
│   │   ├── Audit/                             # Application · Infrastructure · Presentation
│   │   └── Notifications/                     # Application · Infrastructure
│   └── Web/                                   # monorepo pnpm (sección 9)
├── Tests/
│   ├── Herensuge.ArchitectureTests/
│   ├── Herensuge.Modules.Identity.UnitTests/
│   ├── Herensuge.Modules.Tenancy.UnitTests/
│   ├── Herensuge.IntegrationTests/
│   └── Herensuge.EndToEndTests/
├── .editorconfig
├── Directory.Build.props                      # net10.0, nullable, warnings como errores
├── Directory.Packages.props                   # versiones NuGet centralizadas
├── global.json                                # versión del SDK fijada
├── Herensuge.slnx
├── README.md
└── RESUMEN.md
```

### 10.1 Interior de un módulo (Identity)

```
Modules/Identity/
├── Herensuge.Modules.Identity.Domain/
│   ├── Users/            User.cs, Email.cs, PasswordHash.cs, PhoneNumber.cs, ExternalLogin.cs,
│   │                     PasskeyCredential.cs, RecoveryCode.cs, UserErrors.cs, IUserRepository.cs,
│   │                     Events/UserRegisteredDomainEvent.cs …
│   ├── Sessions/         UserSession.cs, SessionErrors.cs, IUserSessionRepository.cs, Events/…
│   └── Tokens/           OneTimeToken.cs, TokenPurpose.cs, IOneTimeTokenRepository.cs
├── Herensuge.Modules.Identity.Application/
│   ├── Abstractions/     IPasswordHasher.cs, ITotpService.cs, IPasskeyService.cs, ISessionCookieService.cs
│   ├── Users/
│   │   ├── RegisterUser/       RegisterUserCommand.cs, RegisterUserCommandHandler.cs, RegisterUserCommandValidator.cs
│   │   ├── VerifyEmail/
│   │   ├── ChangePassword/
│   │   └── GetCurrentUser/     GetCurrentUserQuery.cs, GetCurrentUserQueryHandler.cs, CurrentUserResponse.cs
│   ├── SignIn/           SignInWithPassword/, VerifyMfa/, SignInWithPasskey/, SignInWithExternalProvider/
│   ├── Mfa/              EnableTotp/, ConfirmTotp/, DisableTotp/, RegenerateRecoveryCodes/, VerifyPhone/
│   ├── Passkeys/         CreatePasskeyOptions/, AddPasskey/, RenamePasskey/, RemovePasskey/, GetPasskeys/
│   ├── Sessions/         GetSessions/, RevokeSession/, RevokeOtherSessions/
│   └── EventHandlers/    UserRegisteredDomainEventHandler.cs → publica UserRegisteredIntegrationEvent …
├── Herensuge.Modules.Identity.Infrastructure/
│   ├── Database/         IdentityDbContext.cs, Configurations/, Migrations/
│   ├── Repositories/
│   ├── Security/         Argon2PasswordHasher.cs, TotpService.cs (Otp.NET), PasskeyService.cs (Fido2)
│   ├── Authentication/   Esquemas de cookie (idp, mfa, external), Google y Microsoft (OpenIddict.Client)
│   ├── PublicApi/        IdentityModuleApi.cs (implementa IIdentityModuleApi)
│   └── IdentityModule.cs AddIdentityModule()
├── Herensuge.Modules.Identity.Presentation/
│   ├── Auth/             Endpoints de /api/v1/auth/*
│   └── Me/               Endpoints de /api/v1/me/*
└── Herensuge.Modules.Identity.Contracts/
    ├── IIdentityModuleApi.cs
    └── IntegrationEvents/ UserRegisteredIntegrationEvent.cs, SessionRevokedIntegrationEvent.cs …
```

---

## 11. Convenciones de nombres

| Elemento | Convención | Ejemplo |
|---|---|---|
| Proyecto host | `Herensuge.<Host>` | `Herensuge.Api`, `Herensuge.Bff` |
| Proyecto común | `Herensuge.Common.<Capa>` | `Herensuge.Common.Domain` |
| Proyecto de módulo | `Herensuge.Modules.<Módulo>.<Capa>` | `Herensuge.Modules.Identity.Application` |
| Proyecto de tests | `<Proyecto>.UnitTests` · `Herensuge.<Tipo>Tests` | `Herensuge.Modules.Identity.UnitTests` |
| Namespace | Proyecto + carpeta | `Herensuge.Modules.Identity.Domain.Users` |
| Carpeta de caso de uso | `<Verbo><Sustantivo>` | `RegisterUser/` |
| Command | `<Verbo><Sustantivo>Command` (+ `Handler`, `Validator`) | `RegisterUserCommand` |
| Query | `Get<Sustantivo>Query` (+ `Handler`, `Response`) | `GetSessionsQuery` |
| Domain event | `<Sustantivo><Participio>DomainEvent` | `UserRegisteredDomainEvent` |
| Integration event | `<Sustantivo><Participio>IntegrationEvent` | `UserRegisteredIntegrationEvent` |
| Errores | `<Agregado>Errors` · código `<Agregado>.<Error>` | `UserErrors.EmailNotVerified` → `Users.EmailNotVerified` |
| API pública de módulo | `I<Módulo>ModuleApi` | `IIdentityModuleApi` |
| Registro en DI | `Add<Módulo>Module()` | `AddTenancyModule()` |
| Esquema de BD | Módulo en minúsculas | `identity`, `tenancy`, `oidc`, `audit`, `notifications` |
| Tablas y columnas | `snake_case`, tablas en plural | `user_sessions.revoked_at` |
| Rutas HTTP | `/api/v1/<recurso-kebab-case>` | `/api/v1/me/recovery-codes` |
| Cookies | `__Host-herensuge-<uso>` | `__Host-herensuge-idp` |
| Clientes OIDC | `herensuge-<app>` | `herensuge-portal` |
| Scopes | `herensuge.<recurso>` | `herensuge.api` |
| Servicios Docker | `herensuge-<servicio>` | `herensuge-postgres` |
| Configuración | Secciones PascalCase; variables de entorno con `__` | `Notifications__Email__Provider` |
| Apps web | `kebab-case` | `apps/portal` |
| Componentes React | `PascalCase.tsx` | `SessionList.tsx` |
| Hooks | `use<Algo>.ts` | `useCurrentUser.ts` |
| Resto de ficheros TS | `kebab-case.ts` | `http-client.ts` |
| Idioma | Código en inglés; interfaz y documentación en español | — |
| Vocabulario | `Tenant` en código, "organización" en la interfaz | — |

---

## 12. API HTTP

### OIDC (OpenIddict)

```
GET       /.well-known/openid-configuration
GET       /.well-known/jwks
GET|POST  /connect/authorize          authorization code + PKCE S256 (obligatorio)
POST      /connect/token              authorization_code, refresh_token
GET|POST  /connect/userinfo
POST      /connect/revocation
POST      /connect/introspection
GET|POST  /connect/logout
```

### Interacción de login — `/api/v1/auth` (cookie del IdP, la usa la SPA `login`)

```
GET   /providers                          proveedores externos configurados
POST  /register
POST  /verify-email                       · POST /verify-email/resend
POST  /sign-in                            · POST /sign-in/mfa · POST /sign-in/mfa/sms
POST  /passkeys/options                   · POST /passkeys/sign-in
GET   /external/{provider}                · GET  /external/{provider}/callback
POST  /password/forgot                    · POST /password/reset
GET   /tenants                            · POST /tenants/select · POST /tenants (onboarding)
GET   /invitations/{token}                · POST /invitations/{token}/accept
```

### Mi cuenta — `/api/v1/me` (Bearer vía BFF)

```
GET | PATCH | DELETE  /                          perfil; DELETE exige re-autenticación
POST    /email                                   cambio de email (verifica el nuevo)
POST    /password
POST    /phone  · POST /phone/verify · DELETE /phone
POST    /mfa/totp · POST /mfa/totp/confirm · DELETE /mfa/totp
POST    /mfa/recovery-codes                      regenerar
GET     /passkeys · POST /passkeys/options · POST /passkeys · PATCH | DELETE /passkeys/{id}
GET     /external-logins · DELETE /external-logins/{provider}
GET     /sessions · DELETE /sessions/{id} · DELETE /sessions (todas menos la actual)
GET     /tenants
```

### Organización — `/api/v1/tenants/{tenantId}` (Owner/Admin salvo que se indique)

```
GET | PATCH   /                                  nombre, require_mfa
GET           /members
PATCH         /members/{userId}                  cambiar rol
DELETE        /members/{userId}                  expulsar (cualquier miembro puede eliminarse a sí mismo)
GET | POST    /invitations · DELETE /invitations/{id}
GET           /audit
```

### Plataforma — `/api/v1/platform` (admin de plataforma + passkey)

```
GET     /tenants · PATCH /tenants/{id}           activar / desactivar
GET | POST /clients · PATCH | DELETE /clients/{id} · POST /clients/{id}/secret (rotar)
GET     /audit
```

### Operación

```
GET /health/live · GET /health/ready · GET /openapi/v1.json (solo en desarrollo)
```

---

## 13. Modelo de datos

| Esquema | Tablas |
|---|---|
| `identity` | `users`, `user_external_logins`, `user_passkeys`, `user_recovery_codes`, `user_known_devices`, `user_sessions`, `one_time_tokens` |
| `tenancy` | `tenants`, `memberships`, `invitations` |
| `oidc` | Tablas de OpenIddict: aplicaciones, autorizaciones, scopes y tokens |
| `audit` | `audit_entries` (append-only: `occurred_at`, `tenant_id?`, `actor_user_id?`, `action`, `target`, `ip`, `user_agent`, `metadata jsonb`) |
| `notifications` | Solo inbox |
| `common` | `data_protection_keys` |

- Cada esquema de módulo tiene además `outbox_messages`, `outbox_message_consumers`, `inbox_messages` e `inbox_message_consumers`.
- **Índices únicos:** `users.email`, `user_external_logins(provider, subject)`, `user_passkeys.credential_id`, `tenants.slug`, `memberships(tenant_id, user_id)`.
- **Valkey** solo guarda estado efímero: sesiones del BFF, retos WebAuthn y locks de refresh. Si se pierde, como mucho los usuarios tienen que volver a hacer login.

---

## 14. Seguridad

| Tema | Decisión |
|---|---|
| Flujo | Solo authorization code + PKCE S256. Sin implicit, sin password grant y sin tokens en el navegador |
| Access token | JWT firmado, 5 min. Claims: `sub`, `sid`, `tenant_id`, `tenant_role`, `amr`, `auth_time`, `scope` |
| Refresh token | 7 días, con **rotación** en cada uso. Si se reutiliza, OpenIddict revoca toda la cadena (con un margen configurable para carreras) |
| Sesión del IdP | 14 días deslizantes con un máximo absoluto de 30 días; se revalida contra `UserSession` cada minuto. Un nuevo inicio de sesión en el mismo navegador revoca la sesión anterior |
| Contraseñas | Argon2id con el mínimo de OWASP (19 MiB, t=2, p=1), configurable y ajustado con prueba de carga. Mínimo 12 caracteres, sin reglas de composición |
| Bloqueo | 5 fallos → 15 min de bloqueo + aviso por email |
| Rate limit | 10 peticiones/min por IP en `/api/v1/auth/*` (límite por instancia en el Nivel 1) |
| Enumeración de usuarios | Registro y "olvidé mi contraseña" responden igual exista o no el email |
| MFA | TOTP (RFC 6238), SMS y passkey. 10 códigos de recuperación hasheados. Obligatorio si alguna organización del usuario lo exige |
| Passkeys | WebAuthn con verificación de usuario. RP ID = dominio registrable (`<dominio>`, o `localhost` en desarrollo); orígenes permitidos: `id.` y `portal.` |
| Admin de plataforma | Exige rol `platform_admin` y `amr` con `pop` (passkey). El primero sale de la configuración `Bootstrap:PlatformAdminEmail` |
| Re-autenticación | Las operaciones sensibles exigen `auth_time` de menos de 10 min |
| Cookies | Prefijo `__Host-`, `HttpOnly`, `Secure`. IdP: `SameSite=Lax` (necesario para el SSO). BFF: `SameSite=Strict` |
| CSRF | Cabecera `X-CSRF: 1` + JSON en toda petición que modifica datos, en los dos hosts. CORS deshabilitado |
| Cabeceras | CSP estricta, `frame-ancestors 'none'` (anticlickjacking en el login), HSTS, `X-Content-Type-Options`, `Referrer-Policy` |
| Secretos en reposo | Secretos TOTP cifrados con Data Protection. Tokens de un solo uso y códigos de recuperación hasheados (SHA-256). En producción, el key ring se protege con certificado |
| Claves de firma | Certificados de desarrollo en local. En producción, X.509 desde un almacén de secretos, con rotación |
| Redirecciones | `returnUrl` solo local. Las `redirect_uris` de cada cliente se comparan exactamente |
| Consentimiento | Implícito: todos los clientes del Nivel 1 son apps propias |
| Logs | Nunca se registran contraseñas, tokens, secretos ni cookies. La única excepción es el proveedor `Log` de notificaciones, y no puede activarse en producción |
| TLS | 1.3 |

---

## 15. Stack y paquetes

### 15.1 Infraestructura

| Pieza | Elección | Licencia |
|---|---|---|
| Runtime | .NET 10 / ASP.NET Core | MIT |
| Protocolo | OpenIddict | Apache-2.0 |
| Base de datos | PostgreSQL | PostgreSQL License |
| Caché / estado efímero | Valkey | BSD-3-Clause |
| Observabilidad | OpenTelemetry → Grafana, Loki, Tempo, Prometheus (Alloy en producción) | Apache-2.0 / AGPL-3.0 |
| Mensajería | RabbitMQ: **preparado, pero no se despliega en el Nivel 1** | MPL-2.0 |

### 15.2 NuGet

| Paquete | Dónde | Licencia |
|---|---|---|
| `OpenIddict.AspNetCore` | Api, Bff | Apache-2.0 |
| `OpenIddict.EntityFrameworkCore` | Oidc.Infrastructure | Apache-2.0 |
| `OpenIddict.Client.WebIntegration` | Identity.Infrastructure (Google, Microsoft) | Apache-2.0 |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | Common.Infrastructure | PostgreSQL |
| `EFCore.NamingConventions` | Common.Infrastructure | Apache-2.0 |
| `Microsoft.EntityFrameworkCore.Design` | Api (migraciones) | MIT |
| `Dapper` | Application de cada módulo (queries) | Apache-2.0 |
| `FluentValidation.DependencyInjectionExtensions` | Common.Application | Apache-2.0 |
| `Scrutor` | Common.Application (escaneo y decoradores) | MIT |
| `Konscious.Security.Cryptography.Argon2` | Identity.Infrastructure | MIT |
| `Otp.NET` | Identity.Infrastructure | MIT |
| `Fido2` | Identity.Infrastructure | MIT |
| `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` | Api | MIT |
| `Microsoft.AspNetCore.DataProtection.StackExchangeRedis` | Bff | MIT |
| `Microsoft.Extensions.Caching.StackExchangeRedis` | Api, Bff | MIT |
| `Yarp.ReverseProxy` | Bff; Api (solo en desarrollo, proxy a Vite) | MIT |
| `Microsoft.AspNetCore.OpenApi` | Api | MIT |
| `OpenTelemetry.Extensions.Hosting`, `.Exporter.OpenTelemetryProtocol`, `.Instrumentation.AspNetCore`, `.Instrumentation.Http` | Common.Infrastructure | Apache-2.0 |
| `Npgsql.OpenTelemetry` | Common.Infrastructure | PostgreSQL |

**Tests:** `xunit.v3`, `Microsoft.AspNetCore.Mvc.Testing`, `Testcontainers.PostgreSql`, `Testcontainers.Redis` (con imagen de Valkey), `NetArchTest.Rules`, `Microsoft.Playwright`.

**Ya incluidos en el framework (sin NuGet):** autenticación por cookies, autorización por policies, rate limiting, health checks, `ProblemDetails`.

### 15.3 Versiones

Todas las versiones van fijadas y centralizadas: NuGet en `Directory.Packages.props`, npm en el catálogo de `pnpm-workspace.yaml` y el SDK en `global.json`.

### 15.4 Prohibidos

| Paquete | Motivo |
|---|---|
| MediatR, AutoMapper | Licencia comercial desde 2025 |
| MassTransit v9+ | Licencia comercial |
| FluentAssertions v8+ | Licencia comercial |
| Duende IdentityServer / Duende BFF | Licencia comercial |
| ASP.NET Core Identity | No es por licencia: su modelo anémico choca con el agregado `User` (ver sección 20) |

---

## 16. Observabilidad

- **Señales:** los dos hosts exportan trazas (ASP.NET Core, HttpClient, Npgsql), métricas y logs por **OTLP**.
- **Desarrollo:** un único contenedor `grafana/otel-lgtm` (collector + Loki + Tempo + Prometheus + Grafana), con Grafana en `:3000`.
- **Producción:** Alloy → Loki, Tempo y Prometheus → Grafana.
- **Trazas de punta a punta:** el `traceparent` viaja dentro de outbox e inbox, así que una misma traza cubre petición → evento → email.
- **Métricas propias:**
  - `herensuge.sign_in.attempts{result,method}`
  - `herensuge.mfa.challenges{method,result}`
  - `herensuge.messaging.pending{module,kind}` (kind = outbox o inbox)
  - `herensuge.messaging.processed{module,kind,result}` (result = success, failure o dead)
  - `herensuge.notifications.sent{channel,provider,result}`
- **Health checks:** `/health/live` (proceso) y `/health/ready` (PostgreSQL + Valkey).

---

## 17. Testing

| Tipo | Proyecto | Qué cubre | Herramientas |
|---|---|---|---|
| Unitarios | `Herensuge.Modules.<Módulo>.UnitTests` | Agregados e invariantes: bloqueo, último método de acceso, último `Owner`, caducidad y único uso de tokens | xUnit |
| Arquitectura | `Herensuge.ArchitectureTests` | Regla de dependencias de capas; un módulo solo ve los `Contracts` de los demás | NetArchTest |
| Integración | `Herensuge.IntegrationTests` | Endpoints + handlers con PostgreSQL y Valkey reales; outbox → inbox → handler | WebApplicationFactory + Testcontainers |
| End-to-end | `Herensuge.EndToEndTests` | Flujo OIDC completo en el navegador: registro → verificar email → login → MFA → portal → logout | Playwright + fake de `IEmailSender` que captura los mensajes |
| Frontend | `Src/Web` | Componentes y hooks con lógica | Vitest + Testing Library |

`dotnet test` ejecuta todo el backend y el E2E; `pnpm test` ejecuta el frontend.

---

## 18. Ejecución local y Docker

**Requisitos:** .NET 10 SDK, Node LTS + pnpm, Docker.

```bash
# Todo en contenedores, detrás de Caddy (TLS): ver Docker/README.md
cp Docker/.env.example Docker/.env      # cambiar los secretos
docker compose -f Docker/compose.yaml up -d --build

# O desarrollo con hot reload
docker compose -f Docker/compose.dev.yaml up -d      # PostgreSQL + Valkey (+ --profile observability)
dotnet dev-certs https --trust
pnpm -C Src/Web install && pnpm -C Src/Web dev        # Vite: login :5173, portal :5174
dotnet run --project Src/Hosts/Herensuge.Api          # https://localhost:5001
dotnet run --project Src/Hosts/Herensuge.Bff          # https://localhost:5002
```

| Servicio | URL |
|---|---|
| Portal | `https://localhost:5002` |
| IdP / login | `https://localhost:5001` |
| Grafana | `http://localhost:3000` |
| Emails y SMS | En la consola de `Herensuge.Api` y en Grafana (Loki) |

**Datos iniciales en desarrollo:**
- El cliente `herensuge-portal` y el scope `herensuge.api`.
- El usuario que verifique el email de `Identity:BootstrapPlatformAdminEmail` se convierte en admin de plataforma.

---

## 19. Definición de hecho del Nivel 1

**Cuenta**
- [ ] Registro con email y contraseña + verificación de email
- [ ] Login con contraseña, bloqueo y rate limit
- [ ] Login con Google y Microsoft (si están configurados) con vinculación segura
- [ ] Recuperar contraseña
- [ ] MFA: TOTP + códigos de recuperación; SMS como alternativa con teléfono verificado
- [ ] Passkeys: alta, login sin contraseña, renombrar y eliminar
- [ ] Sesiones: listar, cerrar una y cerrar todas las demás
- [ ] Perfil, cambio de email, cambio de contraseña, cuentas vinculadas y borrado de cuenta
- [ ] Avisos de seguridad por email (nuevo dispositivo, cambios de credenciales)

**Organización**
- [ ] Onboarding (crear organización) y selector de organización
- [ ] Invitar, aceptar invitación, cambiar rol, expulsar y abandonar
- [ ] Política de MFA obligatoria por organización
- [ ] Auditoría de la organización

**Plataforma**
- [ ] Clientes OIDC: crear, editar redirect URIs, rotar secreto y desactivar
- [ ] Listar organizaciones y activarlas o desactivarlas
- [ ] Auditoría global

**OIDC y BFF**
- [ ] Discovery, JWKS, authorize, token, userinfo, revocation, introspection y logout
- [ ] BFF: login, callback, user, logout, proxy YARP con refresh y rotación

**Operación**
- [ ] `docker compose up` levanta todo y funciona sin ninguna API key
- [ ] Migraciones, datos iniciales, health checks y OpenTelemetry
- [ ] Tests de arquitectura, unitarios, integración y E2E en verde en la CI

---

## 20. Decisiones de esta revisión

| # | Decisión | Por qué |
|---|---|---|
| 1 | **5 módulos** (Identity, Tenancy, Oidc, Audit, Notifications) en vez de 10 | Los antiguos Users, Authentication, Mfa, Passkeys y Sessions eran un mismo bounded context |
| 2 | **Clean Architecture por módulo**, con un `.csproj` por capa | El compilador garantiza la regla de dependencias |
| 3 | **Sin ASP.NET Core Identity** | Su modelo anémico choca con el agregado `User`. Se usan directamente Argon2id, Otp.NET y Fido2 |
| 4 | **Fido2 en vez de las passkeys nativas de .NET 10** | Las nativas dependen de `UserManager`/`SignInManager` (consecuencia de la decisión 3) |
| 5 | **Outbox + inbox + bus en memoria; RabbitMQ fuera del Nivel 1** | Dan entrega fiable dentro del monolito. `IEventBus` permite enchufar RabbitMQ al extraer un módulo |
| 6 | **CQRS sin mediador**: handlers propios + decoradores Scrutor; queries con Dapper | MediatR es comercial y un mediador no aporta nada aquí |
| 7 | **Roles fijos** (`Owner`, `Admin`, `Member` + admin de plataforma) | Los roles y permisos personalizados son Nivel 2; se elimina la tabla `permissions` |
| 8 | **Una sola SPA `portal`** en vez de AccountPortal + AdminPortal | Mismo BFF y misma sesión; las secciones se cargan *lazy* por rol |
| 9 | **Login en React servido por `Herensuge.Api`** | Mismo origen que el IdP, sin CORS |
| 10 | **Cabecera `X-CSRF` en los dos hosts** | Un solo mecanismo anti-CSRF |
| 11 | **Sin `/connect/register`** | Los clientes se crean desde Plataforma; OpenIddict no implementa registro dinámico de serie |
| 12 | **Sin Serilog** | Logging nativo + exportador OTLP |
| 13 | **`grafana/otel-lgtm` en desarrollo** | Un contenedor en vez de cinco |
| 14 | **Bloqueo de 15 min + aviso** en vez de escalar hasta 24 h | Un atacante no puede bloquear cuentas ajenas durante un día |
| 15 | **Argon2id con el mínimo de OWASP** en vez de 64 MB | 64 MB por login concurrente abre la puerta a un DoS |
| 16 | **Usuario global con membresías por organización** | Una persona puede estar en varias organizaciones con una sola cuenta, como en Slack o Notion |
| 17 | **Desarrollo con la misma topología que producción** | Los hosts son la entrada también en desarrollo y hacen de proxy a Vite |

---

## 21. Pendiente

- [ ] Elegir el proveedor real de email (Resend, SES, Mailgun) y de SMS (Twilio, Bird, Vonage): precios actuales en España y coste para 10.000 usuarios.
- [ ] RGPD: DPA con cada proveedor, ubicación de los datos y exportación de datos del usuario.
- [ ] Revisar la retención: auditoría 365 días (`Audit:RetentionDays`), sesiones terminadas 30 días, tokens 1 día, mensajes procesados 7 días.
- [ ] Hosting (VPS Hetzner/OVH o Kubernetes) y dominio de producción.
- [ ] Escribir los ADRs de la sección 20 en `Docs/Decisiones/`.
- [ ] Diseñar el Nivel 2.

---

## 22. Estado de la implementación (backend)

**Hecho:** backend completo del Nivel 1 (`Herensuge.Api` + `Herensuge.Bff`), migraciones, tests, Dockerfiles, `compose.yaml` y CI. Faltan las SPAs `login` y `portal` y, con ellas, el proyecto E2E.

**Verificado en la máquina de desarrollo (sin Docker):**
- La solución compila con los warnings tratados como errores.
- 44 tests unitarios y 24 de arquitectura en verde.
- Ambos hosts arrancan: el grafo de dependencias y los modelos EF se construyen.
- Las consultas Dapper se han cotejado con el SQL de las migraciones.
- `compose.yaml` y `compose.dev.yaml` validan.
- Ningún paquete vulnerable.

**Sin verificar todavía:** la ejecución contra PostgreSQL y Valkey reales. Los tests de integración (flujo OIDC completo, bloqueo, MFA, outbox → auditoría) están escritos y necesitan Docker. Las imágenes Docker no se han construido.

**Ajustes respecto a las secciones anteriores, decididos al implementar:**

| Tema | Ajuste |
|---|---|
| Logout del BFF | `GET /bff/logout?sid=…` (navegación) en vez de `POST` con `X-CSRF` |
| Transacciones | Sin decorador `Transaction`: un `SaveChangesAsync` por handler |
| Revocación | Sin `SecurityStamp`: cambiar o restablecer la contraseña revoca sesiones y, con ellas, sus tokens |
| Usuarios | Borrado físico (RGPD), sin campo `Status` |
| Sesión SSO | Máximo absoluto de 30 días |
| Despliegue | Caddy (TLS) y servicio migrador en `compose.yaml`; `compose.dev.yaml` para desarrollo |
| API de login | Añadidos `GET /session`, `POST /sign-out`, `GET /sign-in/mfa`, `POST /mfa/totp[/confirm]` (alta exigida por la organización) y `POST /email-change/confirm`; `GET /api/v1/me/tenants` |
| Valkey | Opcional en `Development` (caché en memoria); obligatorio en el resto |
| NuGet | `nuget.config` propio (solo nuget.org) y *transitive pinning* (`System.Formats.Cbor` parcheado) |
| Tests | Microsoft.Testing.Platform (`dotnet test --project …`) |

**Siguientes pasos:** ejecutar los tests de integración y `docker compose up` en la máquina con Docker, implementar las SPAs (sección 9), elegir los proveedores reales de email y SMS y redactar los ADRs.
