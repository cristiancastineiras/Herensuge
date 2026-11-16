# ADR-A6: deny por defecto y sin deny explícito

- **Estado:** aceptada · 16/11/2026
- **Plan:** [Autorizacion.md](../Arquitectura/Autorizacion.md) §7

## Contexto

Las reglas de *deny* explícito obligan a resolver conflictos entre reglas con prioridades, y eso cuesta explicarlo y
auditarlo.

## Decisión

- Sin una concesión (rol, grupo, plantilla por rol base o relación), la respuesta es *deny*.
- No existen reglas de *deny*. Lo que restringe son las **condiciones** del permiso (MFA, inicio de sesión reciente, red
  de confianza) y el **plan** de la organización. Las dos se evalúan después de la concesión y producen razones
  distintas (`step_up_required`, `untrusted_network`, `not_entitled`).

## Consecuencias

- La traza de una decisión es una lista de pasos en orden y la razón dice el primero que falló.
- Para quitar un permiso a una persona concreta hay que quitarle el rol, no añadir una excepción.
