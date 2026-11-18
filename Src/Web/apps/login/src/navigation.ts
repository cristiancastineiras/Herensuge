import { useSearchParams } from "react-router"

/** Solo rutas locales: evita redirecciones abiertas (?returnUrl=https://otro-sitio). */
export function safeLocalPath(url: string | null | undefined): string | null {
	if (!url || !url.startsWith("/") || url.startsWith("//") || url.startsWith("/\\")) {
		return null
	}

	return url
}

/** El ?returnUrl= de la página actual (normalmente /connect/authorize?...), si es local. */
export function useReturnUrl(): string | null {
	const [params] = useSearchParams()
	return safeLocalPath(params.get("returnUrl"))
}

/** Ruta de la SPA conservando el returnUrl: withReturnUrl("/mfa", returnUrl). */
export function withReturnUrl(path: string, returnUrl: string | null): string {
	return returnUrl ? `${path}?returnUrl=${encodeURIComponent(returnUrl)}` : path
}

/**
 * Sigue el flujo OIDC. Es una navegación completa porque returnUrl suele ser /connect/authorize,
 * que responde el backend (no la SPA). Sin returnUrl se vuelve a la portada del login.
 */
export function continueTo(returnUrl: string | null): void {
	window.location.assign(returnUrl ?? "/")
}

export const portalUrl = import.meta.env.VITE_URL_PORTAL || null

/** client_id de un returnUrl que reanuda /connect/authorize: la aplicación en la que se está entrando. */
export function clientIdOf(returnUrl: string | null): string | null {
	if (!returnUrl) {
		return null
	}

	const url = new URL(returnUrl, window.location.origin)
	return url.pathname.endsWith("/connect/authorize") ? url.searchParams.get("client_id") : null
}

// La última aplicación en la que se intentó entrar, para los enlaces de los emails (verificar el email, cambiar la
// contraseña), que llegan sin returnUrl. No es un dato de sesión: solo decide la marca y adónde vuelve "Iniciar sesión".
const lastReturnUrlKey = "herensuge.ultimo-returnUrl"

export function rememberReturnUrl(returnUrl: string): void {
	try {
		localStorage.setItem(lastReturnUrlKey, returnUrl)
	} catch {
		// Sin almacenamiento (modo privado estricto): los enlaces de los emails usan la marca de Herensuge.
	}
}

export function rememberedReturnUrl(): string | null {
	try {
		return safeLocalPath(localStorage.getItem(lastReturnUrlKey))
	} catch {
		return null
	}
}

/** /login de la aplicación con la que se estaba entrando: la del returnUrl o, en los enlaces de los emails, la última. */
export function useSignInPath(): string {
	return withReturnUrl("/login", useReturnUrl() ?? rememberedReturnUrl())
}
