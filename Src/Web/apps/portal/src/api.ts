import { createApiClient, type CreateClientRequest } from "@paquetes/api"
import { startRegistration, type PublicKeyCredentialCreationOptionsJSON } from "@simplewebauthn/browser"

// El portal solo habla con su BFF (mismo origen): /bff/* y /api/* (que la BFF reenvía a Herensuge.Api
// con el access token). Ningún token llega al navegador. Rutas, cuerpos y respuestas los comprueba
// TypeScript contra el contrato OpenAPI generado.

const currentPath = () => window.location.pathname + window.location.search

/** Inicia sesión (o la renueva) en el IdP y vuelve a la página actual. */
export function signIn(returnUrl = currentPath()): void {
	window.location.assign(`/bff/login?returnUrl=${encodeURIComponent(returnUrl)}`)
}

/** Operaciones sensibles: exige volver a introducir las credenciales (max_age=0). */
export function reauthenticate(returnUrl = currentPath()): void {
	window.location.assign(`/bff/login?max_age=0&returnUrl=${encodeURIComponent(returnUrl)}`)
}

/** Cambia de organización: tokens nuevos para ese tenant (sin pedir credenciales si la sesión sigue viva). */
export function switchTenant(tenantId: string): void {
	window.location.assign(`/bff/login?tenant=${encodeURIComponent(tenantId)}&returnUrl=/`)
}

const { api, call } = createApiClient({
	// 401: sesión caducada → login. 401 con reauth_required → login reciente y volver aquí.
	onUnauthorized: (error) => (error.requiresReauthentication ? reauthenticate() : signIn()),
})

export const bff = {
	user: () => call(api.GET("/bff/user")),
}

// ── Mi cuenta: /api/v1/me ───────────────────────────────────────────────────

const passkey = (passkeyId: string) => ({ params: { path: { passkeyId } } })

export const me = {
	get: () => call(api.GET("/api/v1/me")),
	update: (displayName: string) => call(api.PATCH("/api/v1/me", { body: { displayName } })),
	remove: () => call(api.DELETE("/api/v1/me")),
	changeEmail: (newEmail: string) => call(api.POST("/api/v1/me/email", { body: { newEmail } })),
	changePassword: (body: { currentPassword: string | null; newPassword: string }) =>
		call(api.POST("/api/v1/me/password", { body })),

	beginTotp: () => call(api.POST("/api/v1/me/mfa/totp")),
	confirmTotp: (code: string) => call(api.POST("/api/v1/me/mfa/totp/confirm", { body: { code } })),
	disableTotp: () => call(api.DELETE("/api/v1/me/mfa/totp")),
	regenerateRecoveryCodes: () => call(api.POST("/api/v1/me/mfa/recovery-codes")),

	startPhoneVerification: (phoneNumber: string) =>
		call(api.POST("/api/v1/me/phone", { body: { phoneNumber } })),
	confirmPhone: (code: string) => call(api.POST("/api/v1/me/phone/verify", { body: { code } })),
	removePhone: () => call(api.DELETE("/api/v1/me/phone")),

	passkeys: () => call(api.GET("/api/v1/me/passkeys")),
	renamePasskey: (id: string, name: string) =>
		call(api.PATCH("/api/v1/me/passkeys/{passkeyId}", { ...passkey(id), body: { name } })),
	removePasskey: (id: string) => call(api.DELETE("/api/v1/me/passkeys/{passkeyId}", passkey(id))),

	sessions: () => call(api.GET("/api/v1/me/sessions")),
	revokeSession: (sessionId: string) =>
		call(api.DELETE("/api/v1/me/sessions/{sessionId}", { params: { path: { sessionId } } })),
	revokeOtherSessions: () => call(api.DELETE("/api/v1/me/sessions")),

	externalLogins: () => call(api.GET("/api/v1/me/external-logins")),
	unlinkExternalLogin: (provider: string) =>
		call(api.DELETE("/api/v1/me/external-logins/{provider}", { params: { path: { provider } } })),
	providers: () => call(api.GET("/api/v1/auth/providers")),

	tenants: () => call(api.GET("/api/v1/me/tenants")),
}

