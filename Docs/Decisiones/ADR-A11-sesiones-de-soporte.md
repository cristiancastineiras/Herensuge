# ADR-A11: impersonación con sesiones de soporte de solo lectura

- **Estado:** aceptada · 16/11/2026 (revisada tras la etapa 7 de EGM)
- **Plan:** [plan de implementación](../Arquitectura/Autorizacion-Implementacion.md) D11
- **Sustituye:** «RFC 8693 (token exchange) con claim `act`» del [diseño general](../Arquitectura/Autorizacion.md), fase 4

## Contexto

Soporte necesita ver el portal como lo ve un usuario.

- RFC 8693 canjea un `subject_token` del usuario, y el admin no lo tiene.
- Dar escritura a soporte permite que un admin de plataforma comprometido deje acceso permanente en cualquier
  organización (invitaciones, cuentas de servicio, owners nuevos). Además, la auditoría lo atribuiría al usuario.

## Decisión

**Inicio.**
- Un admin de plataforma con passkey pide una sesión de soporte con email y motivo. No se puede abrir sobre otro admin
  de plataforma.
- Recibe un enlace de un solo uso de 5 min con el token en el **fragmento** (`/support#<token>`). Se audita y se avisa al
  usuario por email.

**Canje.**
- La SPA de login pide **la passkey del mismo admin** y envía `POST /api/v1/auth/support/redeem` con `X-CSRF`.
- Se rechaza sin consumir el enlace si el navegador ya tiene una sesión del IdP.

**Sesión.**
- Una `UserSession` del usuario con `ImpersonatorUserId`, `amr = ["imp"]` y 30 min de vida absoluta.
- Los tokens llevan `act: { sub: <admin> }`.
- **Solo lectura.** Un filtro global rechaza toda petición que cambie algo si hay `act`, salvo los endpoints marcados
  `AllowDuringSupport()`. Los endpoints del IdP que usan la cookie comprueban el actor.
- **Alcance.** Solo los clientes de `Identity:Support:Clients` (por defecto, el portal).
- **MFA.** La sesión cumple la MFA obligatoria de la organización; los permisos con `requires: mfa` se siguen denegando.

**Fin.** Al cerrar sesión o al caducar. Se audita.

**Activación.** Apagado por defecto (`Identity:Support:Enabled`).

## Consecuencias

- Soporte no puede corregir datos por el usuario. Si alguna vez hace falta, cada operación se marcará
  `AllowDuringSupport()` y la auditoría tendrá que registrar también al actor.
- El *token exchange* se usa solo entre servicios (plan, D14).
