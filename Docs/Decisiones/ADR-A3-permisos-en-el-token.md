# ADR-A3: permisos en el token por audiencia, y consulta en vivo para lo sensible

- **Estado:** aceptada · 16/11/2026
- **Plan:** [Autorizacion.md](../Arquitectura/Autorizacion.md), secciones 5 y 9

## Contexto

Consultar la PDP en cada petición añade latencia y una dependencia; meterlo todo en el JWT da tokens grandes y con hasta
5 minutos de retraso.

## Decisión

- El access token de una app lleva `roles` (claves de los roles de la app) y `permissions` (RFC 9068) de **esa** app,
  recalculados en cada *refresh*.
- Si la app declara `embedPermissions: false` en su manifiesto (catálogos grandes), solo viajan los `roles` y su API
  pregunta a la PDP.
- Los permisos con condición (`requires: mfa | recent_auth`) y la autorización por objeto se consultan siempre en vivo.

## Consecuencias

- Lo habitual se decide en la API de la app sin red.
- Quitar un rol tarda como mucho la vida del access token (5 min) en el token, y nada en la PDP.
- Los roles **propios** de cada organización no viajan como `roles` (sus nombres no significan nada para la app), solo
  sus permisos.
