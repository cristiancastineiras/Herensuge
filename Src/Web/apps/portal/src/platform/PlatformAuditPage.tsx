import { audit } from "../api"
import { AuditLog, PageHeader } from "../components"

export default function PlatformAuditPage() {
	return (
		<>
			<PageHeader title="Auditoría" description="Eventos de todas las organizaciones y usuarios." />
			<AuditLog id="platform" load={audit.platform} />
		</>
	)
}
