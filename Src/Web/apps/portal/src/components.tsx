import type { AuditPage } from "@paquetes/api"
import { Button, Card, ErrorAlert, Field, Input, Loading, Table, Td } from "@paquetes/componentes"
import { formValues, formatDateTime } from "@paquetes/utiles"
import { useInfiniteQuery } from "@tanstack/react-query"
import { useState, type ReactNode } from "react"

/** Título de cada página (y título de la pestaña). */
export function PageHeader({
	title,
	description,
	actions,
}: {
	title: string
	description?: string
	actions?: ReactNode
}) {
	return (
		<div className="flex flex-wrap items-end justify-between gap-4">
			<title>{`${title} · Herensuge`}</title>
			<div>
				<h1 className="text-2xl font-semibold text-zinc-900">{title}</h1>
				{description && <p className="mt-1 text-sm text-zinc-500">{description}</p>}
			</div>
			{actions}
		</div>
	)
}

/** Rol que envía la API ("owner", "admin", "member") → texto. */
export const roleLabels: Record<string, string> = {
	owner: "Propietario",
	admin: "Administrador",
	member: "Miembro",
}

/** Pantalla de carga o de error de una consulta; si hay datos, no pinta nada. */
export function QueryState({ query }: { query: { isPending: boolean; error: unknown } }) {
	if (query.isPending) {
		return <Loading />
	}

	return <ErrorAlert error={query.error} />
}

/** Acción de auditoría (nombre del integration event sin "IntegrationEvent") → texto. */
const actionLabels: Record<string, string> = {
	UserRegistered: "Registro",
	EmailVerificationRequested: "Verificación de email solicitada",
	EmailVerified: "Email verificado",
	EmailChangeRequested: "Cambio de email solicitado",
	EmailChanged: "Email cambiado",
	PasswordChanged: "Contraseña cambiada",
	PasswordResetRequested: "Recuperación de contraseña solicitada",
	SignInSucceeded: "Inicio de sesión",
	SignInFailed: "Inicio de sesión fallido",
	NewDeviceSignedIn: "Inicio de sesión en un dispositivo nuevo",
	UserLockedOut: "Cuenta bloqueada",
	SessionRevoked: "Sesión cerrada",
	MfaEnabled: "MFA activado",
	MfaDisabled: "MFA desactivado",
	SmsMfaCodeRequested: "Código por SMS solicitado",
	PhoneVerificationRequested: "Verificación de teléfono solicitada",
	PasskeyAdded: "Passkey añadida",
	PasskeyRemoved: "Passkey eliminada",
	ExternalLoginLinked: "Cuenta externa vinculada",
	ExternalLoginUnlinked: "Cuenta externa desvinculada",
	UserDeleted: "Cuenta eliminada",
	PlatformAdminGranted: "Admin de plataforma concedido",
	TenantCreated: "Organización creada",
	TenantUpdated: "Organización actualizada",
	TenantStatusChanged: "Organización activada o desactivada",
	MemberAdded: "Miembro añadido",
	MemberRoleChanged: "Rol cambiado",
	MemberRemoved: "Miembro eliminado",
	InvitationCreated: "Invitación enviada",
	InvitationAccepted: "Invitación aceptada",
	InvitationRevoked: "Invitación revocada",
}

/**
 * Usuario de un evento: su nombre si se conoce; si no, el final del id (los UUID v7 empiezan por la fecha,
 * así que el principio se repite entre usuarios creados a la vez). El id completo, al pasar el ratón.
 */
function UserCell({ id, names }: { id: string | null; names: Record<string, string> }) {
	if (!id) {
		return <Td>—</Td>
	}

	const name = names[id]
	return (
		<Td className={name ? undefined : "font-mono text-xs"} title={id}>
			{name ?? `…${id.slice(-8)}`}
		</Td>
	)
}

/** Registro de auditoría paginado por cursor (organización o plataforma). */
type AuditQuery = { action: string; before: string | null; pageSize: number }

/** `load` es audit.tenant(…) o audit.platform(…) de api.ts; `id` distingue la caché de cada registro. */
type AuditLogProps = {
	/** Distingue la caché de cada registro (organización o plataforma). */
	id: string
	/** audit.tenant(…) o audit.platform(…) de api.ts. */
	load: (query: AuditQuery) => Promise<AuditPage>
	/** Nombres conocidos por id de usuario (los miembros de la organización). */
	names?: Record<string, string>
}

const noNames: Record<string, string> = {}

export function AuditLog({ id, load, names = noNames }: AuditLogProps) {
	const [action, setAction] = useState("")
	const entries = useInfiniteQuery({
		queryKey: ["audit", id, action],
		queryFn: ({ pageParam }) => load({ action, before: pageParam, pageSize: 50 }),
		initialPageParam: null as string | null,
		getNextPageParam: (page) => page.nextCursor,
	})
	const items = entries.data?.pages.flatMap((page) => page.items) ?? []

	return (
		<Card
			flush
			title="Eventos"
			actions={
				<form
					className="flex items-end gap-2"
					onSubmit={(event) => setAction(formValues(event, "action").action.trim())}
				>
					<Field label="Acción">
						<Input name="action" placeholder="p. ej. SignInSucceeded" className="w-56 py-1.5" />
					</Field>
					<Button type="submit" variant="secondary">
						Filtrar
					</Button>
				</form>
			}
		>
			<div className="px-5">
				<QueryState query={entries} />
			</div>
			{items.length > 0 && (
				<Table headers={["Fecha", "Acción", "Usuario", "Actor", "IP"]}>
					{items.map((entry) => (
						<tr key={entry.id}>
							<Td className="whitespace-nowrap">{formatDateTime(entry.occurredAtUtc)}</Td>
							<Td>
								<span className="font-medium text-zinc-900">
									{actionLabels[entry.action] ?? entry.action}
								</span>
								<span className="block font-mono text-xs text-zinc-400">{entry.action}</span>
							</Td>
							<UserCell id={entry.subjectUserId} names={names} />
							<UserCell id={entry.actorUserId} names={names} />
							<Td className="font-mono text-xs">{entry.ipAddress ?? "—"}</Td>
						</tr>
					))}
				</Table>
			)}
			{entries.isSuccess && items.length === 0 && (
				<p className="px-5 pb-4 text-sm text-zinc-500">No hay eventos.</p>
			)}
			{entries.hasNextPage && (
				<div className="border-t border-zinc-100 px-5 py-3">
					<Button
						variant="secondary"
						loading={entries.isFetchingNextPage}
						onClick={() => entries.fetchNextPage()}
					>
						Cargar más
					</Button>
				</div>
			)}
		</Card>
	)
}
