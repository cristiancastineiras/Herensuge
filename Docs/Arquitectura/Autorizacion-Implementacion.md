# Herensuge Access: plan para completar las fases 1 a 4

> **Estado:** borrador para revisión EGM · **Fecha:** 16/11/2026
> **Diseño general:** [Autorizacion.md](Autorizacion.md) (qué y por qué). Este documento dice **cómo** se termina, pieza a pieza.
> **Decisiones ya tomadas:** [ADR-A1](../Decisiones/ADR-A1-access-como-modulo.md), [ADR-A2](../Decisiones/ADR-A2-api-de-decision-authzen.md),
> [ADR-A3](../Decisiones/ADR-A3-permisos-en-el-token.md), [ADR-A4](../Decisiones/ADR-A4-un-token-una-api.md).

---

## 1. Punto de partida

Ya existe el backend de la fase 1 (módulo [Access](../../Src/Modules/Access/)):

- **Catálogo por app.** `ResourceServer` con `Permission` y `RoleTemplate`, sincronizado desde un manifiesto. Al sincronizar,
  Oidc crea el scope `<app>.api` con la audiencia `<app>-api`.
- **Roles y asignaciones.** `Role` (rol propio de una organización) y `RoleAssignment` (usuario ↔ rol o plantilla, con
  caducidad opcional).
- **Permisos en el token.** `roles` y `permissions` en el access token de cada app (`IAccessModuleApi.GetTokenClaimsAsync`).
- **API de decisión.** `POST /access/v1/evaluation(s)`, para los backends de las apps con `client_credentials` y el scope
  `herensuge.access`.
- **API de gestión.** Catálogo, roles y roles de cada miembro; manifiesto desde Plataforma y desde la CI de cada app.

Falta: toda la parte web, los SDK, el *dogfooding*, el aviso en tiempo real y las fases 2, 3 y 4.

## 2. Qué entrega este plan

| Fase | Entrega | Usuario que lo nota |
|---|---|---|
| 1 | Herensuge usa sus propios permisos; portal con Roles, roles por miembro y Aplicaciones; `useCan`; aviso `permissions-changed`; SDK .NET; app de ejemplo | Admin de organización, desarrollador de apps |
| 2 | Planes y features; grupos; cuentas de servicio; condición de red de confianza; simulador | Plataforma, admin de organización |
| 3 | Permisos por objeto (compartir *esta* factura) con OpenFGA, opcional | Desarrollador de apps |
| 4 | Impersonación de soporte auditada; acceso temporal con aprobación | Soporte, miembros |

**Fuera de este plan:**
- Consentimiento de terceros y RAR (RFC 9396): no hay API pública para terceros (condición del diseño general, fase 4).
- *Token exchange* entre servicios (RFC 8693): OpenIddict 7.8 lo soporta (`AllowTokenExchangeFlow`), pero no se puede
  comprobar en esta máquina cómo expone el `actor_token` en modo *passthrough*. Se hará cuando se pueda probar.
- Caché de la API de decisión: hoy lee la base de datos en cada decisión (unas pocas consultas con índice). Solo se añade
  si las trazas lo piden.
- Lenguaje de políticas general (Cedar, Rego) y un sujeto distinto por elemento en AuthZEN.

## 3. Decisiones de este plan

Completan las del diseño general. Cada una dice qué se descarta.

### D1. Herensuge es una app más (dogfooding)

- Al arrancar, un `AccessSeeder` sincroniza el manifiesto de Herensuge: clave `herensuge`, cliente `herensuge-portal`.
  El scope `herensuge.api` y la audiencia `herensuge-api` ya existen.
- **Permisos:** `tenant:update`, `members:read`, `members:manage`, `invitations:manage`, `roles:manage`, `audit:read`,
  `service-accounts:manage`, `access-requests:approve`.
- **Plantillas:**
  - `organization-admin`, con todos los permisos y `grantTo: [owner, admin]`;
  - `organization-member`, sin permisos de gestión y `grantTo: [member]`.
- Tenancy pregunta por permisos con `IAccessModuleApi.HasPermissionAsync(tenantId, userId, "herensuge", permission)`.
  Las reglas de dominio de Tenancy no cambian: solo un owner nombra a otro owner y siempre queda un owner.
- El filtro `RequireTenantAdministrator` pasa a `RequirePermission("members:read")` sobre el claim `permissions` del
  token del portal.
