import type { AccessManifest } from "@paquetes/api"
import {
	Alert,
	Badge,
	Button,
	Card,
	ErrorAlert,
	Field,
	Form,
	Table,
	Td,
	Textarea,
} from "@paquetes/componentes"
import { formValues, formatDateTime } from "@paquetes/utiles"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { useState } from "react"
import { platform } from "../api"
import { PageHeader, QueryState } from "../components"

const example = `{
  "resourceServer": "facturas",
  "name": "Facturación",
  "clientId": "facturas-app",
  "permissions": [
    { "key": "invoice:read" },
    { "key": "invoice:approve", "requires": "mfa" }
  ],
  "roles": [
    { "key": "finance", "name": "Finanzas", "permissions": ["invoice:*"], "grantTo": ["owner"] }
  ]
}`

export default function ApplicationsPage() {
	const servers = useQuery({ queryKey: ["resource-servers"], queryFn: platform.resourceServers })

	return (
		<>
			<PageHeader
				title="Aplicaciones"
				description="Permisos y roles que publica cada aplicación del SaaS en su manifiesto (herensuge.access.json)."
			/>
			<SyncManifestCard />
			<Card flush title="Publicadas">
				<div className="px-5">
					<QueryState query={servers} />
				</div>
				{servers.data?.length === 0 && (
					<p className="px-5 pb-4 text-sm text-zinc-500">Ninguna aplicación ha publicado su manifiesto.</p>
				)}
				{servers.data && servers.data.length > 0 && (
					<Table
						headers={["Aplicación", "Cliente", "Scope / audiencia", "Permisos", "Plantillas", "Versión"]}
					>
						{servers.data.map((server) => (
							<tr key={server.key}>
								<Td>
									<span className="font-medium text-zinc-900">{server.name}</span>
									<span className="block font-mono text-xs text-zinc-500">{server.key}</span>
								</Td>
								<Td className="font-mono text-xs">{server.clientId}</Td>
								<Td className="font-mono text-xs">
									{server.scope}
									<span className="block text-zinc-400">{server.audience}</span>
								</Td>
								<Td className="space-x-1 whitespace-nowrap">
									<span>{server.activePermissions}</span>
									{server.deprecatedPermissions > 0 && (
										<Badge tone="warning">{server.deprecatedPermissions} retirados</Badge>
									)}
									{!server.embedPermissions && <Badge>solo PDP</Badge>}
								</Td>
								<Td>{server.roleTemplates}</Td>
								<Td className="whitespace-nowrap">
									v{server.manifestVersion}
									<span className="block text-xs text-zinc-500">{formatDateTime(server.updatedAtUtc)}</span>
								</Td>
							</tr>
						))}
					</Table>
				)}
			</Card>
		</>
	)
}

/**
 * Lo normal es que la aplicación publique su manifiesto en el despliegue (PUT /access/v1/manifest con su
 * client_credentials). Esto es para darla de alta o corregirla a mano.
 */
function SyncManifestCard() {
	const queryClient = useQueryClient()
	const [invalidJson, setInvalidJson] = useState(false)
	const sync = useMutation({
		mutationFn: platform.syncManifest,
		onSuccess: () => queryClient.invalidateQueries({ queryKey: ["resource-servers"] }),
	})

	return (
		<Card
			title="Sincronizar un manifiesto"
			description="Pega el JSON. Los permisos que ya no aparezcan quedan retirados (no se borran) y dejan de concederse."
		>
			<Form
				onSubmit={(event) => {
					const { manifest } = formValues(event, "manifest")
					let parsed: AccessManifest
					try {
						parsed = JSON.parse(manifest) as AccessManifest
					} catch {
						setInvalidJson(true)
						return
					}

					setInvalidJson(false)
					sync.mutate(parsed)
				}}
			>
				<Field label="Manifiesto">
					<Textarea name="manifest" required rows={14} placeholder={example} spellCheck={false} />
				</Field>
				{invalidJson && <Alert tone="error">El texto no es JSON válido.</Alert>}
				<ErrorAlert error={sync.error} />
				{sync.isSuccess && (
					<Alert tone="success">
						{sync.data.resourceServer} sincronizada (versión {sync.data.manifestVersion}): scope{" "}
						<code className="font-mono">{sync.data.scope}</code>, audiencia{" "}
						<code className="font-mono">{sync.data.audience}</code>.
					</Alert>
				)}
				<Button type="submit" loading={sync.isPending}>
					Sincronizar
				</Button>
			</Form>
		</Card>
	)
}
