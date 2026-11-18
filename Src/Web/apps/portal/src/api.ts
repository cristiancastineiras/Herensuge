import { reauthenticate, signIn } from "@herensuge/react"
import {
	createApiClient,
	type AccessManifest,
	type AssignedRoleResponse,
	type CreateClientRequest,
	type LoginSettings,
	type PlanRequest,
	type RoleGrant,
	type RoleRequest,
} from "@paquetes/api"
import { startRegistration, type PublicKeyCredentialCreationOptionsJSON } from "@simplewebauthn/browser"

// El portal solo habla con su BFF (mismo origen): la sesión (/bff/*) la lleva @herensuge/react y aquí
// están las llamadas a /api/* (que la BFF reenvía a Herensuge.Api con el access token). Ningún token llega
// al navegador. Rutas, cuerpos y respuestas los comprueba TypeScript contra el contrato OpenAPI generado.

export { reauthenticate, signIn, switchTenant } from "@herensuge/react"

const { api, call } = createApiClient({
	// 401: sesión caducada o cerrada → login. 401 con reauth_required → login reciente y volver aquí.
	onUnauthorized: (error) => (error.requiresReauthentication ? reauthenticate() : signIn()),
})

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
	/** `trustedNetworks: null` deja las redes como están. */
	update: (id: string, body: { name: string; requireMfa: boolean; trustedNetworks: string[] | null }) =>
		call(api.PATCH("/api/v1/tenants/{tenantId}", { ...tenant(id), body })),
	plan: (id: string) => call(api.GET("/api/v1/tenants/{tenantId}/plan", tenant(id))),
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

// ── Accesos de la organización: roles y permisos de las aplicaciones ────────

const role = (tenantId: string, roleId: string) => ({ params: { path: { tenantId, roleId } } })

export const access = {
	/** Aplicaciones con sus permisos y plantillas de rol. */
	catalog: (id: string) => call(api.GET("/api/v1/tenants/{tenantId}/permissions", tenant(id))),
	roles: (id: string) => call(api.GET("/api/v1/tenants/{tenantId}/roles", tenant(id))),
	createRole: (id: string, body: RoleRequest) =>
		call(api.POST("/api/v1/tenants/{tenantId}/roles", { ...tenant(id), body })),
	updateRole: (id: string, roleId: string, body: RoleRequest) =>
		call(api.PATCH("/api/v1/tenants/{tenantId}/roles/{roleId}", { ...role(id, roleId), body })),
	deleteRole: (id: string, roleId: string) =>
		call(api.DELETE("/api/v1/tenants/{tenantId}/roles/{roleId}", role(id, roleId))),
	simulate: (id: string, body: { userId: string; resourceServer: string; permission: string }) =>
		call(api.POST("/api/v1/tenants/{tenantId}/access/simulate", { ...tenant(id), body })),
}

// Roles de un sujeto (miembro, grupo o cuenta de servicio): mismas operaciones, tres rutas.
const subject = (tenantId: string, subjectId: string) => ({ params: { path: { tenantId, subjectId } } })

/** Carga y guarda los roles de un sujeto; lo usa el editor de roles. */
export type SubjectRoles = {
	load: () => Promise<AssignedRoleResponse[]>
	save: (roles: RoleGrant[]) => Promise<unknown>
}

export const subjectRoles = {
	member: (id: string, userId: string): SubjectRoles => ({
		load: () => call(api.GET("/api/v1/tenants/{tenantId}/members/{subjectId}/roles", subject(id, userId))),
		save: (roles) =>
			call(
				api.PUT("/api/v1/tenants/{tenantId}/members/{subjectId}/roles", {
					...subject(id, userId),
					body: { roles },
				}),
			),
	}),
	group: (id: string, groupId: string): SubjectRoles => ({
		load: () => call(api.GET("/api/v1/tenants/{tenantId}/groups/{subjectId}/roles", subject(id, groupId))),
		save: (roles) =>
			call(
				api.PUT("/api/v1/tenants/{tenantId}/groups/{subjectId}/roles", {
					...subject(id, groupId),
					body: { roles },
				}),
			),
	}),
	serviceAccount: (id: string, accountId: string): SubjectRoles => ({
		load: () =>
			call(api.GET("/api/v1/tenants/{tenantId}/service-accounts/{subjectId}/roles", subject(id, accountId))),
		save: (roles) =>
			call(
				api.PUT("/api/v1/tenants/{tenantId}/service-accounts/{subjectId}/roles", {
					...subject(id, accountId),
					body: { roles },
				}),
			),
	}),
}

const group = (tenantId: string, groupId: string) => ({ params: { path: { tenantId, groupId } } })

