# ADR-A9: condiciones fijas antes que un lenguaje de políticas

- **Estado:** aceptada · 16/11/2026
- **Plan:** [Autorizacion.md](../Arquitectura/Autorizacion.md) §7; [plan de implementación](../Arquitectura/Autorizacion-Implementacion.md) D3, D9

## Contexto

Un lenguaje general (Cedar, Rego, CEL) da flexibilidad, pero añade un motor, una sintaxis que aprender y políticas
difíciles de auditar.

## Decisión

- **Condiciones.** Un permiso puede exigir una sola, declarada en el manifiesto: `mfa`, `recent_auth` (menos de 10 min)
  o `trusted_network` (redes de la organización, en Tenancy).
- **Token.** Un permiso con condición viaja en `conditional_permissions`, no en `permissions`: la API pregunta siempre a
  la PDP.

## Consecuencias

- Las tres condiciones cubren los casos de un SaaS B2B y se explican en una línea.
- Si se quedan cortas, se valorará Cedar o CEL con su propio ADR.
