import { Badge, Card, ConfirmButton, ErrorAlert, Table, Td } from "@paquetes/componentes"
import { formatDateTime } from "@paquetes/utiles"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { me } from "../api"
import { PageHeader, QueryState } from "../components"

export default function SessionsPage() {
	const queryClient = useQueryClient()
	const sessions = useQuery({ queryKey: ["sessions"], queryFn: me.sessions })
	const refresh = () => queryClient.invalidateQueries({ queryKey: ["sessions"] })
	const revoke = useMutation({ mutationFn: me.revokeSession, onSuccess: refresh })
	const revokeOthers = useMutation({ mutationFn: me.revokeOtherSessions, onSuccess: refresh })
	const others = sessions.data?.filter((session) => !session.isCurrent).length ?? 0

	return (
		<>
			<PageHeader title="Sesiones" description="Dispositivos en los que has iniciado sesión." />
			<Card
				flush
				title="Sesiones abiertas"
				actions={
					others > 0 && (
						<ConfirmButton
							question="¿Cerrar todas las demás?"
							confirmLabel="Cerrar"
							loading={revokeOthers.isPending}
							onConfirm={() => revokeOthers.mutate()}
						>
							Cerrar las demás
						</ConfirmButton>
					)
				}
			>
				<div className="px-5">
					<QueryState query={sessions} />
					<ErrorAlert error={revoke.error ?? revokeOthers.error} />
				</div>
				{sessions.data && (
					<Table headers={["Dispositivo", "IP", "Última actividad", "Inicio", ""]}>
						{sessions.data.map((session) => (
							<tr key={session.id}>
								<Td>
									<span className="font-medium text-zinc-900">{describeDevice(session.userAgent)}</span>{" "}
									{session.isCurrent && <Badge tone="success">Esta sesión</Badge>}
								</Td>
								<Td className="font-mono text-xs">{session.ipAddress ?? "—"}</Td>
								<Td className="whitespace-nowrap">{formatDateTime(session.lastSeenAtUtc)}</Td>
								<Td className="whitespace-nowrap">{formatDateTime(session.createdAtUtc)}</Td>
								<Td className="text-right">
									{!session.isCurrent && (
										<ConfirmButton confirmLabel="Cerrar" onConfirm={() => revoke.mutate(session.id)}>
											Cerrar
										</ConfirmButton>
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

// El orden importa: el User-Agent de Edge también contiene "Chrome" y el de Chrome, "Safari".
const browsers: [RegExp, string][] = [
	[/Edg\//, "Edge"],
	[/OPR\//, "Opera"],
	[/Firefox\//, "Firefox"],
	[/Chrome\//, "Chrome"],
	[/Safari\//, "Safari"],
]
const systems: [RegExp, string][] = [
	[/Windows/, "Windows"],
	[/Android/, "Android"],
	[/iPhone|iPad/, "iOS"],
	[/Mac OS X/, "macOS"],
	[/Linux/, "Linux"],
]

/** "Chrome en Windows" a partir del User-Agent. */
function describeDevice(userAgent: string | null): string {
	if (!userAgent) {
		return "Dispositivo desconocido"
	}

	const find = (list: [RegExp, string][], fallback: string) =>
		list.find(([pattern]) => pattern.test(userAgent))?.[1] ?? fallback
	return `${find(browsers, "Navegador")} en ${find(systems, "otro sistema")}`
}
