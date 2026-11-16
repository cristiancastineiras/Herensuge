import { can, type HerensugeUser } from "@herensuge/react"

/** Usuario de la sesión del portal. AppLayout solo pinta las páginas cuando hay sesión. */
export { useUser } from "@herensuge/react"

export const isTenantAdmin = (user: HerensugeUser) =>
	user.tenantRole === "owner" || user.tenantRole === "admin"

/** Permisos de Herensuge (su manifiesto de sistema, herensuge.access.json). */
export const Permissions = {
	manageRoles: "roles:manage",
	readAudit: "audit:read",
	manageServiceAccounts: "service-accounts:manage",
} as const

/**
 * `can` con la misma ventana de compatibilidad que el filtro RequirePermission de la API: si la sesión aún no
 * trae permisos (antes del primer refresco), owner y admin conservan el acceso que tenían.
 */
// shortcut: compat window for sessions started before the access claims; drop it with the API filter's.
export const canInPortal = (user: HerensugeUser, permission: string) =>
	user.permissions.length + user.conditionalPermissions.length === 0
		? isTenantAdmin(user)
		: can(user, permission)

/** Inició sesión con passkey (requisito de la administración de plataforma). */
export const usedPasskey = (user: HerensugeUser) => user.authenticationMethods.includes("pop")

/** Las operaciones sensibles exigen un inicio de sesión de menos de 10 minutos. */
export const signedInRecently = (user: HerensugeUser) =>
	user.authenticationTime !== null && Date.now() / 1000 - user.authenticationTime < 9 * 60

export const idpUrl = import.meta.env.VITE_URL_IDP || null
