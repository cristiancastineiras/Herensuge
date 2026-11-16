# ADR-A2: la API de decisión sigue OpenID AuthZEN

- **Estado:** aceptada · 16/11/2026
- **Plan:** [Autorizacion.md](../Arquitectura/Autorizacion.md), sección 8.1

## Contexto

Las APIs de las apps del SaaS (PEP) necesitan preguntar "¿puede este usuario hacer esto?" para lo sensible y lo que no
viaja en el token. OpenID AuthZEN (Authorization API) estandariza esa pregunta.

## Decisión

- `POST /access/v1/evaluation` y `POST /access/v1/evaluations` con la forma de AuthZEN: `subject`, `action`, `resource`
  → `{ decision, context: { reason } }`. *Discovery* en `/.well-known/authzen-configuration`.
- El permiso es `resource.type + ":" + action.name` (`invoice:approve`).
- `subject.properties` lleva los claims del token del usuario: `tenant_id`, `sid`, `amr` y `auth_time`.
- Solo lo llaman los backends de las apps, con su token `client_credentials` (scope `herensuge.access`), y cada una solo
  pregunta por su propia aplicación.

## Consecuencias

- Cualquier PEP compatible con AuthZEN sirve; el SDK .NET propio es opcional.
- Restricción propia: un solo sujeto por llamada en lote (AuthZEN permite uno por elemento).
- Hay que cotejar los nombres de campos y de *discovery* con la versión vigente de la especificación antes de
  publicarlo a terceros.
