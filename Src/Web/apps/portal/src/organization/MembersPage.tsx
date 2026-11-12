import type { TenantRole } from "@paquetes/api"
import {
	Alert,
	Badge,
	Button,
	Card,
	ConfirmButton,
	ErrorAlert,
	Field,
	Input,
	Select,
	Table,
	Td,
} from "@paquetes/componentes"
import { formValues, formatDate } from "@paquetes/utiles"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { tenants } from "../api"
import { PageHeader, QueryState, roleLabels } from "../components"
import { useUser } from "../user"

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
								<Td className="text-right">
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
		</Card>
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
