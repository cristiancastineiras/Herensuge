import createClient from "openapi-fetch"
import { ApiError, type ProblemDetails } from "./errors"
import type { paths } from "./schema"
import type { BffPaths } from "./types"

export type ApiClientOptions = {
	/** Se llama con cada 401 antes de lanzar el error (por ejemplo, para volver a iniciar sesión). */
	onUnauthorized?: (error: ApiError) => void
}

/**
 * Cliente de Herensuge.Api tipado con el contrato OpenAPI (Docs/API/Herensuge.Api.json): rutas, parámetros,
 * cuerpos y respuestas se comprueban al compilar. Mismo origen (cookies, sin CORS) y siempre con
 * `X-CSRF: 1`, que exigen el IdP y la BFF.
 *
 *   const { api, call } = createApiClient()
 *   const user = await call(api.GET("/api/v1/me"))
 */
export function createApiClient(options: ApiClientOptions = {}) {
	const api = createClient<paths & BffPaths>({
		credentials: "same-origin",
		headers: { Accept: "application/json", "X-CSRF": "1" },
	})

	/** Devuelve los datos o lanza ApiError (ProblemDetails), que es lo que espera TanStack Query. */
	async function call<Data>(
		request: Promise<{ data?: Data; error?: unknown; response: Response }>,
	): Promise<Data> {
		let result: Awaited<typeof request>
		try {
			result = await request
		} catch {
			throw new ApiError(0, { code: "General.Network" })
		}

		if (result.response.ok) {
			return result.data as Data
		}

		const problem =
			typeof result.error === "object" && result.error !== null ? (result.error as ProblemDetails) : undefined
		const error = new ApiError(result.response.status, problem)
		if (error.status === 401) {
			options.onUnauthorized?.(error)
		}

		throw error
	}

	return { api, call }
}
