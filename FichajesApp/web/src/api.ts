import { herensugeFetch, reauthenticate, signIn } from "@herensuge/react"

/** Una jornada de Fichajes.Api. Fechas ISO; `minutes` lo calcula el servidor (null si sigue abierta). */
export type Shift = {
	id: string
	name: string
	clockIn: string
	clockOut: string | null
	minutes: number | null
}

/**
 * Llama a Fichajes.Api a través de la BFF, que añade el access token del usuario (herensugeFetch pone X-CSRF: 1).
 * - 401 con el reto de step-up (insufficient_user_authentication): la acción pide haber iniciado sesión hace poco;
 *   se vuelve a pedir la contraseña.
 * - Cualquier otro 401: la sesión ya no existe; se vuelve al login.
 */
export async function api<T>(path: string, init?: RequestInit): Promise<T> {
	const response = await herensugeFetch(`/api/shifts${path}`, init)
	if (response.status === 401) {
		if (response.headers.get("WWW-Authenticate")?.includes("insufficient_user_authentication")) {
			reauthenticate()
		} else {
			signIn()
		}

		// La página se va al login: no hay nada que pintar.
		return new Promise<T>(() => {})
	}

	if (response.status === 403) {
		throw new Error("No tienes permiso para hacer esto.")
	}

	if (!response.ok) {
		throw new Error(`No se ha podido completar (HTTP ${response.status}).`)
	}

	return (response.status === 204 ? undefined : await response.json()) as T
}
