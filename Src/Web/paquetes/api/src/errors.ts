/** Cuerpo de error de la API (ProblemDetails, RFC 9457) con la extensión `code`. */
export type ProblemDetails = {
	title?: string
	status?: number
	code?: string
	/** `reauth_required` cuando la operación exige un inicio de sesión reciente. */
	error?: string
	/** Errores de validación por campo (camelCase). */
	errors?: Record<string, string[]>
	traceId?: string
}

export class ApiError extends Error {
	readonly status: number
	readonly code: string | undefined
	readonly problem: ProblemDetails | undefined

	constructor(status: number, problem?: ProblemDetails) {
		super(problem?.title ?? `HTTP ${status}`)
		this.name = "ApiError"
		this.status = status
		this.code = problem?.code
		this.problem = problem
	}

	/** La API pide volver a iniciar sesión antes de una operación sensible. */
	get requiresReauthentication(): boolean {
		return this.status === 401 && this.problem?.error === "reauth_required"
	}
}

/** Texto en español para cada `code` de la API. Si falta uno, se muestra el `title` de la respuesta. */
const messages: Record<string, string> = {
	"General.Network": "No se ha podido conectar con el servidor. Comprueba tu conexión.",
	"General.TooManyRequests": "Demasiados intentos. Espera un minuto y vuelve a probar.",
	"General.ConcurrencyConflict":
		"Otra persona ha cambiado estos datos. Recarga la página y vuelve a intentarlo.",
	"General.Unexpected": "Ha ocurrido un error inesperado.",
	"Security.CsrfHeaderMissing": "Petición no válida. Recarga la página.",

	"Auth.NotSignedIn": "Tu inicio de sesión ha caducado. Vuelve a empezar.",
	"Auth.ReauthenticationRequired": "Por seguridad, vuelve a iniciar sesión para continuar.",
	"Auth.TenantMismatch": "Has cambiado de organización. Recarga la página.",

	"Users.InvalidCredentials": "El email o la contraseña no son correctos.",
	"Users.EmailNotVerified": "Todavía no has verificado tu email.",
	"Users.LockedOut": "Demasiados intentos fallidos. Prueba de nuevo en unos minutos.",
	"Users.EmailAlreadyInUse": "Ese email ya está en uso.",
	"Users.InvalidEmail": "El email no es válido.",
	"Users.InvalidCurrentPassword": "La contraseña actual no es correcta.",
	"Users.InvalidMfaCode": "El código no es correcto.",
	"Users.MfaMethodNotAvailable": "Ese método de verificación no está activado.",
	"Users.MfaNotEnabled": "No tienes la verificación en dos pasos activada.",
	"Users.LastAccessMethod": "No puedes quitar tu último método de acceso.",
	"Users.TotpAlreadyEnabled": "La app autenticadora ya está activada.",
	"Users.TotpNotEnabled": "La app autenticadora no está activada.",
	"Users.TotpSetupNotStarted": "Empieza de nuevo la configuración de la app autenticadora.",
	"Users.PhoneNumberNotSet": "No tienes un teléfono verificado.",
	"Users.PasskeyAlreadyRegistered": "Esta passkey ya está registrada.",
	"Users.PasskeyLimitReached": "Has alcanzado el máximo de passkeys.",
	"Users.PasskeyNotFound": "La passkey no existe.",
	"Users.PasskeyVerificationFailed": "No se ha podido verificar la passkey.",
	"Users.ExternalLoginInUse": "Esa cuenta externa ya está vinculada a otro usuario.",
	"Users.ExternalLoginNotFound": "Esa cuenta no está vinculada.",
	"Users.ExternalEmailMissing": "El proveedor no ha compartido tu email.",
	"Users.NotFound": "El usuario no existe.",

	"Sessions.NotFound": "La sesión no existe.",
	"Sessions.Inactive": "La sesión ha caducado o se ha cerrado.",

	"Tenants.NotFound": "La organización no existe.",
	"Tenants.Inactive": "La organización está desactivada.",
	"Memberships.NotFound": "No perteneces a esa organización.",
	"Memberships.NotAllowed": "Tu rol no permite esta operación.",
	"Memberships.LastOwner": "La organización debe tener al menos un propietario.",
	"Memberships.AlreadyMember": "Esa persona ya es miembro de la organización.",
	"Memberships.InvalidRole": "El rol no es válido.",
	"Invitations.NotFound": "La invitación no existe.",
	"Invitations.NotPending": "La invitación ya se ha usado, se ha revocado o ha caducado.",
	"Invitations.EmailNotVerified": "Verifica tu email antes de aceptar la invitación.",

	"Clients.AlreadyExists": "Ya existe un cliente con ese id.",
	"Clients.NotFound": "El cliente no existe.",
	"Clients.SystemClient": "Este cliente se gestiona desde la configuración.",
	"Clients.PublicClientHasNoSecret": "Los clientes públicos no tienen secreto.",

	"Oidc.SessionInvalid": "Tu sesión ha caducado o se ha cerrado.",
	"Oidc.TenantInactive": "La organización está desactivada.",
	"Oidc.MembershipRevoked": "Ya no perteneces a la organización.",
	"Oidc.MfaRequired": "La organización exige verificación en dos pasos.",

	// Errores que el IdP pasa a la web de login en ?error=
	external_failed: "No se ha podido iniciar sesión con el proveedor externo.",
	link_requires_sign_in: "Inicia sesión antes de vincular una cuenta externa.",
}

