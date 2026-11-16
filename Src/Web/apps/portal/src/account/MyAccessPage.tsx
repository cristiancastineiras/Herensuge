import type { RequestableRole } from "@paquetes/api"
import {
	Alert,
	Badge,
	Button,
	Card,
	ErrorAlert,
	Field,
	Form,
	Input,
	Select,
	Table,
	Td,
} from "@paquetes/componentes"
import { formValues, formatDateTime } from "@paquetes/utiles"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { accessRequests } from "../api"
import { PageHeader, QueryState, requestStatus } from "../components"
import { useUser } from "../user"

/** Mis accesos en la organización activa: permisos por aplicación y accesos temporales. */
export default function MyAccessPage() {
	const tenantId = useUser().tenantId ?? ""
	const access = useQuery({
		queryKey: ["my-access", tenantId],
		queryFn: () => accessRequests.myAccess(tenantId),
	})

	return (
		<>
			<PageHeader
				title="Mis accesos"
				description="Lo que puedes hacer en cada aplicación de la organización y los accesos temporales que has pedido."
			/>
			<QueryState query={access} />
			{access.data && (
				<Card flush title="Permisos por aplicación">
					{access.data.applications.length === 0 ? (
						<p className="px-5 py-4 text-sm text-zinc-500">
							Todavía no tienes permisos en ninguna aplicación.
						</p>
					) : (
						<Table headers={["Aplicación", "Roles", "Permisos"]}>
							{access.data.applications.map((application) => (
								<tr key={application.key}>
									<Td className="font-medium text-zinc-900">{application.name}</Td>
									<Td>{application.roles.join(", ") || "—"}</Td>
									<Td className="space-y-1">
										<span className="block font-mono text-xs">
											{application.permissions.join(", ") || "—"}
										</span>
										{application.conditionalPermissions.length > 0 && (
											<span
												className="block font-mono text-xs text-amber-700"
												title="Con condición (MFA, red, objeto…)"
											>
												{application.conditionalPermissions.join(", ")}
											</span>
										)}
									</Td>
								</tr>
							))}
						</Table>
					)}
				</Card>
			)}
			{access.data && <RequestCard roles={access.data.requestable} />}
			<MyRequestsCard />
		</>
	)
}

function RequestCard({ roles }: { roles: RequestableRole[] }) {
	const tenantId = useUser().tenantId ?? ""
	const queryClient = useQueryClient()
	const request = useMutation({
		mutationFn: (body: {
			roleId: string | null
			roleTemplateId: string | null
			hours: number
			reason: string
		}) => accessRequests.request(tenantId, body),
		onSuccess: () => queryClient.invalidateQueries({ queryKey: ["my-access-requests", tenantId] }),
	})

	return (
		<Card
			title="Pedir un acceso temporal"
			description="Entre 1 y 72 horas. Lo aprueba quien gestiona los accesos de la organización; si nadie responde en 24 h, caduca."
		>
			<Form
				onSubmit={(event) => {
					const form = event.currentTarget
					const { role, hours, reason } = formValues(event, "role", "hours", "reason")
					const [kind, id] = role.split(":") as [string, string]
					request.mutate(
						{
							roleId: kind === "role" ? id : null,
							roleTemplateId: kind === "template" ? id : null,
							hours: Number(hours),
							reason: reason.trim(),
						},
						{ onSuccess: () => form.reset() },
					)
				}}
			>
				<div className="grid gap-4 sm:grid-cols-[1fr_8rem]">
					<Field label="Rol">
						<Select name="role" required defaultValue="">
							<option value="" disabled>
								Elige…
							</option>
							{roles.map((role) => (
								<option
									key={role.roleId ?? role.roleTemplateId}
									value={role.roleId ? `role:${role.roleId}` : `template:${role.roleTemplateId}`}
								>
									{role.name} · {role.group}
								</option>
							))}
						</Select>
					</Field>
					<Field label="Horas">
						<Input name="hours" type="number" min={1} max={72} defaultValue={8} required />
					</Field>
				</div>
				<Field label="Motivo">
					<Input name="reason" required maxLength={500} placeholder="p. ej. cierre del trimestre" />
				</Field>
				<ErrorAlert error={request.error} />
				{request.isSuccess && <Alert tone="success">Petición enviada.</Alert>}
				<Button type="submit" loading={request.isPending}>
					Pedir acceso
				</Button>
			</Form>
		</Card>
	)
}

function MyRequestsCard() {
	const tenantId = useUser().tenantId ?? ""
	const requests = useQuery({
		queryKey: ["my-access-requests", tenantId],
		queryFn: () => accessRequests.mine(tenantId),
	})

	return (
		<Card flush title="Mis peticiones">
			<div className="px-5">
				<QueryState query={requests} />
			</div>
			{requests.data?.length === 0 && (
				<p className="px-5 pb-4 text-sm text-zinc-500">No has pedido accesos.</p>
			)}
			{requests.data && requests.data.length > 0 && (
				<Table headers={["Rol", "Horas", "Motivo", "Pedida", "Estado"]}>
					{requests.data.map((request) => (
						<tr key={request.id}>
							<Td>
								<span className="font-medium text-zinc-900">{request.roleName ?? "—"}</span>
								{request.applicationName && (
									<span className="block text-xs text-zinc-500">{request.applicationName}</span>
								)}
							</Td>
							<Td>{request.hours}</Td>
							<Td>{request.reason}</Td>
							<Td className="whitespace-nowrap">{formatDateTime(request.createdAtUtc)}</Td>
							<Td>
								<Badge tone={requestStatus[request.status]?.tone ?? "neutral"}>
									{requestStatus[request.status]?.label ?? request.status}
								</Badge>
							</Td>
						</tr>
					))}
				</Table>
			)}
		</Card>
	)
}
