# ADR-A8: permisos por objeto con OpenFGA, opcional

- **Estado:** aceptada · 16/11/2026
- **Plan:** [plan de implementación](../Arquitectura/Autorizacion-Implementacion.md) D10

## Contexto

Compartir *esta* factura con una persona concreta no se resuelve con roles. Un motor de relaciones propio (tipo
Zanzibar) es difícil de hacer bien: consistencia, herencia y *new enemy problem*.

## Decisión

- **Motor.** OpenFGA (Apache-2.0) con el SDK `OpenFga.Sdk`, desactivado por defecto (`Access:OpenFga:ApiUrl`).
- **Modelo fijo** escrito por Herensuge:
  - `user`, `client` y `group#member`;
  - `object` con `owner ⊂ editor ⊂ viewer` y `parent`.
- **Ids.** `object:<app>/<tenantId>/<type>/<id>`, compuestos por Herensuge: una app no puede tocar objetos de otra ni de
  otra organización.
- **Acceso de las apps.** Escriben tuplas **a través de Herensuge** (`/access/v1/relationships`), con auditoría.
- **Combinación por permiso:** `relationMode` `or` (por defecto: compartir concede) o `and` (hacen falta el rol y la
  relación).
- **Consistencia alta** en las comprobaciones.

## Consecuencias

- **Descartados:** un modelo por app generado desde el manifiesto (exige traducir DSL) y SpiceDB, que es una
  alternativa válida y se puede volver a evaluar.
- Sin OpenFGA configurado, los permisos relacionales se deniegan con `relationships_disabled`.