/** Alta de passkey: opciones del servidor → navegador (WebAuthn) → registro en el servidor. */
export async function addPasskey(name: string) {
	// Las opciones y la credencial son JSON del estándar WebAuthn (JsonElement en el contrato).
	const options = (await call(
		api.POST("/api/v1/me/passkeys/options"),
	)) as PublicKeyCredentialCreationOptionsJSON
	const credential = await startRegistration({ optionsJSON: options })
	return call(api.POST("/api/v1/me/passkeys", { body: { credential, name: name || null } }))
}

// ── Organización activa: /api/v1/tenants/{tenantId} ─────────────────────────

const tenant = (tenantId: string) => ({ params: { path: { tenantId } } })
const member = (tenantId: string, userId: string) => ({ params: { path: { tenantId, userId } } })

export const tenants = {
	get: (id: string) => call(api.GET("/api/v1/tenants/{tenantId}", tenant(id))),
	update: (id: string, body: { name: string; requireMfa: boolean }) =>
		call(api.PATCH("/api/v1/tenants/{tenantId}", { ...tenant(id), body })),
	members: (id: string) => call(api.GET("/api/v1/tenants/{tenantId}/members", tenant(id))),
	changeRole: (id: string, userId: string, role: string) =>
		call(api.PATCH("/api/v1/tenants/{tenantId}/members/{userId}", { ...member(id, userId), body: { role } })),
	removeMember: (id: string, userId: string) =>
		call(api.DELETE("/api/v1/tenants/{tenantId}/members/{userId}", member(id, userId))),
	invitations: (id: string) => call(api.GET("/api/v1/tenants/{tenantId}/invitations", tenant(id))),
	invite: (id: string, body: { email: string; role: string }) =>
		call(api.POST("/api/v1/tenants/{tenantId}/invitations", { ...tenant(id), body })),
	revokeInvitation: (id: string, invitationId: string) =>
		call(
			api.DELETE("/api/v1/tenants/{tenantId}/invitations/{invitationId}", {
				params: { path: { tenantId: id, invitationId } },
			}),
		),
}

// ── Plataforma: /api/v1/platform ────────────────────────────────────────────

const client = (clientId: string) => ({ params: { path: { clientId } } })

export const platform = {
	tenants: (query: { search: string; page: number; pageSize: number }) =>
		call(
			api.GET("/api/v1/platform/tenants", {
				params: { query: { ...query, search: query.search || undefined } },
			}),
		),
	setTenantStatus: (tenantId: string, isActive: boolean) =>
		call(
			api.PATCH("/api/v1/platform/tenants/{tenantId}", {
				params: { path: { tenantId } },
				body: { isActive },
			}),
		),

	clients: () => call(api.GET("/api/v1/platform/clients")),
	createClient: (body: CreateClientRequest) => call(api.POST("/api/v1/platform/clients", { body })),
	updateClient: ({ clientId, isConfidential: _, ...body }: CreateClientRequest) =>
		call(api.PATCH("/api/v1/platform/clients/{clientId}", { ...client(clientId), body })),
	rotateSecret: (clientId: string) =>
		call(api.POST("/api/v1/platform/clients/{clientId}/secret", client(clientId))),
	deleteClient: (clientId: string) =>
		call(api.DELETE("/api/v1/platform/clients/{clientId}", client(clientId))),
}

// ── Auditoría (organización activa o plataforma) ────────────────────────────

type AuditQuery = { action: string; before: string | null; pageSize: number }
const auditQuery = ({ action, before, pageSize }: AuditQuery) => ({
	action: action || undefined,
	before: before ?? undefined,
	pageSize,
})

export const audit = {
	tenant: (tenantId: string, query: AuditQuery) =>
		call(
			api.GET("/api/v1/tenants/{tenantId}/audit", {
				params: { path: { tenantId }, query: auditQuery(query) },
			}),
		),
	platform: (query: AuditQuery) =>
		call(api.GET("/api/v1/platform/audit", { params: { query: auditQuery(query) } })),
}
