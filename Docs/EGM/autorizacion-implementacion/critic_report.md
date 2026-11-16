# Etapa 7: revisión crítica (FeatureRange)

**Objetivo:** `Docs/Arquitectura/Autorizacion-Implementacion.md` v3, líneas 1–439.
**Estándar:** `feature-spec-standard` (para `design_critic` y `structural`).

| Revisor | Estado |
| --- | --- |
| `egm-design` (design_critic) | completado: 2 🔴, 10 🟡, 3 🟢 |
| `egm-structural` | completado: 1 🔴, 6 🟡, 4 🟢 |
| `egm-style` | completado: 0 🔴, 7 🟡, 5 🟢 |

## Hallazgos y resolución (Elephant)

En la columna «Resolución», los números de D son los de la versión 4.

| Id | Sev. | Hallazgo | Resolución |
| --- | --- | --- | --- |
| DC-1 | 🔴 | La sesión de soporte puede dejar acceso permanente (invitaciones, cuentas de servicio, owners) | PROPOSE FIX: la sesión de soporte es **de solo lectura**. Un filtro global rechaza toda petición que cambie algo si el token lleva `act`, salvo las marcadas `AllowDuringSupport` (ninguna en este plan). No se abren sesiones sobre admins de plataforma |
| DC-2 | 🔴 | Lo hecho en soporte se audita a nombre del usuario | PROPOSE FIX: con solo lectura no hay escrituras que atribuir. Inicio, canje y fin quedan auditados con el admin y el usuario. Un test comprueba que una escritura con `act` se rechaza |
| ST-1 | 🔴 | No hay evaluación de alternativas | PROPOSE FIX: §9 «Alternativas consideradas», con una o más por decisión |
| DC-3 | 🟡 | La regla de subconjunto no cubre la entrada en un grupo | PROPOSE FIX: para añadir a alguien a un grupo, el actor debe tener todos los permisos del grupo; test de autoescalada |
| DC-4 | 🟡 | Con permisos, el ID token rompe el logout (`id_token_hint` en la URL) y los expone | PROPOSE FIX: los claims de acceso llegan a la BFF por **userinfo**; el ID token no cambia. ADR-A13 reescrita |
| DC-5 | 🟡 | El latido refresca sin parar: carga y sesiones que no caducan | PROPOSE FIX: **versiones de acceso** en Valkey (`INCR`); el latido solo lee y la BFF refresca solo si cambian |
| DC-6 | 🟡 | El objetivo de latencia de la PDP no se mide | PROPOSE FIX: histograma `herensuge.access.decision.duration`; si el p99 supera 25 ms en producción, se abre el WP de caché; se ajusta `Autorizacion.md` |
| DC-7 | 🟡 | Cuentas de servicio con la organización suspendida; sin atomicidad; revocación tardía | PROPOSE FIX: «organización activa» también para clientes; orden con compensación; emitir un token exige el agregado; ventana de 5 min documentada |
| DC-8 | 🟡 | El *token exchange* no ata el `subject_token` al cliente; actor sin validar | PROPOSE FIX: la audiencia del `subject_token` debe ser la app del cliente que canjea; sin `actor_token`; sin *refresh*; con `act`, las condiciones no se cumplen; tests positivo y negativo |
| DC-9 | 🟡 | OpenFGA: réplica de grupos lenta o perdida, tuplas sin limpiar, caída sin definir | PROPOSE FIX: grupos como *contextual tuples* (sin réplica); limpieza al expulsar o borrar la cuenta; timeout de 2 s con `relationships_unavailable`; se valida el sujeto al escribir |
| DC-10 | 🟡 | El enlace de soporte es un GET con el token en la ruta | PROPOSE FIX: página de la SPA con el token en el fragmento (`#`); el canje es un POST con `X-CSRF` y la passkey del mismo admin |
| DC-11 | 🟡 | La red de confianza no siempre falla cerrada | PROPOSE FIX: la PDP deniega una IP privada o de loopback; `TrustedNetworks` rechaza rangos privados; los saltos de proxy se documentan |
| DC-12 / ST-2 | 🟡 | Riesgos incompletos (seguridad, fallos, escala) | PROPOSE FIX: §10 con una columna de tipo |
| ST-3 | 🟡 | Falta el planteamiento del problema | PROPOSE FIX: §1 «Problema y objetivos», con criterio de éxito por fase y no objetivos |
| ST-4 / ST.-2 | 🟡 | La trazabilidad D → WP y la relación fase ↔ WP | PROPOSE FIX: columna «Fase»; D8 → WP1; WP0 y WP3 incluidos; WP7.2 se separa como «tras desplegar WP1» |
| ST-5 / ST.-1 | 🟡 | Glosario incompleto (PDP, WP, IdP, AuthZEN, `amr`, `act`, `auth_time`) y sinónimos | PROPOSE FIX: glosario ampliado; «PDP» y «evaluador» como términos únicos |
| ST-6 | 🟡 | No distingue lo que existe de lo nuevo; `AccessResolver` sin mencionar | PROPOSE FIX: §2 marca «(existe)» / «(nuevo)»; `AccessResolver` se absorbe en `AccessEvaluator` |
| ST-7 | 🟡 | Sin vista de componentes ni flujos | PROPOSE FIX: §4 con cuatro flujos en mermaid |
| ST.-3 | 🟡 | «Apagado por defecto» dice más de lo que se cumple | PROPOSE FIX: tabla de interruptores, con lo que cambia sin ellos (migraciones y motivos); D13 no tiene interruptor |
| ST.-4 | 🟡 | `owner`, «dueño» y «admin» ambiguos | PROPOSE FIX: títulos y textos precisos |
| ST.-5 | 🟡 | Afirmaciones sin fuente | PROPOSE FIX: se cita el fichero o la doc XML de OpenIddict |
| ST.-6 | 🟡 | Códigos de revisión en los títulos | PROPOSE FIX: línea «Origen:» al final de cada decisión |
| ST.-7 / ST-9 | 🟡 / 🟢 | Rutas sin formato literal en la tabla de D1 | PROPOSE FIX: rutas completas, relativas a `/api/v1` |
| DC-13 | 🟢 | El aviso compara relojes distintos | PROPOSE FIX: desaparece con las versiones monotónicas (DC-5) |
| DC-14 | 🟢 | El manifiesto `herensuge` se puede sobrescribir y los seeders compiten | PROPOSE FIX: clave reservada; reintento ante el índice único |
| DC-15 | 🟢 | Varias imprecisiones (interruptor de D13, plan por defecto, origen de los permisos del actor, motivo para un cliente) | PROPOSE FIX: todas resueltas en el texto |
| ST.-8 | 🟢 | La cuenta del retraso máximo no cuadra | PROPOSE FIX: desaparece con DC-5; nueva cota explicada |
| ST.-9 / ST.-10 / ST.-11 / ST.-12 | 🟢 | Calificativos vagos, «Descartado» sin motivo, formato y ambigüedades | PROPOSE FIX: aplicadas |
| ST-8 / ST-10 / ST-11 | 🟢 | Referencias ambiguas, orden y sufijos, formato | PROPOSE FIX: «este doc §N» o «Autorizacion.md §N»; fase en el título de cada decisión; bloque `text` |
| ST-11 (cabecera) | 🟢 | «## Detailed Implementation Plan» en inglés y sin número | CONTEST: es el marcador que `egm-review` busca para separar la especificación del plan detallado (`skills/egm-review/SKILL.md`, paso 3) |
| ST-11 (git) | 🟢 | `Docs/EGM` y las ADR nuevas no están en git | ESCALATE: los commits los hace el solicitante; el informe final lo recuerda |
