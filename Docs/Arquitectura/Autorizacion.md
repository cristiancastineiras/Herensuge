# Herensuge Access: plan de autorización (Nivel 2)

> **Estado:** en desarrollo (fase 1: backend hecho, falta la parte web) · **Fecha:** 16/11/2026
> Cuando se apruebe, sustituye la decisión 7 del [RESUMEN](../../RESUMEN.md#20-decisiones-de-esta-revisión) ("roles fijos").
> Las decisiones marcadas como **ADR-A*n*** se redactan en `Docs/Decisiones/` antes de empezar cada fase.

---

## 0. En diez líneas

1. **Qué:** que cada aplicación del SaaS pueda decir *qué puede hacer cada usuario, en cada organización, sobre sus recursos*.
2. **Piezas:** un catálogo de permisos por aplicación, roles personalizables por organización, planes contratados por organización y una API de decisión.
3. **Dónde:** un módulo nuevo, `Access`, dentro de `Herensuge.Api`. No es un servicio aparte: sigue el monolito modular y se puede extraer después.
4. **Contrato de decisión:** la forma de **OpenID AuthZEN** (`POST /access/v1/evaluation`), no una API inventada.
5. **Tokens:** cada access token va a una sola API y lleva sus `roles` y `permissions` (RFC 8707 + RFC 9068). Lo grueso se comprueba en local, sin red.
6. **Lo sensible o por objeto** se consulta en vivo a la API de decisión: revocación inmediata, igual que las sesiones.
7. **No construimos un Zanzibar.** Cuando haga falta autorización por objeto (compartir *este* documento), se usa **OpenFGA**.
8. **Primero condiciones fijas** (MFA, inicio de sesión reciente, IP), no un lenguaje de políticas general.
9. **Cuatro fases**, cada una usable por sí misma: RBAC por app → planes, grupos y cuentas de servicio → ReBAC → delegación.
10. **Validación:** una app de ejemplo (`Facturas`) se integra **sin tocar el código de Herensuge**, y Herensuge usa su propio sistema de permisos.

---

## 1. Vocabulario

"Authorization Server" significa cosas distintas según quién lo diga. En OAuth, **Herensuge ya es el Authorization Server**: emite los tokens. Lo nuevo son las piezas del modelo clásico de control de acceso (XACML/AuthZEN):

| Término | Qué es | En Herensuge |
|---|---|---|
| **AS** (OAuth Authorization Server) | Emite tokens | `Herensuge.Api` (OpenIddict). Ya existe |
| **PAP** (Policy Administration Point) | Donde se gestionan roles, permisos y planes | Nuevo: API de gestión + portal |
| **PDP** (Policy Decision Point) | Decide *permit* o *deny* | Nuevo: `POST /access/v1/evaluation` |
| **PEP** (Policy Enforcement Point) | Aplica la decisión | La API de cada app (SDK .NET). La UI solo esconde botones (SDK React) |
| **PIP** (Policy Information Point) | Aporta los datos para decidir | Identity (sesión, `amr`), Tenancy (membresía), Access (roles, planes) |
| **Resource server** | Una API protegida | Cada API del SaaS, con su audiencia (`aud`) |
| **Permiso** | `recurso:acción` | `invoices:approve` |
| **Rol** | Conjunto de permisos | "Responsable financiero" en la organización Acme |
| **Feature / entitlement** | Lo que la organización tiene **contratado** | `invoices.approval` en el plan Pro |

**Permiso ≠ entitlement.** Que Acme tenga contratada la aprobación de facturas no significa que todos sus usuarios puedan aprobar. El entitlement habilita la funcionalidad para la organización; el permiso decide qué personas la usan.

---

## 2. Punto de partida

**Ya existe:**
- Roles fijos por organización en `Tenancy` (`Membership.Role`: `owner`, `admin`, `member`) y el admin de plataforma (`role=platform_admin` + passkey).
- Claims del access token: `sub`, `sid`, `tenant_id`, `tenant_role`, `amr`, `auth_time`, `role` y `scope`.
- Policies `Api` y `PlatformAdmin`. Las operaciones de organización se comprueban contra la base de datos con `TenantAccess`, porque el claim puede tener hasta 5 minutos.
- Revocación inmediata de sesiones: comprobación en cada petición más el canal de Valkey `herensuge:session-revoked` hacia las BFF (`/bff/events`).
- Outbox/inbox, el módulo `Audit`, clientes OIDC gestionados desde Plataforma, la BFF como plantilla para cada app y `@herensuge/react`.

**Falta:**
- Una sola API protegida (`herensuge.api` → audiencia `herensuge-api`). No hay forma de registrar las APIs de otras apps.
- Roles no configurables y ningún permiso fino.
- Ni planes ni features por organización.
- Ninguna máquina puede autenticarse: solo hay *authorization code* y *refresh token*, sin `client_credentials`.

---

## 3. Objetivos y no objetivos

**Objetivos (verificables):**
- [ ] Integrar una app nueva = un manifiesto de permisos + el SDK, **sin cambiar código de Herensuge**.
- [ ] El owner o admin de una organización crea roles y los asigna desde el portal.
- [ ] Plataforma define planes; un permiso de una feature no contratada se deniega.
- [ ] Quitar un rol corta el acceso en la API de decisión en **< 1 s** y en los tokens en **≤ 5 min** (lo que dura un access token).
- [ ] Decisión en la PDP: **p99 < 5 ms** con caché caliente y **< 25 ms** en frío. Si falla, deniega (*fail-closed*).
- [ ] Todo cambio de roles, asignaciones y planes queda auditado. Toda denegación se mide.

**No objetivos del Nivel 2:**
- Un lenguaje de políticas general (Rego, Cedar…) y XACML.
- UMA 2.0. En B2B decide el admin de la organización, no el "propietario" de cada recurso.
- Consentimiento de terceros y RAR (RFC 9396), mientras no haya una API pública para terceros.
- *Deny* explícito y resolución de conflictos entre reglas.
- SCIM, SSO por organización y el resto del Nivel 2: se planifican aparte.

---

## 4. Qué tomo y qué cambio de la propuesta inicial

| Propuesta inicial | Decisión | Por qué |
|---|---|---|
| Authorization Server como **servicio independiente** | **Módulo `Access`** dentro de `Herensuge.Api`, con API propia y extraíble | Principio 3 del RESUMEN (monolito modular). Comparte outbox, auditoría y base de datos, y la PDP queda a una llamada en proceso del emisor de tokens |
| 8 motores en orden (recursos, permisos, roles, políticas, relaciones…) | **4 fases**, cada una entregable | Al final de la fase 1 ya hay un producto usable |
| Construir un motor de relaciones tipo Zanzibar | **Adoptar OpenFGA** en la fase 3 | Principio 2 (no reinventar). La consistencia y el *new enemy problem* de Zanzibar son difíciles de hacer bien |
| PBAC/ABAC con políticas generales | **Condiciones fijas** (`mfa`, `recent_auth`, IP). Cedar/CEL solo si se quedan cortas | Cubren los casos reales de un SaaS B2B y se explican en una línea |
| UMA 2.0, prioridad alta | **Baja** | Encaja con "el usuario comparte *su* recurso", no con organizaciones |
| RFC 9396 (RAR), prioridad muy alta | **Solo con clientes de terceros** | Sirve para pedir permisos finos en el flujo OAuth, no para el RBAC interno |
| API propia `POST /authorization/check` | **OpenID AuthZEN** (`/access/v1/evaluation`) | Es el estándar para hablar con una PDP: PEPs y PDPs intercambiables |
| — | **Añadidos:** AuthZEN, RFC 9068 (`roles` y `entitlements` en el JWT), RFC 8693 (token exchange) | Faltaban, y son los que más se usan en la práctica |

---

## 5. Arquitectura

```
                Navegador
                    │ cookie de la BFF
                    ▼
            BFF de la app (plantilla Herensuge.Bff)
                    │ access token: aud=facturas-api, roles, permissions
                    ▼
            API de la app ── PEP (SDK .NET)
              │  1. permiso en el token        → decide en local
              │  2. sensible / por objeto       → POST /access/v1/evaluation
              ▼
┌───────────────────────── Herensuge.Api ─────────────────────────┐
│ Identity · Tenancy · Oidc · Access (nuevo) · Audit · Notifications│
│                                                                   │
│ Oidc ──IAccessModuleApi──► Access: roles/permisos para el token   │
│ Access ──ITenancyModuleApi──► Tenancy: membresía y rol base       │
│ Access ──IIdentityModuleApi──► Identity: sesión activa            │
└─────────────┬───────────────────────────────────┬─────────────────┘
              │ outbox → RoleAssigned, …          │ (fase 3)
              ▼                                   ▼
   Valkey "herensuge:access-changed"           OpenFGA
   → caché de la PDP y BFF (/bff/events)
```

**Módulo `Access`** (bounded context *Core*), con la misma forma que Identity y Tenancy: `Domain`, `Application`, `Infrastructure`, `Presentation` y `Contracts`. El esquema es `access`.

- `Access.Contracts` → `IAccessModuleApi`. Lo usa `Oidc` para meter claims en el token y `Tenancy` para el *dogfooding* (sección 6.4).
- `Access.Application` solo ve los `Contracts` de Tenancy e Identity. Lo comprueban los tests de arquitectura (añadir `Access` a `ModuleNames`).
- No hay ciclo entre `Tenancy.Application → Access.Contracts` y `Access.Application → Tenancy.Contracts`: los `Contracts` son ensamblados aparte y la implementación se resuelve por DI.

**Regla "un token, una API" (ADR-A4).** La BFF de cada app pide el scope de *su* API (por ejemplo `facturas.api`). Así el token lleva una sola audiencia y sus `permissions` no se mezclan con los de otra app. El portal sigue con `herensuge.api`.

---

## 6. Modelo de dominio y datos

### 6.1 Agregados

| Agregado | Contiene | Invariantes |
|---|---|---|
| `ResourceServer` | `Key` (`facturas`), `Name`, `Audience` (`facturas-api`), `Scope` (`facturas.api`), `EmbedPermissions`, `ManifestVersion`, `Features[]`, `Permissions[]`, `RoleTemplates[]` | `Key`, `Audience` y `Scope` únicos. Formato de permiso `^[a-z][a-z0-9-]*:[a-z][a-z0-9-]*$`. Un permiso en uso no se borra: se **retira** (`DeprecatedAt`) |
| `Permission` (hijo) | `Key`, `ResourceType`, `Action`, `Description`, `FeatureKey?`, `Requires` (`none` · `mfa` · `recent_auth`) | Si tiene `FeatureKey`, la feature existe en el mismo resource server |
| `RoleTemplate` (hijo) | Rol definido por la app: `Key`, `Name`, `Permissions[]`, `GrantTo[]` (roles base: `owner`, `admin`, `member`) | Solo lo cambia el manifiesto. Las organizaciones lo ven, pero no lo editan |
| `Role` | Rol de una organización: `TenantId`, `Key`, `Name`, `Description`, `PermissionIds[]` | `Key` único por organización. Como máximo 100 roles por organización y 500 permisos por rol |
| `RoleAssignment` | `TenantId`, `SubjectType` (`user` · `group` · `client`), `SubjectId`, `RoleRef` (rol o plantilla), `ExpiresAt?`, `GrantedBy`, `GrantedAt` | El usuario es miembro de la organización. Una asignación caducada no cuenta. Se borra con `MemberRemoved` y `UserDeleted` |
| `Group` *(fase 2)* | `TenantId`, `Name`, `Members[]` | Nombre único por organización. Solo miembros de la organización |
| `Plan` *(fase 2)* | `Key`, `Name`, `Features[]` (`FeatureKey` + `Limits` en JSON) | — |
| `TenantSubscription` *(fase 2)* | `TenantId`, `PlanKey`, `StartsAt`, `EndsAt?`, `Overrides[]` (activa o desactiva features sueltas para acuerdos especiales) | Una sola suscripción activa por organización |

`ExpiresAt` en las asignaciones existe desde la fase 1: abarata el acceso temporal (fase 4) y no cuesta nada.

### 6.2 Tablas (esquema `access`)

```
resource_servers        (id, key, name, audience, scope, embed_permissions, manifest_version, status, created_at)
features                (id, resource_server_id, key, name)
permissions             (id, resource_server_id, key, resource_type, action, description, feature_id?, requires, deprecated_at?)
role_templates          (id, resource_server_id, key, name, description)
role_template_permissions (role_template_id, permission_id)
role_template_grants    (role_template_id, base_role)                  -- owner | admin | member
roles                   (id, tenant_id, key, name, description, created_at, xmin)
role_permissions        (role_id, permission_id)
role_assignments        (id, tenant_id, subject_type, subject_id, role_id?, role_template_id?, expires_at?, granted_by, granted_at)
groups / group_members                                                 -- fase 2
plans / plan_features / tenant_subscriptions / tenant_feature_overrides -- fase 2
+ outbox_messages, outbox_message_consumers, inbox_messages, inbox_message_consumers
```

Índices: `roles(tenant_id, key)` único y `role_assignments(tenant_id, subject_type, subject_id)`. Sin claves foráneas hacia otros esquemas (regla del RESUMEN 6.7).

### 6.3 Roles base y plantillas (ADR-A7)

- Los roles base (`owner`, `admin`, `member`) **siguen en Tenancy**. Son la membresía y gobiernan la gestión de la propia organización.
- Una plantilla de app puede concederse **por rol base** (`grantTo`). Así, el día que se instala una app, el owner ya puede usarla sin asignar nada a mano.
- Si una organización necesita algo distinto, crea **roles propios** combinando permisos de cualquiera de sus apps.
- **Sin superusuario implícito:** el owner tiene lo que le dan las plantillas con `grantTo: owner` y lo que él mismo se asigne. El admin de plataforma no tiene acceso a los datos de las organizaciones (lo cubre la impersonación auditada de la fase 4).

### 6.4 Dogfooding: Herensuge como resource server

Herensuge se registra como resource server `herensuge` con permisos como `members:read`, `members:invite`, `members:remove`, `roles:manage`, `tenant:update` y `audit:read`.

- Las plantillas `owner` y `admin` se conceden por rol base, así que el comportamiento actual no cambia.
- `TenantAccess.RequireAdministratorAsync` pasa a `RequirePermissionAsync("members:invite")` vía `IAccessModuleApi`.
- Si Herensuge no puede usar su propio sistema de permisos, las apps tampoco: este paso valida el diseño.

---

## 7. Cómo se decide

```
decide(sujeto, organización, resource server, permiso, contexto):
  1. resource server activo, permiso existe y no retirado      → deny(unknown_permission)
  2. sesión activa (si hay sid) y miembro de organización activa → deny(not_member)
  3. feature del permiso contratada por la organización (fase 2) → deny(not_entitled)
  4. roles efectivos = asignaciones directas no caducadas
                     ∪ asignaciones de sus grupos (fase 2)
                     ∪ plantillas concedidas por su rol base
     ¿alguno contiene el permiso?                                → deny(no_role)
  5. condición del permiso:
       mfa         → amr contiene "mfa"                          → deny(step_up_required, acr=mfa)
       recent_auth → auth_time hace menos de 10 min              → deny(step_up_required, max_age=600)
  6. permit(reason = "role:<key>")
```

- **Deny por defecto y sin *deny* explícito** (ADR-A6): no hay conflictos que resolver y la traza es una lista de pasos.
- Las razones son códigos que la SPA traduce. `step_up_required` lleva a `/bff/login?max_age=0`, igual que hoy `reauth_required`.
- **Caché:** el conjunto efectivo de permisos por `(organización, usuario, resource server)` vive en memoria y en Valkey, con un número de versión por organización.
- **Invalidación:** cada cambio (asignación, rol, plan, membresía) sube la versión y publica en `herensuge:access-changed`, con el mismo patrón que `herensuge:session-revoked`. La PDP descarta la caché y las BFF emiten `permissions-changed` por `/bff/events`.
- **Consistencia:** el número de versión se sube dentro de la misma transacción que el cambio. Una decisión con una versión anterior no se reutiliza (versión simplificada del *new enemy problem*).

---

## 8. APIs

### 8.1 API de decisión (PDP), forma OpenID AuthZEN (ADR-A2)

Hay que comprobar la versión vigente de *AuthZEN Authorization API* al implementar: nombres de campos, endpoints de búsqueda y *discovery*.

```http
POST /access/v1/evaluation
Authorization: Bearer <token client_credentials del resource server, scope herensuge.access>
```

```json
{
  "subject":  { "type": "user", "id": "0193…", "properties": { "tenant_id": "0193…", "amr": ["pwd", "otp", "mfa"], "auth_time": 1794830400 } },
  "action":   { "name": "approve" },
  "resource": { "type": "invoice", "id": "inv-123", "properties": { "resource_server": "facturas" } },
  "context":  { "ip": "10.0.0.1" }
}
```

```json
{ "decision": false, "context": { "reason": "step_up_required", "max_age": 600 } }
```

| Endpoint | Para qué | Fase |
|---|---|---|
| `POST /access/v1/evaluation` | Una decisión | 1 |
| `POST /access/v1/evaluations` | Lote: qué botones pintar en una pantalla, en una sola llamada | 1 |
| `GET /.well-known/authzen-configuration` | *Discovery* de la PDP | 1 |
| `POST /access/v1/search/resource` | "¿Qué facturas puedo aprobar?" (ListObjects de OpenFGA) | 3 |

**Seguridad de la PDP:** solo la llaman resource servers autenticados con `client_credentials` y el scope `herensuge.access`. Cada cliente solo puede preguntar por los permisos de **sus** resource servers. Que pregunte por otro devuelve `403`, no `deny`.

### 8.2 API de gestión (PAP)

Sigue las convenciones del RESUMEN 12.

**Plataforma** (`/api/v1/platform`, admin de plataforma + passkey)
```
GET | POST          /resource-servers
GET | PATCH | DELETE /resource-servers/{key}
PUT                 /resource-servers/{key}/manifest      sincroniza el catálogo (idempotente; también lo llama la CI de la app)
GET | POST          /plans · PATCH | DELETE /plans/{key}                      (fase 2)
PUT                 /tenants/{tenantId}/subscription                          (fase 2)
```

**Organización** (`/api/v1/tenants/{tenantId}`, permiso `herensuge:roles:manage`)
```
GET                 /permissions                  catálogo de las apps de la organización, agrupado por app
GET | POST          /roles · GET | PATCH | DELETE /roles/{roleId}
GET | PUT           /members/{userId}/roles       asignaciones (con caducidad opcional)
GET | POST          /groups · …                   (fase 2)
POST                /access/simulate              "¿puede X hacer Y?" con la traza de pasos   (fase 2)
```

**Mi cuenta:** `GET /api/v1/me/permissions?resourceServer=facturas`.

**BFF:** `/bff/user` añade `permissions` (los de la API de esa BFF) y `/bff/events` emite `permissions-changed`.

### 8.3 Manifiesto de la app: el catálogo como código (ADR-A5)

Cada app versiona `herensuge.access.json` en su repositorio. Su CI lo sincroniza con `PUT /resource-servers/{key}/manifest`, autenticada con su cliente.

```json
{
  "resourceServer": "facturas",
  "name": "Facturación",
  "audience": "facturas-api",
  "scope": "facturas.api",
  "embedPermissions": true,
  "features": [{ "key": "invoices.approval", "name": "Aprobación de facturas" }],
  "permissions": [
    { "key": "invoices:read", "description": "Ver facturas" },
    { "key": "invoices:create", "description": "Crear facturas" },
    { "key": "invoices:approve", "description": "Aprobar facturas", "feature": "invoices.approval", "requires": "mfa" }
  ],
  "roles": [
    { "key": "finance-manager", "name": "Responsable financiero", "permissions": ["invoices:*"], "grantTo": ["owner"] },
    { "key": "viewer", "name": "Consulta", "permissions": ["invoices:read"], "grantTo": ["admin", "member"] }
  ]
}
```

Reglas de sincronización:
- **Permiso nuevo:** se añade.
- **Permiso que desaparece:** se retira (`deprecated_at`). Deja de conceder acceso, pero no se borra mientras algún rol lo use. El portal avisa en esos roles.
- **Renombrar** = retirar uno + añadir otro. No hay renombrado implícito.
- **Comodines** (`invoices:*`): se expanden **al sincronizar**, no al decidir. Son explícitos y auditables, y cada sincronización los recalcula.
- La sincronización publica `ResourceServerManifestSynced` (auditoría) e invalida cachés.

---

## 9. SDKs (PEP)

**.NET** (`Herensuge.Access.AspNetCore`; en este repositorio, `Src/Sdk/`):

```csharp
builder.Services.AddHerensugeAccess(options =>
{
    options.Authority = "https://id.example.com";
    options.ResourceServer = "facturas";
});

app.MapGet("/invoices", ListInvoices).RequirePermission("invoices:read");      // claim del token, sin red
app.MapPost("/invoices/{id}/approve", Approve).RequirePermission("invoices:approve"); // requires=mfa → PDP

// Por objeto o con contexto
if (!await access.CheckAsync(user, "invoices:approve", new AccessResource("invoice", id), cancellationToken))
{
    return Results.Forbid();
}
```

Estrategia del SDK:
1. Si el permiso está en el claim `permissions` y no es sensible, decide en local.
2. Si no viene en el token o es sensible, llama a la PDP. Sin caché en el SDK para lo sensible.
3. Si la PDP no responde, deniega y devuelve `503` al cliente. Nunca concede.

**React** (`@herensuge/react`, ya existe):
- `useCan("invoices:approve")` y `<Can permission="invoices:approve">…</Can>`, a partir de `user.permissions`.
- El SDK vuelve a pedir `/bff/user` al recibir `permissions-changed`.
- Solo sirve para la UX: la autorización real la aplica siempre la API (regla ya vigente en `Src/Web/README.md`).

**BFF:** la plantilla añade `permissions` a `/bff/user` y reenvía `permissions-changed`. Una app nueva cambia `client_id`, scope y rutas YARP, como hoy.

---

## 10. Fases

Los tamaños son relativos (S < M < L), sin fechas.

### Fase 0: decisiones y spike (S)

- [x] Redactar ADR-A1 a ADR-A4 (sección 11): [Docs/Decisiones](../Decisiones/).
- [x] **Spike OpenIddict 7.x** (hecho directamente en el código de la fase 1):
  - registrar un segundo scope con su resource (`facturas.api` → `facturas-api`);
  - emitir `roles` y `permissions` solo cuando la audiencia es esa;
  - habilitar `client_credentials` solo para clientes confidenciales con el scope `herensuge.access`.
- [ ] Comprobar qué soporta OpenIddict 7 de serie de RFC 8707 (`resource`) y de RFC 8693 (token exchange). No bloquea la fase 1.
- [ ] App de ejemplo mínima, `Samples/Facturas` (API + BFF clonada + SPA con `@herensuge/react`), que valida el JWT por JWKS.

### Fase 1: RBAC por aplicación (L), el MVP

**Hecho (16/11/2026):** backend del módulo, permisos en el token, PDP y gestión por API. Ver "Estado" al final de la fase.

- [x] Módulo `Access` completo, con el esquema `access` y su migración `Initial`.
- [x] `ResourceServer`, manifiesto y sincronización (`PUT /api/v1/platform/resource-servers/{key}/manifest` y
  `PUT /access/v1/manifest` para la CI de la app); alta automática del scope y la audiencia en OpenIddict.
- [x] Roles de organización, plantillas con `grantTo` y asignaciones con caducidad.
- [x] `roles` y `permissions` en el access token de cada API (`Oidc` → `IAccessModuleApi`), con `embedPermissions` por app.
- [x] PDP: `evaluation`, `evaluations` y *discovery*.
- [ ] PDP: caché versionada y canal `herensuge:access-changed` (hoy lee la base de datos en cada decisión: ya es inmediata).
- [ ] Portal:
  - Organización → **Roles** (CRUD, permisos agrupados por app);
  - Miembros → columna y edición de roles;
  - Plataforma → **Aplicaciones** (resource servers, versión del manifiesto, permisos retirados).
- [ ] SDK .NET (`RequirePermission`, `CheckAsync`) y `useCan` / `<Can>` en `@herensuge/react`.
- [ ] Dogfooding: Herensuge como resource server `herensuge`; `TenantAccess` decide por permisos.
- [x] Eventos y auditoría:
  - de dominio: `RoleCreated`, `RoleUpdated`, `RoleDeleted`, `RoleAssigned`, `RoleUnassigned`, `ResourceServerSynced`;
  - limpieza al recibir `MemberRemoved` y `UserDeleted`.
- [x] Métrica `herensuge.access.decisions{resource_server,decision,reason}`.
- [ ] Métrica `herensuge.access.decision.duration` (la traza de ASP.NET ya mide el endpoint).

**Hecho cuando:**
- [ ] `Samples/Facturas` se integra solo con su manifiesto, su cliente y el SDK.
- [ ] Quitar un rol corta el acceso en la PDP en < 1 s y en el token en ≤ 5 min. La SPA recibe `permissions-changed`.
- [ ] Un miembro expulsado pierde todas sus asignaciones.
- [ ] Los roles base actuales funcionan igual que antes (los tests de integración actuales siguen en verde).
- [ ] Tests:
  - unitarios de invariantes de `Role` y `ResourceServer`;
  - integración (sincronización del manifiesto, decisión con y sin claims, aislamiento entre organizaciones: otra organización → `404`);
  - arquitectura (`Access` solo ve los `Contracts`).

**Estado de la fase 1 (16/11/2026):**
- Unitarios del dominio (`Herensuge.Modules.Access.UnitTests`, 8) y de arquitectura (29, con `Access`) en verde.
- Test de integración de punta a punta en `SignInFlowTests`: manifiesto → scope → token con `permissions` →
  `client_credentials` → decisión con *step-up*. **Compila, sin ejecutar** (necesita Docker).
- Simplificaciones respecto a la sección 6, marcadas en el código con `shortcut:`:
  - las asignaciones son solo a usuarios (grupos y clientes en la fase 2);
  - administran los roles los owner y admin (rol base), no aún el permiso `herensuge:roles:manage` (dogfooding pendiente);
  - el catálogo muestra todas las apps a todas las organizaciones (planes en la fase 2);
  - sin caché en la PDP.
- Pendiente de la fase 1: portal (Roles, Miembros, Aplicaciones), SDK .NET, `useCan`, dogfooding, `Samples/Facturas`,
  `permissions-changed` y regenerar `paquetes/api/src/schema.d.ts` (`pnpm -C Src/Web api:types`).

### Fase 2: planes, grupos, cuentas de servicio y condiciones (M)

- [ ] Features, planes, suscripciones y *overrides*; paso 3 del algoritmo.
  - Los límites (cuotas) viajan como `entitlements` en el token (RFC 9068). Herensuge no cuenta el uso: lo aplica cada app.
- [ ] Grupos por organización, asignables a roles.
- [ ] **Cuentas de servicio:** clientes `client_credentials` por organización, con roles (máquina a máquina).
- [ ] Condiciones `requires: mfa | recent_auth` y lista de IPs permitidas por organización.
- [ ] **Simulador** en el portal ("¿puede Ana aprobar facturas?") con la traza de los 6 pasos. Es la mejor ayuda para los admins (lo tiene Keycloak; Auth0 y Clerk no lo tienen así).
- [ ] Plataforma → **Planes**; Organización → **Plan contratado** (solo lectura).

### Fase 3: autorización por objeto (ReBAC) con OpenFGA (L)

Solo si hay casos reales: compartir un documento concreto, jerarquías de departamentos y proyectos, "¿qué proyectos puedo ver?".

- [ ] OpenFGA en `compose.yaml` (con PostgreSQL) y el SDK oficial `OpenFga.Sdk`.
- [ ] Herensuge sincroniza por outbox (idempotente) las tuplas de organización, membresía, grupo y rol.
- [ ] Las apps escriben sus tuplas de objeto (`invoice:acme/123#owner@user:ana`) **a través de Herensuge** (`/access/v1/relationships`), no directamente. Así se conservan el aislamiento por organización y la auditoría (ADR-A8).
- [ ] Permisos marcados como *relacionales* en el manifiesto → la PDP hace `Check` en OpenFGA; `search/resource` → `ListObjects`.
- [ ] Consistencia tras escribir: la preferencia de consistencia de OpenFGA, o un *token* de consistencia propio.

### Fase 4: delegación (M)

- [ ] **Impersonación de soporte**: RFC 8693 (token exchange) con claim `act`. Exige motivo y duración máxima, avisa al usuario por email y queda auditada.
- [ ] **Acceso temporal con aprobación (JIT)**: asignación con `ExpiresAt` más un flujo de aprobación de otro admin.
- [ ] Token exchange servicio a servicio en nombre del usuario, con *downscoping*.
- [ ] Solo si hay API pública para terceros: pantalla de consentimiento y RAR (RFC 9396).

---

## 11. Decisiones a registrar (ADRs)

| ADR | Decisión propuesta | Alternativa descartada |
|---|---|---|
| A1 | `Access` es un **módulo** de `Herensuge.Api` | Servicio aparte: más latencia, otra base de datos y otro despliegue sin necesidad hoy |
| A2 | La API de decisión sigue **OpenID AuthZEN** | API propia: obligaría a escribir todos los PEP a medida |
| A3 | **Permisos en el token por audiencia** + PDP en vivo para lo sensible y por objeto | Todo en el JWT (tokens enormes y caducados) o todo por red (latencia y dependencia) |
| A4 | **Un token, una API**: cada BFF pide solo el scope de su API | Tokens con varias audiencias y permisos mezclados |
| A5 | **Catálogo como código** (manifiesto sincronizado por la CI) | Alta manual de permisos en el portal: se desincroniza del código |
| A6 | **Deny por defecto, sin deny explícito** | Reglas de deny con prioridades: difíciles de explicar y de auditar |
| A7 | Roles base en Tenancy; plantillas de app **concedidas por rol base** | Mover todo a Access ya: migración grande sin beneficio inmediato |
| A8 | ReBAC con **OpenFGA**, y las apps escriben tuplas a través de Herensuge | Motor propio, o SpiceDB (válido: comparar en el spike de la fase 3) |
| A9 | **Condiciones fijas** antes que un lenguaje de políticas | Cedar, Rego o CEL desde el principio |
| A10 | Nombre del módulo: **`Access`** (esquema `access`, scope `herensuge.access`) | `Authorization`: se confunde con `Microsoft.AspNetCore.Authorization` |

---

## 12. Riesgos

| Riesgo | Impacto | Mitigación |
|---|---|---|
| Tokens grandes | Cabeceras > 8 KB, proxies que cortan | `embedPermissions` por app; por encima de ~100 permisos se omiten y el SDK pregunta a la PDP |
| Permisos caducados en el token (≤ 5 min) | Acceso después de quitar un rol | Los permisos sensibles se consultan siempre en vivo; `permissions-changed` refresca la UI |
| PDP no disponible | Las apps no pueden decidir lo sensible | Fail-closed; lo grueso sigue funcionando con el token; la PDP vive en el mismo proceso que el IdP |
| Fuga de datos entre organizaciones | Grave | `tenant_id` en cada tabla y consulta, y un test de aislamiento por endpoint |
| Explosión de roles | Admins perdidos | Plantillas, límites por organización y simulador |
| Cambios en el manifiesto rompen roles | Usuarios sin acceso | Retirar en vez de borrar, avisos en el portal y la versión del manifiesto auditada |
| Dos fuentes de verdad (rol base y roles de Access) | Confusión | Rol base = membresía y gestión de la organización; todo lo demás, en Access. Documentado en ADR-A7 |
| Coste operativo de OpenFGA | Otro servicio que operar | Solo en la fase 3 y solo con casos reales |

---

## 13. Qué estudiar, por orden

| # | Referente | Licencia | Qué mirar | Fase |
|---|---|---|---|---|
| 1 | **Auth0 RBAC** (APIs, permissions, roles) | Comercial (estudiar) | Su modelo "API → permisos → roles" y la opción de meter los permisos en el access token: es casi la fase 1 | 1 |
| 2 | **Clerk Organizations** | Comercial (estudiar) | Roles y permisos personalizados por organización; cómo une features (billing) y permisos | 1–2 |
| 3 | **OpenID AuthZEN** | Abierto (OpenID Foundation) | `evaluation`, `evaluations`, *search* y *discovery*: el contrato de la PDP | 1 |
| 4 | **RFC 9068, 8707, 8693, 7662** | Estándares IETF | Claims `roles`/`entitlements`; audiencia por recurso; token exchange y `act`; introspección | 1 · 4 |
| 5 | **Keycloak Authorization Services** | Apache-2.0 | Resource server, scopes, policies, permissions y la herramienta de evaluación (el simulador). Ver también qué complejidad **no** copiar | 1–2 |
| 6 | **Paper de Zanzibar + OpenFGA** | Apache-2.0 | Modelado, *conditions*, `ListObjects`, consistencia y multi-tenancy | 3 |
| 7 | **SpiceDB** (Authzed) | Apache-2.0 | Alternativa a OpenFGA: *caveats* y *zedtokens* | 3 |
| 8 | **Topaz** (Aserto) | Apache-2.0 | Implementación de referencia de AuthZEN con un directorio tipo Zanzibar | 1 · 3 |
| 9 | **Cerbos / Cedar** | Apache-2.0 | PDP por políticas; solo si las condiciones fijas se quedan cortas | 2+ |
| 10 | **WorkOS FGA, Permit.io** | Comercial (estudiar) | UX de producto para admins: editores de roles, auditoría y simulación | 1–2 |

Cuando se elija dependencia, la regla FOSS del RESUMEN (sección 15.4) aplica igual: lo comercial solo se estudia.

---

## 14. Primer paso

1. Escribir ADR-A1 a ADR-A4 en `Docs/Decisiones/`.
2. Hacer el spike de la fase 0 contra `Samples/Facturas`: un token con `aud=facturas-api` y `permissions`, validado por la API de ejemplo con el JWKS.
3. Escribir el manifiesto de `Facturas` y, con él, el primer test de integración de la sincronización.
