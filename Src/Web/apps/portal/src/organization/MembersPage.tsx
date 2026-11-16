import type { MemberResponse, RoleGrant, TenantRole } from "@paquetes/api"
import {
	Alert,
	Badge,
	Button,
	Card,
	Checkbox,
	ConfirmButton,
	ErrorAlert,
	Field,
	Form,
	Input,
	Loading,
	Select,
	Table,
	Td,
} from "@paquetes/componentes"
import { formValues, formatDate } from "@paquetes/utiles"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { useState } from "react"
import { access, tenants } from "../api"
import { PageHeader, QueryState, roleLabels } from "../components"
import { Permissions, canInPortal, useUser } from "../user"

const roles: TenantRole[] = ["owner", "admin", "member"]

function RoleOptions() {
	return roles.map((role) => (
		<option key={role} value={role}>
			{roleLabels[role]}
		</option>
	))
}

export default function MembersPage() {
	return (
		<>
			<PageHeader title="Miembros" description="Quién pertenece a la organización y con qué rol." />
			<MembersCard />
			<InvitationsCard />
		</>
	)
}

function MembersCard() {
	const user = useUser()
	const tenantId = user.tenantId ?? ""
	const queryClient = useQueryClient()
	const manageRoles = canInPortal(user, Permissions.manageRoles)
	const [editingRoles, setEditingRoles] = useState<MemberResponse | null>(null)
	const members = useQuery({ queryKey: ["members", tenantId], queryFn: () => tenants.members(tenantId) })
	const refresh = () => queryClient.invalidateQueries({ queryKey: ["members", tenantId] })
	const changeRole = useMutation({
		mutationFn: ({ userId, role }: { userId: string; role: TenantRole }) =>
			tenants.changeRole(tenantId, userId, role),
		onSettled: refresh,
	})
	const remove = useMutation({
		mutationFn: (userId: string) => tenants.removeMember(tenantId, userId),
		onSuccess: refresh,
	})

	return (
		<Card flush title="Miembros">
			<div className="px-5">
				<QueryState query={members} />
				<ErrorAlert error={changeRole.error ?? remove.error} />
			</div>
			{members.data && (
				<Table headers={["Persona", "Rol", "Desde", ""]}>
					{members.data.map((member) => {
						const isMe = member.userId === user.subject
						return (
							<tr key={member.userId}>
								<Td>
									<span className="font-medium text-zinc-900">{member.displayName}</span>{" "}
									{isMe && <Badge>Tú</Badge>}
									<span className="block text-xs text-zinc-500">{member.email}</span>
								</Td>
								<Td>
									<Select
										aria-label={`Rol de ${member.displayName}`}
										className="w-40 py-1.5"
										value={member.role}
										disabled={changeRole.isPending}
										onChange={(event) =>
											changeRole.mutate({ userId: member.userId, role: event.target.value as TenantRole })
										}
									>
										<RoleOptions />
									</Select>
								</Td>
								<Td className="whitespace-nowrap">{formatDate(member.joinedAtUtc)}</Td>
								<Td className="space-x-2 text-right whitespace-nowrap">
									{manageRoles && (
										<Button variant="ghost" onClick={() => setEditingRoles(member)}>
											Roles
										</Button>
									)}
									{!isMe && (
										<ConfirmButton
											question="¿Expulsar?"
											confirmLabel="Expulsar"
											onConfirm={() => remove.mutate(member.userId)}
										>
											Expulsar
										</ConfirmButton>
									)}
								</Td>
							</tr>
						)
					})}
				</Table>
			)}
			{editingRoles && (
				<div className="border-t border-zinc-100 px-5 py-4">
					<MemberRolesEditor
						key={editingRoles.userId}
						member={editingRoles}
						onDone={() => setEditingRoles(null)}
					/>
				</div>
			)}
		</Card>
	)
}

const grantKey = (grant: { roleId: string | null; roleTemplateId: string | null }) =>
	grant.roleId ? `role:${grant.roleId}` : `template:${grant.roleTemplateId}`

/** Fecha de caducidad (yyyy-mm-dd, hora local) → fin de ese día en UTC. */
const endOfDay = (date: string) => (date ? new Date(`${date}T23:59:59`).toISOString() : null)