/** Errores de validación: el campo (camelCase) que ha fallado. */
const fieldMessages: Record<string, string> = {
	email: "Introduce un email válido.",
	newEmail: "Introduce un email válido.",
	password: "La contraseña debe tener entre 12 y 128 caracteres.",
	newPassword: "La contraseña debe tener entre 12 y 128 caracteres.",
	displayName: "Introduce un nombre de como máximo 100 caracteres.",
	name: "Revisa el nombre.",
	code: "El código no tiene el formato correcto.",
	phoneNumber: "Escribe el teléfono en formato internacional, por ejemplo +34600111222.",
	clientId: "El client id debe tener entre 3 y 100 caracteres: minúsculas, números y guiones.",
	redirectUris:
		"Revisa las URIs de redirección: URLs HTTPS absolutas, sin fragmento (http solo para localhost).",
	postLogoutRedirectUris: "Revisa las URIs de cierre de sesión: deben ser URLs absolutas.",
	scopes: "Revisa los scopes.",
	role: "El rol no es válido.",
}

export function errorCode(error: unknown): string | undefined {
	return error instanceof ApiError ? error.code : undefined
}

/** Errores del navegador al usar una passkey (WebAuthn), por su `name`. */
const webAuthnMessages: Record<string, string> = {
	NotAllowedError: "Se ha cancelado la operación con la passkey o ha caducado.",
	AbortError: "Se ha cancelado la operación con la passkey.",
	InvalidStateError: "Esta passkey ya está registrada en este dispositivo.",
	SecurityError: "Este dominio no admite passkeys (revisa Identity:Passkeys:RelyingPartyId).",
	NotSupportedError: "Este navegador o dispositivo no admite passkeys.",
}

/** Mensaje para el usuario a partir de cualquier error. */
export function describeError(error: unknown): string {
	if (!(error instanceof ApiError)) {
		const name = error instanceof Error ? error.name : ""
		return webAuthnMessages[name] ?? "Ha ocurrido un error inesperado."
	}

	const fieldErrors = error.problem?.errors
	if (fieldErrors && Object.keys(fieldErrors).length > 0) {
		return Object.entries(fieldErrors)
			.map(([field, details]) => fieldMessages[field] ?? details.join(" "))
			.join(" ")
	}

	if (error.code && messages[error.code]) {
		return messages[error.code] as string
	}

	if (error.status === 401) {
		return "Tu sesión ha caducado. Vuelve a iniciar sesión."
	}

	if (error.status === 403) {
		return "No tienes permiso para hacer esto."
	}

	return error.problem?.title ?? "Ha ocurrido un error inesperado."
}

/** Texto para un `?error=` recibido en la URL (código de la API o error de OAuth). */
export function describeErrorCode(code: string): string {
	return messages[code] ?? "No se ha podido completar el inicio de sesión."
}
