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