/** Roles de las aplicaciones de un miembro: plantillas y roles propios, cada uno con caducidad opcional. */
function MemberRolesEditor({ member, onDone }: { member: MemberResponse; onDone: () => void }) {
	const tenantId = useUser().tenantId ?? ""
	const catalog = useQuery({
		queryKey: ["access-catalog", tenantId],
		queryFn: () => access.catalog(tenantId),
	})
	const customRoles = useQuery({ queryKey: ["roles", tenantId], queryFn: () => access.roles(tenantId) })
	const current = useQuery({
		queryKey: ["member-roles", tenantId, member.userId],
		queryFn: () => access.memberRoles(tenantId, member.userId),
	})
	const queryClient = useQueryClient()
	const save = useMutation({
		mutationFn: (grants: RoleGrant[]) => access.setMemberRoles(tenantId, member.userId, grants),
		onSuccess: async () => {
			await queryClient.invalidateQueries({ queryKey: ["member-roles", tenantId, member.userId] })
			await queryClient.invalidateQueries({ queryKey: ["roles", tenantId] })
			onDone()
		},
	})

	if (!catalog.data || !customRoles.data || !current.data) {
		const error = catalog.error ?? customRoles.error ?? current.error
		return error ? <ErrorAlert error={error} /> : <Loading />
	}

	const held = new Map(current.data.map((grant) => [grantKey(grant), grant.expiresAtUtc?.slice(0, 10) ?? ""]))
	const options = [
		...catalog.data.flatMap((application) =>
			application.roles.map((template) => ({
				key: grantKey({ roleId: null, roleTemplateId: template.id }),
				label: template.name,
				group: application.name,
				byBaseRole: template.grantTo.includes(member.role),
			})),
		),
		...customRoles.data.map((role) => ({
			key: grantKey({ roleId: role.id, roleTemplateId: null }),
			label: role.name,
			group: "Roles de la organización",
			byBaseRole: false,
		})),
	]

	return (
		<Form
			onSubmit={(event) => {
				event.preventDefault()
				const data = new FormData(event.currentTarget)
				const grants = data.getAll("grant").map((value) => {
					const key = String(value)
					const [kind, id] = key.split(":") as [string, string]
					return {
						roleId: kind === "role" ? id : null,
						roleTemplateId: kind === "template" ? id : null,
						expiresAtUtc: endOfDay(String(data.get(`expires:${key}`) ?? "")),
					}
				})
				save.mutate(grants)
			}}
		>
			<p className="text-sm font-semibold text-zinc-900">Roles de {member.displayName}</p>
			{options.length === 0 && (
				<p className="text-sm text-zinc-500">Todavía no hay plantillas ni roles que asignar.</p>
			)}
			<ul className="divide-y divide-zinc-100">
				{options.map((option) => (
					<li key={option.key} className="flex flex-wrap items-center justify-between gap-3 py-2">
						{option.byBaseRole ? (
							<Checkbox
								checked
								readOnly
								disabled
								label={option.label}
								hint={`${option.group} · por su rol base (${roleLabels[member.role] ?? member.role})`}
							/>
						) : (
							<Checkbox
								name="grant"
								value={option.key}
								defaultChecked={held.has(option.key)}
								label={option.label}
								hint={option.group}
							/>
						)}
						{!option.byBaseRole && (
							<span className="flex items-center gap-2 text-xs text-zinc-500">
								<span aria-hidden="true">Caduca</span>
								<Input
									type="date"
									name={`expires:${option.key}`}
									aria-label={`Caducidad de ${option.label}`}
									defaultValue={held.get(option.key) ?? ""}
									className="w-40 py-1"
								/>
							</span>
						)}
					</li>
				))}
			</ul>
			<ErrorAlert error={save.error} />
			<div className="flex gap-2">
				<Button type="submit" loading={save.isPending}>
					Guardar roles
				</Button>
				<Button variant="ghost" onClick={onDone}>
					Cancelar
				</Button>
			</div>
		</Form>
	)
}

function InvitationsCard() {
	const tenantId = useUser().tenantId ?? ""
	const queryClient = useQueryClient()
	const invitations = useQuery({
		queryKey: ["invitations", tenantId],
		queryFn: () => tenants.invitations(tenantId),
	})
	const refresh = () => queryClient.invalidateQueries({ queryKey: ["invitations", tenantId] })
	const invite = useMutation({
		mutationFn: (body: { email: string; role: TenantRole }) => tenants.invite(tenantId, body),
		onSuccess: refresh,
	})
	const revoke = useMutation({
		mutationFn: (id: string) => tenants.revokeInvitation(tenantId, id),
		onSuccess: refresh,
	})

	return (
		<Card flush title="Invitaciones" description="La invitación llega por email y caduca a los 7 días.">
			<div className="space-y-4 px-5 py-4">
				<form
					className="flex flex-wrap items-end gap-2"
					onSubmit={(event) => {
						const form = event.currentTarget
						const { email, role } = formValues(event, "email", "role")
						invite.mutate({ email, role: role as TenantRole }, { onSuccess: () => form.reset() })
					}}
				>
					<Field label="Email">
						<Input name="email" type="email" required className="w-64" />
					</Field>
					<Field label="Rol">
						<Select name="role" defaultValue="member" className="w-40">
							<RoleOptions />
						</Select>
					</Field>
					<Button type="submit" loading={invite.isPending}>
						Invitar
					</Button>
				</form>
				{invite.isSuccess && <Alert tone="success">Invitación enviada a {invite.variables.email}.</Alert>}
				<ErrorAlert error={invite.error ?? revoke.error} />
				<QueryState query={invitations} />
				{invitations.data?.length === 0 && (
					<p className="text-sm text-zinc-500">No hay invitaciones pendientes.</p>
				)}
			</div>
			{invitations.data && invitations.data.length > 0 && (
				<Table headers={["Email", "Rol", "Caduca", ""]}>
					{invitations.data.map((invitation) => (
						<tr key={invitation.id}>
							<Td className="font-medium text-zinc-900">{invitation.email}</Td>
							<Td>{roleLabels[invitation.role] ?? invitation.role}</Td>
							<Td className="whitespace-nowrap">{formatDate(invitation.expiresAtUtc)}</Td>
							<Td className="text-right">
								<ConfirmButton confirmLabel="Revocar" onConfirm={() => revoke.mutate(invitation.id)}>
									Revocar
								</ConfirmButton>
							</Td>
						</tr>
					))}
				</Table>
			)}
		</Card>
	)
}
