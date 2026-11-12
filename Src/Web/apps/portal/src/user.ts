import type { BffUser } from "@paquetes/api"
import { createContext, use } from "react"

/** Usuario de la sesión del portal (/bff/user). Lo carga AppLayout antes de pintar cualquier página. */
export const UserContext = createContext<BffUser | null>(null)

export function useUser(): BffUser {
	const user = use(UserContext)
	if (!user) {
		throw new Error("useUser() se usa fuera de AppLayout.")
	}

	return user
}

export const isTenantAdmin = (user: BffUser) => user.tenantRole === "owner" || user.tenantRole === "admin"

/** Inició sesión con passkey (requisito de la administración de plataforma). */
export const usedPasskey = (user: BffUser) => user.authenticationMethods.includes("pop")

/** Las operaciones sensibles exigen un inicio de sesión de menos de 10 minutos. */
export const signedInRecently = (user: BffUser) =>
	user.authenticationTime !== null && Date.now() / 1000 - user.authenticationTime < 9 * 60

export const idpUrl = import.meta.env.VITE_URL_IDP || null