export const groups = {
	list: (id: string) => call(api.GET("/api/v1/tenants/{tenantId}/groups", tenant(id))),
	create: (id: string, body: { name: string; description: string | null }) =>
		call(api.POST("/api/v1/tenants/{tenantId}/groups", { ...tenant(id), body })),
	update: (id: string, groupId: string, body: { name: string; description: string | null }) =>
		call(api.PATCH("/api/v1/tenants/{tenantId}/groups/{groupId}", { ...group(id, groupId), body })),
	remove: (id: string, groupId: string) =>
		call(api.DELETE("/api/v1/tenants/{tenantId}/groups/{groupId}", group(id, groupId))),
	setMembers: (id: string, groupId: string, userIds: string[]) =>
		call(
			api.PUT("/api/v1/tenants/{tenantId}/groups/{groupId}/members", {
				...group(id, groupId),
				body: { userIds },
			}),
		),
}

const account = (tenantId: string, serviceAccountId: string) => ({
	params: { path: { tenantId, serviceAccountId } },
})

export const serviceAccounts = {
	list: (id: string) => call(api.GET("/api/v1/tenants/{tenantId}/service-accounts", tenant(id))),
	create: (id: string, name: string) =>
		call(api.POST("/api/v1/tenants/{tenantId}/service-accounts", { ...tenant(id), body: { name } })),
	rotateSecret: (id: string, accountId: string) =>
		call(
			api.POST(
				"/api/v1/tenants/{tenantId}/service-accounts/{serviceAccountId}/secret",
				account(id, accountId),
			),
		),
	remove: (id: string, accountId: string) =>
		call(
			api.DELETE("/api/v1/tenants/{tenantId}/service-accounts/{serviceAccountId}", account(id, accountId)),
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
	/** Pantalla de login del cliente: marca, métodos de acceso y campos del registro. */
	loginPage: (clientId: string) =>
		call(api.GET("/api/v1/platform/clients/{clientId}/login-page", client(clientId))),
	updateLoginPage: (clientId: string, body: LoginSettings) =>
		call(api.PUT("/api/v1/platform/clients/{clientId}/login-page", { ...client(clientId), body })),

	features: () => call(api.GET("/api/v1/platform/features")),
	plans: () => call(api.GET("/api/v1/platform/plans")),
	createPlan: (body: PlanRequest) => call(api.POST("/api/v1/platform/plans", { body })),
	updatePlan: (body: PlanRequest) =>
		call(api.PATCH("/api/v1/platform/plans/{key}", { params: { path: { key: body.key } }, body })),
	setDefaultPlan: (key: string) =>
		call(api.POST("/api/v1/platform/plans/{key}/default", { params: { path: { key } } })),
	deletePlan: (key: string) =>
		call(api.DELETE("/api/v1/platform/plans/{key}", { params: { path: { key } } })),
	tenantPlans: () => call(api.GET("/api/v1/platform/tenant-plans")),
	/** `planKey: null`: vuelve al plan por defecto. */
	setTenantPlan: (tenantId: string, planKey: string | null) =>
		call(
			api.PUT("/api/v1/platform/tenants/{tenantId}/plan", {
				params: { path: { tenantId } },
				body: { planKey },
			}),
		),

	resourceServers: () => call(api.GET("/api/v1/platform/resource-servers")),
	syncManifest: (manifest: AccessManifest) =>
		call(
			api.PUT("/api/v1/platform/resource-servers/{key}/manifest", {
				params: { path: { key: manifest.resourceServer } },
				body: manifest,
			}),
		),
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

// ── Accesos temporales (plan D13) ───────────────────────────────────────────

const accessRequest = (tenantId: string, requestId: string) => ({ params: { path: { tenantId, requestId } } })

export const accessRequests = {
	/** Mis permisos por aplicación y los roles que puedo pedir. */
	myAccess: (id: string) => call(api.GET("/api/v1/tenants/{tenantId}/me/access", tenant(id))),
	mine: (id: string) => call(api.GET("/api/v1/tenants/{tenantId}/me/access-requests", tenant(id))),
	request: (
		id: string,
		body: { roleId: string | null; roleTemplateId: string | null; hours: number; reason: string },
	) => call(api.POST("/api/v1/tenants/{tenantId}/me/access-requests", { ...tenant(id), body })),
	all: (id: string) => call(api.GET("/api/v1/tenants/{tenantId}/access-requests", tenant(id))),
	approve: (id: string, requestId: string) =>
		call(
			api.POST(
				"/api/v1/tenants/{tenantId}/access-requests/{requestId}/approve",
				accessRequest(id, requestId),
			),
		),
	deny: (id: string, requestId: string) =>
		call(
			api.POST("/api/v1/tenants/{tenantId}/access-requests/{requestId}/deny", accessRequest(id, requestId)),
		),
}

// ── Soporte (plan D11) ──────────────────────────────────────────────────────

export const support = {
	/** Enlace de un solo uso (5 min) para abrir una sesión de solo lectura en la cuenta del usuario. */
	start: (body: { email: string; reason: string }) =>
		call(api.POST("/api/v1/platform/support-sessions", { body })),
}