- **Descartado:** borrar los roles base de Tenancy. Son la membresía y dan el comportamiento por defecto vía `grantTo`.

### D2. Los permisos llegan a la SPA a través de la BFF

- La BFF ya guarda el access token. `/bff/user` lee de su contenido `roles`, `permissions`, `entitlements` y `act` (solo
  lectura: el token llegó por TLS desde el endpoint de token y la firma la comprueba la API).
- `/bff/user?refresh=true` fuerza un *refresh* antes de responder, para leer permisos nuevos.
- **Descartado:** que la SPA llame a Herensuge para conocer sus permisos. La audiencia del token es la API de la app, no
  `herensuge-api`.

### D3. Aviso en tiempo real de cambios de permisos

- Access publica en Valkey `herensuge:access-changed` con `{ tenantId, userId? }`. Sin `userId` afecta a toda la
  organización (un rol cambiado, una app que sincroniza su manifiesto, un plan nuevo).
- Lo publica un handler de la *outbox* de Access, con el mismo patrón que `herensuge:session-revoked`.
- Cada BFF reenvía `permissions-changed` por `/bff/events` a las pestañas afectadas. `@herensuge/react` vuelve a pedir
  `/bff/user?refresh=true`.
- Si el aviso se pierde, el token se pone al día en su siguiente *refresh* (≤ 5 min) y la API de decisión ya es inmediata.

### D4. Planes y features

- **Features.** El manifiesto declara `features` y cada permiso puede pedir una (`feature`).
- **Planes.** Plataforma crea planes con features y límites (JSON libre por feature, que interpreta la app). Un plan se
  marca **por defecto**: lo tienen las organizaciones sin suscripción.
- **Suscripción.** Una por organización y como mucho una activa: plan, `StartsAt` y `EndsAt` opcional.
- **Decisión.** Un permiso con feature no contratada se deniega con `not_entitled`, también en el token (no viaja en
  `permissions`). Un permiso sin feature no depende del plan.
- **Token.** Lleva `entitlements` (claves de features de esa app). Los límites se leen con
  `GET /access/v1/tenants/{tenantId}/entitlements` (backend de la app).
- **Descartado:** contar el uso dentro de Herensuge. Cada app aplica sus límites.

### D5. Grupos

- Grupos por organización (`Group` con miembros). Una asignación tiene un sujeto `user`, `group` o `client`.
- El resolutor suma las asignaciones de los grupos del usuario. Expulsar a un miembro lo saca también de sus grupos.

### D6. Cuentas de servicio

- **Cliente.** Cuenta de máquina de una organización (`ServiceAccount`), respaldada por un cliente OIDC confidencial
  con solo `client_credentials`. Lo crea Oidc a través de un `IOidcModuleApi` nuevo (proyecto `Oidc.Contracts`), que
  devuelve el secreto una sola vez.
- **Token.** Para una app (`scope=<app>.api`) lleva `sub = client_id`, `tenant_id` y los `permissions` de los roles
  asignados al sujeto `client`. Sin sesión: la API de decisión acepta `subject.type = "client"`.
- **Descartado:** claves API propias de Access. OAuth ya resuelve emisión, rotación y revocación.

### D7. Red de confianza

- Nuevo valor `requires: trusted_network` en el manifiesto.
- **Lista de redes.** Cada organización guarda las suyas (`TrustedNetworks`, CIDR, IPv4 e IPv6). Sin lista, la
  condición se cumple.
- **Comprobación.** Solo en la API de decisión, con `context.ip` (la IP real del navegador, que pasa el backend de la
  app). Los permisos con esta condición nunca viajan en el token.
- **Descartado:** comprobarlo al emitir tokens. En el *refresh* la IP es la del servidor de la BFF, no la del usuario.

### D8. Simulador

- `POST /api/v1/tenants/{tenantId}/access/simulate { userId, resourceServer, permission, context? }` devuelve la
  decisión y la traza de cada paso del algoritmo.
- Usa la misma función de decisión que la API de decisión, con un registro de pasos.

### D9. Permisos por objeto con OpenFGA (opcional)

- Solo se activa si `Access:OpenFga:ApiUrl` tiene valor. Sin él, los permisos relacionales se deniegan con
  `relationships_disabled`.
