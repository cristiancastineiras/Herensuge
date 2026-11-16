# Etapa 6: prueba de comprensión

**Objetivo:** `Docs/Arquitectura/Autorizacion-Implementacion.md`, versión 2.
- FeatureRange: líneas 1–312.
- DetailedRange: líneas 313–444.

**Revisor:** `egm-comprehension` (goldfish). Solo leyó el documento, sin contexto ni estándar.

## Qué propone (paráfrasis del revisor)

- **Fase 1.** El plan completa las fases 1 a 4 de la autorización de Herensuge. Herensuge protege sus pantallas de roles,
  auditoría, cuentas de servicio y accesos temporales con permisos de su catálogo, concedidos con la plantilla
  `organization-admin` a owner y admin. Los permisos llegan a la SPA en el ID token a través de la BFF (`useCan`). Los
  cambios se avisan con un latido de 25 s y un canal Valkey que acelera el aviso. Además, un SDK .NET y una app de
  ejemplo.
- **Fases 2 a 4.** Planes, grupos, cuentas de servicio, red de confianza y simulador (2); permisos por objeto con OpenFGA
  (3); impersonación de soporte, acceso temporal con aprobación y *token exchange* (4). Las fases 3 y 4 vienen apagadas
  por defecto.
- **Evaluación.** Todo pasa por un único `AccessEvaluator` (7 pasos), compartido por la PDP, las comprobaciones internas
  y el simulador. Una regla de subconjunto impide conceder lo que el actor no tiene.
- **Trabajo.** WP0 a WP7; cada WP es un PR que solo se da por terminado con la CI en verde.

## Cómo funciona el sistema existente, según este documento (paráfrasis del revisor)

- **Catálogo.** Cada app es un `ResourceServer` con permisos y plantillas, sincronizado desde un manifiesto. Al
  sincronizar, Oidc crea el scope `<app>.api` y la audiencia `<app>-api`.
- **Roles y token.** Las organizaciones tienen roles propios y asignaciones con caducidad. El access token lleva
  `roles` y `permissions`.
- **API de decisión.** La consultan los backends (AuthZEN) con `client_credentials` y `herensuge.access`.
- **Administración.** Se protege con el rol base de Tenancy.
- **BFF.** Expone `/bff/events` y `/bff/user`; Identity publica las revocaciones en Valkey.
- **Estado.** Nada se ha ejecutado contra una base de datos real.

## Hallazgos

| Id | Sev. | Hueco | Corrección aplicada (v3) |
| --- | --- | --- | --- |
| CO-1 | 🔴 | No está claro qué decide `RequirePermission` (token o evaluador), ni qué hechos usan las comprobaciones internas | D1: secuencia exacta (el filtro lee el claim; el handler vuelve a evaluar contra la base de datos); hechos de las comprobaciones internas; por qué hay ventana de compatibilidad |
| CO-2 | 🟡 | `embedPermissions` no se define, ni qué app va en el ID token | D2: dónde vive, valor por defecto, estado actual y regla audiencia → app |
| CO-3 | 🟡 | El orden de los WP no respeta las dependencias (`entitlements`, `act`, `WriteRelationshipAsync`, `ExchangeTokenAsync`, IP) | Cada pieza se mueve al WP que crea su parte de servidor |
| CO-4 | 🟡 | Los ADR se citan sin su contenido | Tabla de ADR: decisión en una frase y sección donde se desarrolla |
| CO-5 | 🟡 | «5 min 25 s» sin explicar | Vida del token y cálculo |
| CO-6 | 🟡 | No se explica el despliegue de las BFF ni quién usa Valkey | Párrafo de despliegue: qué BFF hay, Valkey compartido, comportamiento sin Valkey, `userId` ausente |
| CO-7 | 🟡 | Semántica ambigua de condicionales y relacionales | Tabla de casos: claim, `useCan`, SDK y regla de subconjunto |
| CO-8 | 🟡 | API de relaciones sin autenticación ni origen de la organización | D10: credencial, forma de la petición, origen de la app y de la organización, réplica de grupos |
| CO-9 | 🟡 | Decisiones sin paso en ningún WP | Tabla de trazabilidad D → WP y pasos añadidos |
| CO-10 | 🟡 | El actor del *token exchange* no está claro | D14: parámetros, actor y rechazo si el `subject_token` ya trae `act` |
| CO-11 | 🟢 | Falta un glosario y un mapa de módulos | Sección 0 |
| CO-12 | 🟢 | Los códigos de revisión no se explican | Leyenda; «el solicitante» |
| CO-13 | 🟢 | Falta el contexto del entorno | Frase en la sección 8 |
| CO-14 | 🟢 | Notación imprecisa | Algoritmo, «manifiesto canónico», herramienta y ruta corregidos |
