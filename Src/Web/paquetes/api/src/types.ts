// Tipos de la API: alias de schema.d.ts, que se GENERA desde el contrato OpenAPI de Herensuge.Api
// (pnpm api:generate). No se escriben a mano; aquí solo se les da un nombre corto.
import type { components } from "./schema"

type Schemas = components["schemas"]

export type SignInResponse = Schemas["SignInResponse"]
export type MfaChallengeResponse = Schemas["MfaChallengeResponse"]
export type SessionStateResponse = Schemas["SessionStateResponse"]
export type TotpSetupResponse = Schemas["TotpSetupResponse"]
export type RecoveryCodesResponse = Schemas["RecoveryCodesResponse"]
export type InvitationPreviewResponse = Schemas["InvitationPreviewResponse"]
export type CurrentUserResponse = Schemas["CurrentUserResponse"]
export type PasskeyResponse = Schemas["PasskeyResponse"]
export type SessionResponse = Schemas["SessionResponse"]
export type ExternalLoginResponse = Schemas["ExternalLoginResponse"]
export type UserTenantResponse = Schemas["UserTenantResponse"]
export type TenantResponse = Schemas["TenantResponse"]
export type MemberResponse = Schemas["MemberResponse"]
export type InvitationResponse = Schemas["InvitationResponse"]
export type AuditPage = Schemas["AuditPage"]
export type PlatformTenantResponse = Schemas["PlatformTenantResponse"]
export type ClientResponse = Schemas["ClientResponse"]
export type CreateClientRequest = Schemas["CreateClientRequest"]

// Valores que la API envía como texto (el contrato solo dice `string`).
export type TenantRole = "owner" | "admin" | "member"

// ── Contrato de Herensuge.Bff (no forma parte del OpenAPI de la API) ─────────

export type BffUser = {
	subject: string
	sessionId: string | null
	name: string | null
	email: string | null
	tenantId: string | null
	tenantRole: TenantRole | null
	authenticationMethods: string[]
	/** Segundos desde epoch. */
	authenticationTime: number | null
	isPlatformAdmin: boolean
	logoutUrl: string
}

/** GET /bff/user en el formato de openapi-typescript, para usar el mismo cliente tipado. */
export type BffPaths = {
	"/bff/user": {
		parameters: { query?: never; header?: never; path?: never; cookie?: never }
		get: {
			parameters: { query?: never; header?: never; path?: never; cookie?: never }
			requestBody?: never
			responses: { 200: { headers: Record<string, unknown>; content: { "application/json": BffUser } } }
		}
	}
}