- **Modelo.** Uno fijo, escrito por Herensuge:
  - `user`;
  - `group#member`;
  - `object` con `owner`, `editor`, `viewer` y `parent`; `owner ⊂ editor ⊂ viewer`, y se heredan del `parent`.
- **Objetos.** Los ids los compone Herensuge: `object:<app>/<tenantId>/<type>/<id>`. Una app no puede escribir objetos
  de otra app ni de otra organización.
- **Permisos.** Un permiso del manifiesto puede pedir `relation: viewer | editor | owner`. La decisión exige el rol
  **y** la relación con `resource.id`.
- **API.**
  - `PUT /access/v1/relationships` y `DELETE /access/v1/relationships` para la app;
  - `POST /access/v1/search/resource` (AuthZEN) con `ListObjects`.
- **Grupos.** La membresía de grupos se sincroniza a OpenFGA por la *outbox*.
- **Descartado:** un modelo por app generado desde el manifiesto (exige traducir DSL) y un motor propio (ADR-A8).

### D10. Impersonación de soporte

- **Inicio.** Un admin de plataforma (con passkey) abre una **sesión de soporte** con un usuario:
  - `POST /api/v1/platform/support-sessions { email, reason }`;
  - dura como mucho 30 minutos;
  - devuelve un enlace de un solo uso.
- **Entrada.** El enlace (`/api/v1/auth/support/{ticket}`, en el IdP) crea una `UserSession` del usuario con
  `ImpersonatorUserId`, `amr = ["imp"]` y `auth_time` = hora de inicio. La cookie del IdP de ese navegador pasa a ser la
  del usuario; se recomienda una ventana privada.
- **Tokens.** Llevan `act: { sub: <admin> }` (RFC 8693, sin usar el flujo de *token exchange*).
- **Límites.** Sin `mfa` en `amr`, la decisión niega lo que requiere MFA. Las operaciones de credenciales (contraseña,
  MFA, passkeys, borrar cuenta) se rechazan siempre en una sesión de soporte.
- **Rastro.** Se audita al iniciar y al terminar, el usuario recibe un email y el portal muestra una franja «Sesión de
  soporte».

### D11. Acceso temporal con aprobación

- Un miembro pide un rol (de la organización o plantilla) durante 1 a 72 horas, con motivo (`AccessRequest`).
- Un usuario con `access-requests:approve` lo aprueba o rechaza. Aprobar crea la asignación con `ExpiresAt`.
- Pendiente más de 24 horas → caduca. No se puede aprobar la propia petición.

## 4. Cambios en el modelo de Access

| Agregado | Cambio |
|---|---|
| `ResourceServer` | `Features[]` (clave, nombre); `Permission.FeatureKey?`, `Permission.Relation?`; `requires` admite `trusted_network` |
| `RoleAssignment` | `SubjectType` (`user` · `group` · `client`) y `SubjectId` en lugar de `UserId` |
| `Group` (nuevo) | `TenantId`, `Name`, `Members[]` (userId) |
| `ServiceAccount` (nuevo) | `TenantId`, `Name`, `ClientId`, `CreatedBy`, `DisabledAt?` |
| `Plan` (nuevo) | `Key`, `Name`, `IsDefault`, `Features[]` (`ResourceServerKey`, `FeatureKey`, `Limits` JSON) |
| `TenantSubscription` (nuevo) | `TenantId`, `PlanKey`, `StartsAtUtc`, `EndsAtUtc?` |
| `TenantSettings` (nuevo) | `TenantId`, `TrustedNetworks[]` |
| `AccessRequest` (nuevo) | `TenantId`, `UserId`, `RoleId?`, `RoleTemplateId?`, `Hours`, `Reason`, `Status`, `DecidedBy?`, `DecidedAtUtc?` |

**Cambio en Identity:** `UserSession.ImpersonatorUserId?` y `SupportTicket` (hash, admin, usuario, motivo, caducidad
de 5 minutos, un solo uso).

## 5. Algoritmo de decisión final

