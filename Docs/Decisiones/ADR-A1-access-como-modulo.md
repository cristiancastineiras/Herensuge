# ADR-A1: Access es un módulo de Herensuge.Api, no un servicio aparte

- **Estado:** aceptada · 16/11/2026
- **Plan:** [Autorizacion.md](../Arquitectura/Autorizacion.md), secciones 4 y 5

## Contexto

Los permisos, roles por organización y la API de decisión (PDP) necesitan la membresía (Tenancy), la sesión (Identity) y
el emisor de tokens (Oidc). Herensuge es un monolito modular con outbox/inbox y un esquema por módulo.

## Decisión

Un módulo nuevo, `Access` (`Domain`, `Application`, `Infrastructure`, `Presentation`, `Contracts`), con su esquema `access`,
dentro de `Herensuge.Api`. Habla con los demás módulos solo por `Contracts` e integration events.

## Consecuencias

- La PDP está a una llamada en proceso de la membresía y de la sesión; sin red ni base de datos extra.
- Comparte outbox, auditoría y despliegue.
- Se puede extraer más adelante como cualquier módulo: el contrato hacia las apps ya es HTTP (ADR-A2).
- Escalar la PDP por separado obliga a extraerlo; hoy no hace falta.
