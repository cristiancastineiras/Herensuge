// @herensuge/react: SDK de Herensuge para SPAs React servidas por una BFF de Herensuge (mismo origen).
// La sesión vive en una cookie HttpOnly de la BFF: ningún token llega al navegador y no hay nada que
// configurar.
import { createContext, use, useEffect, useState, type ReactNode } from "react"

// ── Sesión de la BFF (sin React) ────────────────────────────────────────────

/** Usuario de la sesión: respuesta de GET /bff/user. */
export type HerensugeUser = {
	subject: string
	sessionId: string | null
	name: string | null
	email: string | null
	tenantId: string | null
	tenantRole: "owner" | "admin" | "member" | null
	/** amr: "pwd", "otp", "sms", "pop" (passkey), "mfa", "fed". */
	authenticationMethods: string[]
	/** Segundos desde epoch. */
	authenticationTime: number | null
	isPlatformAdmin: boolean
	/** Navegar aquí cierra la sesión en la BFF y en el IdP. */
	logoutUrl: string
}

const currentPath = () => window.location.pathname + window.location.search

/** Inicia sesión en el IdP y vuelve a `returnUrl` (por defecto, la página actual). */
export function signIn(returnUrl = currentPath()): void {
	window.location.assign(`/bff/login?returnUrl=${encodeURIComponent(returnUrl)}`)
}

/** Operaciones sensibles: exige volver a introducir las credenciales (max_age=0). */
export function reauthenticate(returnUrl = currentPath()): void {
	window.location.assign(`/bff/login?max_age=0&returnUrl=${encodeURIComponent(returnUrl)}`)
}

/** Cambia de organización: tokens nuevos para ese tenant, sin pedir credenciales si la sesión sigue viva. */
export function switchTenant(tenantId: string, returnUrl = "/"): void {
	const query = `tenant=${encodeURIComponent(tenantId)}&returnUrl=${encodeURIComponent(returnUrl)}`
	window.location.assign(`/bff/login?${query}`)
}

/** Cierra la sesión de verdad: en la BFF, en el IdP y, al momento, en las demás pestañas y aplicaciones. */
export function signOut(user: HerensugeUser): void {
	window.location.assign(user.logoutUrl)
}

/** `fetch` a la BFF con la cabecera `X-CSRF: 1`, que exige en /api/*. */
export function herensugeFetch(url: string | URL, init: RequestInit = {}): Promise<Response> {
	const headers = new Headers(init.headers)
	headers.set("X-CSRF", "1")
	return fetch(url, { credentials: "same-origin", ...init, headers })
}

/** Usuario de la sesión, o `null` si no hay sesión (401). Lanza un Error si la BFF no responde bien. */
export async function fetchUser(): Promise<HerensugeUser | null> {
	const response = await herensugeFetch("/bff/user", { headers: { Accept: "application/json" } })
	if (response.status === 401) {
		return null
	}

	if (!response.ok) {
		throw new Error(`GET /bff/user: HTTP ${response.status}`)
	}

	return (await response.json()) as HerensugeUser
}

const RECONNECT_MS = 5000

/**
 * Avisa en cuanto la sesión termina, la cierre quien la cierre: esta pestaña u otra, otro dispositivo
 * ("cerrar sesión" en la lista de sesiones), un cambio de contraseña... Escucha GET /bff/events
 * (server-sent events). Devuelve la función que deja de escuchar.
 */
export function watchSession(onSignedOut: () => void): () => void {
	let source: EventSource | undefined
	let retry: ReturnType<typeof setTimeout> | undefined
	let stopped = false

	const stop = () => {
		stopped = true
		clearTimeout(retry)
		source?.close()
	}

	const end = () => {
		stop()
		onSignedOut()
	}

	const connect = () => {
		const events = new EventSource("/bff/events")
		source = events
		events.addEventListener("signed-out", end)
		events.addEventListener("error", async () => {
			// CONNECTING: el navegador reconecta solo. CLOSED: la BFF ha rechazado la conexión. Si es porque
			// ya no hay sesión (401), se avisa; si no (despliegue, red), se vuelve a intentar.
			if (events.readyState !== EventSource.CLOSED) {
				return
			}

			const user = await fetchUser().catch(() => undefined)
			if (stopped) {
				return
			}

			if (user === null) {
				end()
			} else {
				retry = setTimeout(connect, RECONNECT_MS)
			}
		})
	}

	connect()
	return stop
}

// ── React ───────────────────────────────────────────────────────────────────

/** Estado de la sesión. `error`: la BFF no ha respondido (red, 5xx); no significa que no haya sesión. */
export type HerensugeSession =
	| { status: "loading"; user: null }
	| { status: "signedIn"; user: HerensugeUser }
	| { status: "signedOut"; user: null }
	| { status: "error"; user: null; error: unknown }

const SessionContext = createContext<HerensugeSession | null>(null)
const signedOut: HerensugeSession = { status: "signedOut", user: null }

/**
 * Carga la sesión (/bff/user) y la vigila en tiempo real (/bff/events): cuando se cierra, el estado pasa a
 * `signedOut` al momento, sin esperar a la siguiente llamada a la API.
 */
export function HerensugeProvider({ children }: { children: ReactNode }) {
	const [session, setSession] = useState<HerensugeSession>({ status: "loading", user: null })

	useEffect(() => {
		let active = true
		fetchUser().then(
			(user) => active && setSession(user ? { status: "signedIn", user } : signedOut),
			(error: unknown) => active && setSession({ status: "error", user: null, error }),
		)
		return () => {
			active = false
		}
	}, [])

	const isSignedIn = session.status === "signedIn"
	useEffect(() => (isSignedIn ? watchSession(() => setSession(signedOut)) : undefined), [isSignedIn])

	return <SessionContext value={session}>{children}</SessionContext>
}

/** Sesión actual. Se usa dentro de <HerensugeProvider>. */
export function useHerensuge(): HerensugeSession {
	const session = use(SessionContext)
	if (!session) {
		throw new Error("useHerensuge() se usa fuera de <HerensugeProvider>.")
	}

	return session
}

/** Usuario de la sesión. Solo donde ya se sabe que hay sesión (status "signedIn"). */
export function useUser(): HerensugeUser {
	const session = useHerensuge()
	if (session.status !== "signedIn") {
		throw new Error('useUser() necesita una sesión iniciada (status "signedIn").')
	}

	return session.user
}

/** Lleva al login al montarse y vuelve después a `returnUrl` (por defecto, la página actual). */
export function RedirectToSignIn({ returnUrl }: { returnUrl?: string }) {
	useEffect(() => signIn(returnUrl), [returnUrl])
	return null
}