```
decide(sujeto, organización, app, permiso, recurso?, contexto):
  1. permiso existe y activo en la app                         → deny(unknown_permission)
  2. sujeto usuario: sesión activa                             → deny(session_inactive)
     sujeto cliente: cuenta de servicio activa de la organización → deny(not_member)
  3. usuario: miembro de organización activa                   → deny(not_member)
  4. feature del permiso contratada (plan activo o por defecto) → deny(not_entitled)
  5. roles efectivos (directos, de grupos, plantillas por rol base) contienen el permiso → deny(no_role)
  6. requires: mfa → amr ∋ mfa; recent_auth → auth_time < 10 min;
               trusted_network → ip ∈ redes de la organización (o sin lista) → deny(step_up_required | untrusted_network)
  7. relation (si la hay): OpenFGA check(user, relation, objeto)  → deny(no_relation | relationships_disabled)
  8. permit(granted)
```

## 6. Pantallas

| Dónde | Pantalla | Qué hace |
|---|---|---|
| Organización | **Roles** | Lista de roles propios y plantillas de las apps. Crear/editar con permisos agrupados por app (casillas). Borrar con confirmación |
| Organización | **Miembros** | Columna "Roles". Editor por miembro: plantillas y roles con caducidad opcional |
| Organización | **Grupos** | Crear, renombrar, borrar; miembros; roles del grupo |
| Organización | **Cuentas de servicio** | Crear (secreto mostrado una vez), rotar secreto, desactivar; roles |
| Organización | **Accesos temporales** | Peticiones pendientes: aprobar o rechazar; historial |
| Organización | **Simulador** | Miembro + app + permiso → decisión y traza |
| Organización | **General** | Plan contratado (solo lectura) y redes de confianza |
| Plataforma | **Aplicaciones** | Lista; sincronizar manifiesto pegando el JSON; permisos retirados |
| Plataforma | **Planes** | Crear y editar planes, features y límites; plan por defecto |
| Plataforma | **Organizaciones** | Asignar plan |
| Plataforma | **Soporte** | Abrir sesión de soporte por email con motivo; lista de sesiones abiertas |
| Mi cuenta | **Mis accesos** | Permisos efectivos por app; pedir acceso temporal; mis peticiones |
| Todas | Franja | "Sesión de soporte de <admin>" cuando el token lleva `act` |

Las secciones se muestran según `useCan(...)` sobre los permisos `herensuge`; la API vuelve a comprobarlo siempre.

## 7. SDKs

- **`@herensuge/react`.** `HerensugeUser` gana `roles`, `permissions`, `entitlements` y `actor`. Añade
  `useCan(permission)`, `useEntitled(feature)` y `<Can permission>`. Al recibir `permissions-changed` pide
  `/bff/user?refresh=true`.
- **`Herensuge.Access.AspNetCore`** (NuGet, `Src/Sdk/`):
  - `AddHerensugeAccess(options)`: valida los tokens de la app con OpenIddict.Validation contra el emisor (JWKS) y la
    audiencia `<app>-api`, y registra `IHerensugeAccess`;
  - `.RequirePermission("invoice:read")`: decide en local con el claim y, si el permiso tiene condición o relación según
    el manifiesto local, pregunta a la API de decisión;
  - `IHerensugeAccess.CheckAsync(user, permission, resourceId?, context?)` y `WriteRelationshipAsync(...)`;
  - token `client_credentials` propio, cacheado hasta un minuto antes de caducar.
- **Ejemplo:** `Samples/Facturas`, con una API mínima, su `herensuge.access.json` y el `appsettings` para ejecutar
  `Herensuge.Bff` como su BFF.

## 8. Riesgos de este plan

| Riesgo | Mitigación |
|---|---|
| Nada se ejecuta en esta máquina | Compilación con warnings como errores, tests unitarios en memoria, tests de integración compilados y listos para Docker |
| Cambiar `UserId` por `SubjectId` en `RoleAssignment` | Migración nueva que renombra la columna y añade `subject_type = 'user'` |
| El portal pierde acceso si el `AccessSeeder` falla | El seeder corre antes de servir peticiones (como `OidcSeeder`); el test de integración del portal lo cubre |
| OpenFGA con una API del SDK no probada aquí | Opcional y desactivado por defecto; tests unitarios de la traducción de ids; documentado como no verificado |
| La impersonación cambia la cookie del admin en su navegador | Aviso en la pantalla de Soporte: usar una ventana privada |

## Detailed Implementation Plan

Cada paquete de trabajo compila por sí mismo y termina con su test. El orden respeta las dependencias.

### WP1. Dogfooding y aviso en tiempo real (fase 1)

