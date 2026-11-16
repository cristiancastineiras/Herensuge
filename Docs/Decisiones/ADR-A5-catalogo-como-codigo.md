# ADR-A5: el catálogo de permisos es código (manifiesto)

- **Estado:** aceptada · 16/11/2026
- **Plan:** [Autorizacion.md](../Arquitectura/Autorizacion.md) §8.3

## Contexto

Si los permisos de una app se dieran de alta a mano en el portal, se desincronizarían del código que los comprueba.

## Decisión

- Cada app versiona `herensuge.access.json`: clave, cliente, features, permisos y roles que ofrece. Su CI lo sincroniza
  con `PUT /access/v1/manifest` (token `client_credentials` de la app) y Plataforma con
  `PUT /api/v1/platform/resource-servers/{key}/manifest`.
- Un permiso que desaparece se **retira** (no concede nada), no se borra: los roles que lo usan siguen siendo válidos.
- Los comodines (`invoice:*`) se expanden al sincronizar.
- Herensuge compara un hash del manifiesto canónico: sin cambios no hay versión nueva ni eventos.

## Consecuencias

- Renombrar un permiso es retirar uno y añadir otro.
- Herensuge se registra a sí mismo con el mismo mecanismo (`AccessSeeder`).
