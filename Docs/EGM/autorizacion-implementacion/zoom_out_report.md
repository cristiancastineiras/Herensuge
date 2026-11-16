# Etapa 5: Zoom Out inicial

**Objetivo:** `Docs/Arquitectura/Autorizacion-Implementacion.md` (archivo completo, versión 1 de 348 líneas)
**Revisores (goldfish, contexto limpio):**

| Revisor | Estado |
| --- | --- |
| `egm-zoom-out` (Skeptical Architect) | completado: 2 🔴, 7 🟡, 6 🟢 |
| `egm-inventive-catalyst` | completado: 11 🟡, 3 🟢 (su persona solo da 🟡/🟢; marca dos de seguridad como candidatos a 🔴) |

En la columna «Resolución» está la decisión del Elephant; el detalle va en `goldfish_review_report.md`.

## Hallazgos

| Id | Sev. | Hallazgo (resumen) | Resolución |
| --- | --- | --- | --- |
| ZO-1 | 🔴 | Las fases 2–4 se construyen sobre una fase 1 que nunca se ha ejecutado; limitar el plan a la fase 1 | ESCALATE: el usuario pidió el sistema completo. Se añaden puertas de verificación por WP en la CI (ver IC-4) |
| ZO-2 | 🔴 | OpenFGA sin caso real; incoherente con aplazar el *token exchange* | ESCALATE el alcance (petición explícita). La incoherencia se corrige: el *token exchange* entra, porque su semántica *passthrough* se verificó en el código de ejemplo de OpenIddict |
| IC-1 | 🟡→🔴 | Escalada de privilegios: quien gestiona roles o aprueba peticiones puede concederse cualquier permiso | PROPOSE FIX: regla de subconjunto (solo se concede lo que se tiene; el owner queda exento) |
| IC-3 | 🟡→🔴 | La impersonación no entra en organizaciones con MFA obligatoria y acaba en las pantallas de MFA del usuario | PROPOSE FIX: una sesión de soporte cumple `RequireMfa` con la passkey del admin; las operaciones de credenciales se bloquean con `act` |
| ZO-3 / IC-12 | 🟡 | La BFF decodifica a mano el access token (RFC 9068 §6) | PROPOSE FIX: los claims de acceso viajan también en el ID token y la BFF los lee del principal validado |
| ZO-4 / IC-5 | 🟡 | El aviso en tiempo real fuerza refrescos en cascada y su formato no cubre los cambios globales | PROPOSE FIX: el evento lleva `changedAt`; la BFF marca «obsoleto desde T» y refresca una vez por sesión con doble comprobación bajo el lock; desaparece `refresh=true` |
| IC-6 | 🟡 | Los avisos perdidos y las caducidades por tiempo no llegan a la UI | PROPOSE FIX: el latido de `/bff/events` compara un hash de los permisos; Valkey solo acelera |
| ZO-5 / IC-2 | 🟡 | El dogfooding deja dos fuentes de autorización y permisos «muertos» frente a las reglas por rol base | PROPOSE FIX (alternativa YAGNI de IC-2, coherente con ADR-A7): miembros, invitaciones y ajustes siguen con el rol base; Access y Audit pasan a permisos; `TenantAdministration` usa `roles:manage`; tabla endpoint → permiso |
| ZO-6 / IC-4 | 🟡 | `schema.d.ts` escrito a mano; los tests solo se compilan | PROPOSE FIX: se genera con la herramienta (ya instalada); la CI es el verificador de cada WP |
| ZO-7 | 🟡 | `Oidc.Contracts` duplica `IClientRegistry` y el estado «desactivado» | PROPOSE FIX: fachada fina sobre `IClientRegistry`; sin `DisabledAt` (se borra); ADR |
| ZO-8 / IC-14 | 🟡 | Impersonación propia sin ADR; SSO a todas las apps; ventana privada como única mitigación | PROPOSE FIX: ADR (RFC 8693 no encaja: el admin no tiene un `subject_token` del usuario); solo para los clientes permitidos; el canje se rechaza si hay otra sesión del IdP |
| ZO-9 | 🟡 | Los planes traen andamiaje de facturación (fechas, límites, overrides) | PROPOSE FIX: un plan es un conjunto de features; una fila por organización; plan por defecto; el resto, descartado |
| IC-7 | 🟡 | `AccessSeeder` crea una versión y un evento en cada arranque | PROPOSE FIX: hash del manifiesto; sin cambios no hace nada; ventana de compatibilidad marcada con `shortcut:` |
| IC-8 / ZO-13 | 🟡 | El simulador depende de la sesión viva del simulado | PROPOSE FIX: separar los hechos del sujeto de la evaluación; supuestos marcados en la traza; exige `roles:manage` |
| IC-9 | 🟡 | «Rol y relación» no sirve para compartir; el modelo no tiene `client`; falta la consistencia | PROPOSE FIX: `relationMode: or` por defecto (`and` opcional); `client` en el modelo; consistencia alta en las comprobaciones |
| IC-10 / ZO-11 | 🟡 / 🟢 | Tres reglas para el claim `permissions`; el SDK necesita un manifiesto local | PROPOSE FIX: `permissions` (sin condición) y `conditional_permissions`; el SDK decide en local o pregunta a la PDP, sin manifiesto; `IAuthorizationRequirementData` |
| IC-11 | 🟡 | La cadena de la IP real no está cerrada | PROPOSE FIX: el SDK toma la IP tras `ForwardedHeaders`, con la BFF como único proxy de confianza; test |
| ZO-10 | 🟢 | `TenantSettings` duplica la política de la organización | PROPOSE FIX: `TrustedNetworks` en `Tenant` y en `MembershipSnapshot` |
| ZO-12 | 🟢 | Job de caducidad de peticiones | PROPOSE FIX: la caducidad se calcula al leer |
| ZO-14 / IC-14 | 🟢 | Ningún WP redacta los ADR pendientes; se cita A8 sin existir | PROPOSE FIX: paso 0 de ADR en cada WP; A5–A13 |
| IC-13 | 🟢 | La cookie del admin se sobrescribe; el fin de la sesión es pasivo | PROPOSE FIX: canje rechazado si hay otra sesión; fin explícito y por caducidad (job de limpieza existente) |
| ZO-15 | 🟢 | El ejemplo no tiene SPA | DECLINE: con los claims en el ID token, el camino BFF → `useCan` es el mismo para todas las apps y lo ejercita el portal |
