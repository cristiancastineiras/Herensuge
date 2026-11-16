import { Badge, Button, Card, ConfirmButton, ErrorAlert, Table, Td } from "@paquetes/componentes"
import { formatDateTime } from "@paquetes/utiles"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { accessRequests, tenants } from "../api"
import { PageHeader, QueryState, requestStatus } from "../components"
import { useUser } from "../user"

/** Accesos temporales: aprobar exige tener los permisos del rol pedido; nadie resuelve los suyos. */
export default function AccessRequestsPage() {
	const user = useUser()
	const tenantId = user.tenantId ?? ""
	const queryClient = useQueryClient()
	const requests = useQuery({
		queryKey: ["access-requests", tenantId],
		queryFn: () => accessRequests.all(tenantId),
	})
	const members = useQuery({ queryKey: ["members", tenantId], queryFn: () => tenants.members(tenantId) })
	const names = Object.fromEntries((members.data ?? []).map((member) => [member.userId, member.displayName]))
	const refresh = () => queryClient.invalidateQueries({ queryKey: ["access-requests", tenantId] })
	const approve = useMutation({
		mutationFn: (id: string) => accessRequests.approve(tenantId, id),
		onSuccess: refresh,
	})
	const deny = useMutation({
		mutationFn: (id: string) => accessRequests.deny(tenantId, id),
		onSuccess: refresh,
	})

	return (
		<>
			<PageHeader
				title="Accesos temporales"
				description="Peticiones de roles por horas. Al aprobar, el rol se asigna con caducidad."
			/>
			<Card flush title="Peticiones">
				<div className="px-5">
					<QueryState query={requests} />
					<ErrorAlert error={approve.error ?? deny.error} />
				</div>
				{requests.data?.length === 0 && <p className="px-5 pb-4 text-sm text-zinc-500">No hay peticiones.</p>}
				{requests.data && requests.data.length > 0 && (
					<Table headers={["Persona", "Rol", "Horas", "Motivo", "Pedida", "Estado", ""]}>
						{requests.data.map((request) => (
							<tr key={request.id}>
								<Td className="font-medium text-zinc-900">{names[request.userId] ?? "…"}</Td>
								<Td>
									{request.roleName ?? "—"}
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
								<Td className="space-x-1 text-right whitespace-nowrap">
									{request.status === "pending" && request.userId !== user.subject && (
										<>
											<Button
												variant="secondary"
												loading={approve.isPending && approve.variables === request.id}
												onClick={() => approve.mutate(request.id)}
											>
												Aprobar
											</Button>
											<ConfirmButton confirmLabel="Rechazar" onConfirm={() => deny.mutate(request.id)}>
												Rechazar
											</ConfirmButton>
										</>
									)}
								</Td>
							</tr>
						))}
					</Table>
				)}
			</Card>
		</>
	)
}
