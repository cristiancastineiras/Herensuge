# ADR-A7: los roles base siguen en Tenancy y conceden plantillas

- **Estado:** aceptada · 16/11/2026
- **Plan:** [Autorizacion.md](../Arquitectura/Autorizacion.md) §6.3; [plan de implementación](../Arquitectura/Autorizacion-Implementacion.md) D1

## Contexto

`owner`, `admin` y `member` son la membresía. Tienen reglas de dominio que no son permisos: solo un owner nombra owners
y siempre queda uno.

## Decisión

- **Tenancy.** Los roles base siguen en Tenancy y gobiernan miembros, invitaciones y ajustes de la organización.
- **Plantillas por rol base.** Una plantilla de app puede concederse por rol base (`grantTo`). El día que se instala una
  app, sus owners ya la usan.
- **Herensuge en Access.** Herensuge se registra como app (`herensuge`) solo para lo que no tiene reglas por rol base:
  `roles:manage`, `audit:read`, `service-accounts:manage` y `access-requests:approve`. Su plantilla
  `organization-admin` se concede a `owner` y `admin`.
- **Sin superusuario implícito.** El owner tiene lo que le conceden las plantillas, y queda exento de la regla de
  subconjunto al delegar (ADR del plan, D12).

## Consecuencias

- **Descartado:** `members:manage`, `invitations:manage` y `tenant:update`. Las reglas por rol base de Tenancy los
  anularían para un `member`.
- Dos fuentes con límites claros: el rol base es la membresía; todo lo demás lo decide Access.