1. `Access.Contracts`: añadir `HasPermissionAsync(Guid tenantId, Guid userId, string resourceServer, string permission)`.
2. `Access.Infrastructure/Seeding/AccessSeeder.cs`: `IHostedService` que ejecuta `SyncManifestCommand` con el manifiesto
   embebido `herensuge.access.json` (recurso del ensamblado) y `CallerClientId = null`. Registrado después de
   `StartupMigrator`.
3. Tenancy: `TenantAccess.RequireAdministratorAsync` → `RequirePermissionAsync(tenantId, actor, permission)`, que comprueba
   la organización activa, la membresía (para las reglas de dominio) y el permiso vía `IAccessModuleApi`.
   - Handlers: `ChangeMemberRole` y `RemoveMember` (`members:manage`); `CreateInvitation` y `RevokeInvitation`
     (`invitations:manage`); `UpdateTenant` (`tenant:update`).
   - `Tenancy.Application.csproj` referencia `Access.Contracts`.
4. `Common.Presentation/Filters`: `RequirePermission(string)` lee el claim `permissions`. Sustituye
   `RequireTenantAdministrator` en `members`, `invitations`, `audit`, `roles`, `permissions` y `members/{id}/roles`.
5. Aviso:
   - Access, `Application/Abstractions/IAccessChangeNotifier.cs` y `EventHandlers/NotifyAccessChangedHandler.cs`:
     escucha sus propios integration events y `MemberRoleChanged` y `MemberRemoved` de Tenancy;
   - Access, `Infrastructure/ValkeyAccessChangeNotifier.cs`: canal `herensuge:access-changed`;
   - BFF, `Sessions/AccessChangedListener.cs`: emite en `SessionEndedSignal` un evento nuevo `AccessChanged(tenantId,
     userId?)`;
   - BFF, `/bff/events`: emite `permissions-changed` si coincide la organización (y el usuario, si viene).
6. BFF: `/bff/user` añade `roles`, `permissions`, `entitlements` y `actor` leyendo el access token
   (`System.IdentityModel.Tokens.Jwt` no está; se decodifica el payload base64url con `System.Text.Json`).
   `refresh=true` llama a `AccessTokenProvider.GetAccessTokenAsync(httpContext, force: true)`.
7. Tests:
   - integración: el portal sigue gestionando miembros (los existentes);
   - nuevo: un owner tiene `members:manage` en el token del portal; un member no.

### WP2. Portal de la fase 1

1. `paquetes/api/src/schema.d.ts` regenerado a mano con el formato de openapi-typescript (no hay `node_modules`), y
   `types.ts` con alias.
2. `apps/portal/src/api.ts`: `access.*` (catálogo, roles, roles de miembros) y `platform.resourceServers` y
   `platform.syncManifest`.
3. Páginas nuevas, con `lazy` en `router.tsx` y navegación en `AppLayout.tsx` según `useCan`:
   - `organization/RolesPage.tsx` y `organization/MemberRolesDialog.tsx` (abierto desde `MembersPage`);
   - `platform/ApplicationsPage.tsx`.
4. `@herensuge/react`: tipos, `useCan`, `useEntitled`, `<Can>`, `permissions-changed` y franja de soporte (`actor`).
5. Errores nuevos en `paquetes/api/src/errors.ts` (`Access.*`).

### WP3. SDK .NET y ejemplo (fase 1)

1. `Src/Sdk/Herensuge.Access.AspNetCore/`:
   - `HerensugeAccessOptions` (Authority, ResourceServer, ClientId, ClientSecret, ManifestPath);
   - `ServiceCollectionExtensions.AddHerensugeAccess`;
   - `PermissionPolicyProvider` y `PermissionHandler` (prefijo `perm:`);
   - `HerensugeAccessClient` (token de máquina con caché, `CheckAsync`, `WriteRelationshipAsync`);
   - `EndpointExtensions.RequirePermission`.
2. `Samples/Facturas/Facturas.Api/` (API mínima con dos endpoints), `herensuge.access.json`, `bff.appsettings.json` y
   `README.md`.
3. Las dos al `.slnx`; tests unitarios del SDK (`Tests/Herensuge.Access.AspNetCore.UnitTests`): política local vs PDP
   según el manifiesto.

### WP4. Planes, grupos, cuentas de servicio, red de confianza y simulador (fase 2)

