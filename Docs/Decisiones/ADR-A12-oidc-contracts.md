# ADR-A12: Oidc expone una fachada fina para los clientes de las cuentas de servicio

- **Estado:** aceptada · 16/11/2026
- **Plan:** [plan de implementación](../Arquitectura/Autorizacion-Implementacion.md) D7

## Contexto

- **Qué es una cuenta de servicio.** Es de una organización y necesita un cliente OIDC con `client_credentials`.
- **Por qué hace falta un contrato.** El secreto se devuelve una sola vez al crearla, así que la creación del cliente
  tiene que ser síncrona. Oidc no tenía `Contracts` porque delega su modelo en OpenIddict.

## Decisión

- **Proyecto nuevo.** `Herensuge.Modules.Oidc.Contracts` con `IOidcModuleApi`: `CreateServiceClientAsync`,
  `RotateServiceClientSecretAsync` y `DeleteServiceClientAsync`, implementado sobre el `IClientRegistry` existente.
- **Dueño.** Access tiene el agregado `ServiceAccount(TenantId, Name, ClientId)`. Borrar la cuenta borra el cliente y
  sus asignaciones; no hay estado «desactivado» duplicado.
- **Marca del cliente.** El cliente lleva la propiedad `tenant_id`. Con ella, `client_credentials` acepta scopes de app
  y emite `tenant_id` y los permisos del sujeto `client`.

## Consecuencias

- Un módulo más con `Contracts`; los tests de arquitectura lo cubren.
- Las cuentas de servicio no aparecen como clientes editables en Plataforma (propiedad de sistema).
