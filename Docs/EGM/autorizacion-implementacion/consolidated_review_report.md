# Informe consolidado de la revisión EGM

Status: resolved (etapas 5 a 7); etapas 8 y 9 sin ejecutar

**Objetivo:** [Autorizacion-Implementacion.md](../../Arquitectura/Autorizacion-Implementacion.md)
**Modo:** «Consolidated report» (sin pausas entre etapas, por petición del solicitante)

## Etapas

| Etapa | Revisores | Estado | Informe |
| --- | --- | --- | --- |
| 5. Zoom Out inicial | `egm-zoom-out`, `egm-inventive-catalyst` | completada | [zoom_out_report.md](zoom_out_report.md) |
| 6. Comprensión | `egm-comprehension` | completada | [comprehension_report.md](comprehension_report.md) |
| 7. Crítica | `egm-design`, `egm-structural`, `egm-style` | completada | [critic_report.md](critic_report.md) |
| 8. Zoom Out intermedio | `egm-zoom-out` | **no ejecutada**: el revisor falló por el límite de uso de la API (HTTP 429); el reintento no era posible hasta que se restableciera | — |
| 9. Preparación para implementar | `egm-implementation-readiness`, `egm-codebase-details`, `egm-style` | **no ejecutada** por el mismo límite | — |

Según el protocolo, un revisor que no completa su informe no cuenta como «revisión sin hallazgos».

**Pendiente.** Repetir las etapas 8 y 9 sobre la versión actual del plan, con `/egm:egm-review` en una sesión nueva y
el paso `8` y después el `9`.

## Resolución (Elephant)

Cada hallazgo tiene su resolución en el informe de su etapa: PROPOSE FIX, CONTEST, ESCALATE o DECLINE. El solicitante
pidió completar el trabajo sin intervención, así que se aplicaron en bloque todas las PROPOSE FIX («auto accept»).

| Resultado | Hallazgos |
| --- | --- |
| Corregidos en el plan (v2 → v4) y en las ADR | todos los PROPOSE FIX de las etapas 5–7 |
| ESCALATE: la evidencia está fuera de lo que leyó el revisor | ZO-1 y ZO-2 (alcance: el solicitante pidió las cuatro fases); ST-11 (commits a cargo del solicitante) |
| CONTEST | ST-11: la cabecera «Detailed Implementation Plan» es el marcador de `egm-review` |
| DECLINE | ZO-15 (SPA en el ejemplo) |

## Cambios de diseño más importantes que salieron de la revisión

1. **Seguridad.**
   - Regla de subconjunto al delegar, también para grupos.
   - Sesiones de soporte de solo lectura, con la passkey del admin en el canje.
   - *Token exchange* atado a la audiencia del cliente.
   - La red de confianza falla cerrada.
2. **Claims para la SPA.** Pasan por `userinfo`, no por el ID token (el logout lo lleva en la URL) ni por el access
   token (RFC 9068 §6).
3. **Aviso de cambios.** Contadores de versión en Valkey en lugar de *refresh* por latido, que costaba carga y dejaba
   sesiones que no caducaban.
4. **Simplificación.**
   - Planes como conjuntos de features.
   - Redes de confianza en Tenancy.
   - Sin job para el acceso temporal.
   - Grupos de OpenFGA como *contextual tuples*.
   - Un solo evaluador.
5. **Documento.** Problema y objetivos, alternativas, riesgos por tipo, flujos, glosario y trazabilidad.
