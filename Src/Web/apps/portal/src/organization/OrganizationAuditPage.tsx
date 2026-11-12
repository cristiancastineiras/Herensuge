import { useQuery } from "@tanstack/react-query"
import { audit, tenants } from "../api"
import { AuditLog, PageHeader } from "../components"
import { useUser } from "../user"

export default function OrganizationAuditPage() {
	const tenantId = useUser().tenantId ?? ""
	// Para mostrar nombres en lugar de ids (misma consulta que la página Miembros, así que suele estar en caché).
	const members = useQuery({ queryKey: ["members", tenantId], queryFn: () => tenants.members(tenantId) })
	const names = Object.fromEntries((members.data ?? []).map((member) => [member.userId, member.displayName]))

	return (
		<>
			<PageHeader title="Auditoría" description="Todo lo que ha pasado en la organización." />
			<AuditLog id={tenantId} load={(query) => audit.tenant(tenantId, query)} names={names} />
		</>
	)
}
