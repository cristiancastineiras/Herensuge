import { createApiClient, type SignInResponse } from "@paquetes/api"
import { startAuthentication, type PublicKeyCredentialRequestOptionsJSON } from "@simplewebauthn/browser"

// /api/v1/auth: API de inicio de sesión de Herensuge.Api (mismo origen, cookie del IdP).
// Rutas, cuerpos y respuestas los comprueba TypeScript contra el contrato OpenAPI generado.
const { api, call } = createApiClient()
const token = (value: string) => ({ params: { path: { token: value } } })

export const auth = {
	session: () => call(api.GET("/api/v1/auth/session")),
	providers: () => call(api.GET("/api/v1/auth/providers")),
	signIn: (body: { email: string; password: string }) => call(api.POST("/api/v1/auth/sign-in", { body })),
	signOut: () => call(api.POST("/api/v1/auth/sign-out")),
	register: (body: { email: string; password: string; displayName: string }) =>
		call(api.POST("/api/v1/auth/register", { body })),
	verifyEmail: (value: string) => call(api.POST("/api/v1/auth/verify-email", { body: { token: value } })),
	resendVerification: (email: string) =>
		call(api.POST("/api/v1/auth/verify-email/resend", { body: { email } })),
	forgotPassword: (email: string) => call(api.POST("/api/v1/auth/password/forgot", { body: { email } })),
	resetPassword: (body: { token: string; newPassword: string }) =>
		call(api.POST("/api/v1/auth/password/reset", { body })),
	confirmEmailChange: (value: string) =>
		call(api.POST("/api/v1/auth/email-change/confirm", { body: { token: value } })),

	mfaChallenge: () => call(api.GET("/api/v1/auth/sign-in/mfa")),
	verifyMfa: (body: { method: string; code: string }) => call(api.POST("/api/v1/auth/sign-in/mfa", { body })),
	sendSmsCode: () => call(api.POST("/api/v1/auth/sign-in/mfa/sms")),
	beginTotp: () => call(api.POST("/api/v1/auth/mfa/totp")),
	confirmTotp: (code: string) => call(api.POST("/api/v1/auth/mfa/totp/confirm", { body: { code } })),

	tenants: () => call(api.GET("/api/v1/auth/tenants")),
	selectTenant: (tenantId: string) => call(api.POST("/api/v1/auth/tenants/select", { body: { tenantId } })),
	createTenant: (name: string) => call(api.POST("/api/v1/auth/tenants", { body: { name } })),
	invitation: (value: string) => call(api.GET("/api/v1/auth/invitations/{token}", token(value))),
	acceptInvitation: (value: string) =>
		call(api.POST("/api/v1/auth/invitations/{token}/accept", token(value))),

	/** Google / Microsoft: navegación de nivel superior, no fetch. */
	externalUrl: (provider: string, returnUrl: string | null) =>
		`/api/v1/auth/external/${provider}${returnUrl ? `?returnUrl=${encodeURIComponent(returnUrl)}` : ""}`,
}

/** Passkey: opciones del servidor → navegador (WebAuthn) → verificación en el servidor. */
export async function signInWithPasskey(): Promise<SignInResponse> {
	// Las opciones y la respuesta de WebAuthn son JSON del estándar (JsonElement en el contrato).
	const options = (await call(
		api.POST("/api/v1/auth/passkeys/options"),
	)) as PublicKeyCredentialRequestOptionsJSON
	const assertion = await startAuthentication({ optionsJSON: options })
	return call(api.POST("/api/v1/auth/passkeys/sign-in", { body: assertion }))
}

export const providerNames: Record<string, string> = { google: "Google", microsoft: "Microsoft" }
