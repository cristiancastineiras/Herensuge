# ADR-A4: un token, una API

- **Estado:** aceptada · 16/11/2026
- **Plan:** [Autorizacion.md](../Arquitectura/Autorizacion.md), sección 5

## Contexto

Si un token sirviera para varias APIs, sus `permissions` se mezclarían y cada API recibiría permisos ajenos.

## Decisión

- Cada app tiene un scope (`<clave>.api`) y una audiencia (`<clave>-api`), derivados de la clave de su manifiesto y
  creados por Oidc al sincronizarlo.
- La BFF de cada app pide solo el scope de su API.
- Si un token acaba con dos apps de Access en su audiencia, no lleva `roles` ni `permissions` de ninguna: sus APIs
  preguntan a la PDP.
- Una app = un cliente OIDC: el `clientId` del manifiesto es único entre apps y es el único que puede preguntar a la PDP
  por ella y actualizar su manifiesto.

## Consecuencias

- Tokens pequeños y sin fugas de permisos entre apps.
- El BFF y el backend de una app comparten cliente (el backend usa `client_credentials` con `herensuge.access`).
  Separarlos es posible más adelante con un cliente por backend.
