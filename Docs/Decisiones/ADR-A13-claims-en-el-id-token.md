# ADR-A13: los claims de acceso llegan a la BFF por userinfo

- **Estado:** aceptada · 16/11/2026 (revisada tras la etapa 7 de EGM: antes decía «en el ID token»)
- **Plan:** [plan de implementación](../Arquitectura/Autorizacion-Implementacion.md) D2, D3

## Contexto

La SPA necesita saber qué puede hacer el usuario para mostrar u ocultar acciones. Las opciones que había:

- **Leer el access token.** Es de la API: RFC 9068 §6 dice que el cliente no lo inspeccione, y con
  `embedPermissions: false` no lleva permisos.
- **Ponerlos en el ID token.** La BFF lo envía como `id_token_hint` en la URL del logout. Con 500 permisos supera el
  límite de la línea de petición (8 KB en Kestrel) y la lista queda en el historial y en los logs.

## Decisión

- `GET /connect/userinfo` devuelve, además de los datos del usuario, los claims de acceso de la app del access token:
  `roles`, `permissions`, `conditional_permissions`, `entitlements` y `act`. Los calcula en el momento con el evaluador
  de Access.
- **Cuándo la llama la BFF.** El cliente OpenIddict llama a `userinfo` en el *callback* y en cada *refresh*. Los dos
  están activos por defecto (`DisableUserInfoRetrieval` y `DisableUserInfo`).
- **Qué hace con ellos.** OpenIddict los combina en el principal (`RefreshTokenAuthenticationResult.Principal`); la BFF
  los copia a la sesión y `/bff/user` los devuelve.
- El ID token no cambia.

## Consecuencias

- `useCan` funciona igual para cualquier app con la plantilla de BFF.
- Cada *refresh* hace una llamada más (`userinfo`), por *backchannel*.