1. Dominio y migración `Phase2` en Access: `Feature`, `Permission.FeatureKey`, `Plan`, `TenantSubscription`, `Group`,
   `ServiceAccount`, `TenantSettings`, y `RoleAssignment.SubjectType/SubjectId`.
2. `Oidc.Contracts` (nuevo proyecto): `IOidcModuleApi.CreateServiceClientAsync`, `RotateServiceClientSecretAsync`,
   `DisableServiceClientAsync`. Implementado en `Oidc.Infrastructure` con el `IOpenIddictApplicationManager`.
   - Propiedad `tenant_id` en la aplicación de OpenIddict;
   - `ConnectEndpoints.IssueMachineTokenAsync` permite scopes de app a estos clientes y añade `tenant_id` y los
     permisos del sujeto `client`.
3. `AccessResolver` con grupos, cuentas de servicio y features; `Decide` con `trusted_network` y una traza opcional
   (`List<DecisionStep>`).
4. API:
   - Plataforma: planes y suscripciones;
   - Organización: grupos, cuentas de servicio, ajustes (redes de confianza) y simulador;
   - Máquina: `GET /access/v1/tenants/{tenantId}/entitlements`.
5. Portal: `organization/GroupsPage.tsx`, `organization/ServiceAccountsPage.tsx`, `organization/SimulatorPage.tsx`,
   ajustes en `OrganizationPage.tsx`, `platform/PlansPage.tsx` y la columna de plan en `PlatformTenantsPage.tsx`.
6. Tests unitarios (planes por defecto, CIDR, grupos, traza) e integración (permiso no contratado; token de cuenta de
   servicio).

### WP5. OpenFGA (fase 3)

1. `Directory.Packages.props`: `OpenFga.Sdk` 0.10.5 (Apache-2.0), solo en `Access.Infrastructure`.
2. `Access.Application/Abstractions/IRelationshipStore.cs` (`CheckAsync`, `WriteAsync`, `DeleteAsync`,
   `ListObjectsAsync`, `IsEnabled`).
3. `Access.Infrastructure/Relationships/`:
   - `OpenFgaRelationshipStore` y `NullRelationshipStore`;
   - `OpenFgaBootstrapper` (crea la *store* y el modelo y guarda sus ids en `access.relationship_store`);
   - `RelationshipIds` (traducción de ids, con tests).
4. Manifiesto: `Permission.Relation`; paso 7 de la decisión.
5. API: `PUT/DELETE /access/v1/relationships` y `POST /access/v1/search/resource`. Sincronizar grupos por la *outbox*.
6. `Docker/compose.yaml`: servicio `herensuge-openfga` en el *profile* `relationships`, con su base de datos en
   PostgreSQL.

### WP6. Impersonación y acceso temporal (fase 4)

1. Identity:
   - `UserSession.StartSupport(...)` con `ImpersonatorUserId`;
   - agregado `SupportTicket`;
   - comandos `StartSupportSession` y `RedeemSupportTicket`;
   - endpoint `GET /api/v1/auth/support/{ticket}`;
   - integration events `SupportSessionStarted` y `SupportSessionEnded` (auditoría y email);
   - los endpoints de credenciales comprueban que la sesión no sea de soporte.
2. Oidc: `AuthorizeQuery` lleva el impersonador y la decisión añade `act`. `IIdentityModuleApi` gana
   `GetSupportSessionAsync(sessionId)`.
3. Access: `AccessRequest` (agregado, comandos `RequestAccess`, `ApproveAccessRequest`, `DenyAccessRequest`) y job de
   caducidad (`AccessRequestExpiryJob`, cada 10 minutos).
4. Portal:
   - `platform/SupportPage.tsx`, `organization/AccessRequestsPage.tsx` y `account/MyAccessPage.tsx`;
   - franja en `AppLayout.tsx`.
5. Tests: unitarios de `AccessRequest` y `SupportTicket`; integración con el claim `act` en el token de una sesión de
   soporte.

### WP7. Cierre

1. Regenerar el contrato OpenAPI (`dotnet build` de `Herensuge.Api`).
2. Actualizar `Autorizacion.md` (estado), `README.md` y `RESUMEN.md` (módulo Access en la tabla).
3. Revisión de desfase EGM (`egm-drift-report`) sobre `Docs/Arquitectura` y `Docs/Decisiones`.
