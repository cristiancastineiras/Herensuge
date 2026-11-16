import type { HerensugeUser } from "@herensuge/react"

/** Usuario de la sesión del portal. AppLayout solo pinta las páginas cuando hay sesión. */
export { useUser } from "@herensuge/react"

export const isTenantAdmin = (user: HerensugeUser) =>
	user.tenantRole === "owner" || user.tenantRole === "admin"

/** Inició sesión con passkey (requisito de la administración de plataforma). */
export const usedPasskey = (user: HerensugeUser) => user.authenticationMethods.includes("pop")

/** Las operaciones sensibles exigen un inicio de sesión de menos de 10 minutos. */
export const signedInRecently = (user: HerensugeUser) =>
	user.authenticationTime !== null && Date.now() / 1000 - user.authenticationTime < 9 * 60

export const idpUrl = import.meta.env.VITE_URL_IDP || null
