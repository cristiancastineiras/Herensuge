import type { ClientResponse, CreateClientRequest } from "@paquetes/api"
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
	Table,
	Td,
	Textarea,
} from "@paquetes/componentes"
import { formValues } from "@paquetes/utiles"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { useState } from "react"
import { platform } from "../api"
import { PageHeader, QueryState } from "../components"

const defaultScopes = "openid profile email offline_access herensuge.api"
const lines = (value: string) => value.split(/\s+/).filter(Boolean)

/** Editor abierto: un cliente nuevo o uno existente. */
type Editing = { mode: "create" } | { mode: "edit"; client: ClientResponse } | null

export default function ClientsPage() {
	const queryClient = useQueryClient()
	const clients = useQuery({ queryKey: ["clients"], queryFn: platform.clients })
	const refresh = () => queryClient.invalidateQueries({ queryKey: ["clients"] })
	const [editing, setEditing] = useState<Editing>(null)
	// El secreto solo se puede ver una vez: al crear el cliente o al rotarlo.
	const [secret, setSecret] = useState<{ clientId: string; value: string } | null>(null)

	const rotate = useMutation({
		mutationFn: platform.rotateSecret,
		onSuccess: (result, clientId) => setSecret({ clientId, value: result.clientSecret }),
	})
	const remove = useMutation({ mutationFn: platform.deleteClient, onSuccess: refresh })

	return (
		<>
			<PageHeader
				title="Clientes OIDC"
				description="Aplicaciones del SaaS que inician sesión con Herensuge."
				actions={
					editing === null && <Button onClick={() => setEditing({ mode: "create" })}>Nuevo cliente</Button>
				}
			/>

			{secret && (
				<Alert tone="warning">
					<p>
						Secreto de <strong>{secret.clientId}</strong>. Cópialo ahora, no se volverá a mostrar:
					</p>
					<code className="mt-2 block font-mono break-all text-zinc-900">{secret.value}</code>
				</Alert>
			)}

			{editing && (
				<ClientForm
					key={editing.mode === "edit" ? editing.client.clientId : "new"}
					client={editing.mode === "edit" ? editing.client : null}
					onClose={() => setEditing(null)}
					onSaved={(newSecret) => {
						setEditing(null)
						setSecret(newSecret)
						void refresh()
					}}
				/>
			)}

			<Card flush title="Clientes">
				<div className="px-5">
					<QueryState query={clients} />
					<ErrorAlert error={rotate.error ?? remove.error} />
				</div>
				{clients.data && (
					<Table headers={["Cliente", "Tipo", "Redirect URIs", ""]}>
						{clients.data.map((client) => (
							<tr key={client.clientId}>
								<Td>
									<span className="font-medium text-zinc-900">{client.displayName}</span>{" "}
									{client.isSystem && <Badge>Sistema</Badge>}
									<span className="block font-mono text-xs text-zinc-500">{client.clientId}</span>
								</Td>
								<Td>{client.isConfidential ? "Confidencial" : "Público"}</Td>
								<Td className="font-mono text-xs">
									{client.redirectUris.map((uri) => (
										<span key={uri} className="block break-all">
											{uri}
										</span>
									))}
								</Td>
								<Td>
									{!client.isSystem && (
										<div className="flex flex-wrap justify-end gap-2">
											<Button variant="ghost" onClick={() => setEditing({ mode: "edit", client })}>
												Editar
											</Button>
											{client.isConfidential && (
												<ConfirmButton
													question="El secreto actual dejará de valer."
													confirmLabel="Rotar"
													onConfirm={() => rotate.mutate(client.clientId)}
												>
													Rotar secreto
												</ConfirmButton>
											)}
											<ConfirmButton confirmLabel="Eliminar" onConfirm={() => remove.mutate(client.clientId)}>
												Eliminar
											</ConfirmButton>
										</div>
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

type ClientFormProps = {
	client: ClientResponse | null
	onClose: () => void
	onSaved: (secret: { clientId: string; value: string } | null) => void
}

function ClientForm({ client, onClose, onSaved }: ClientFormProps) {
	const save = useMutation({
		mutationFn: async (input: CreateClientRequest) => {
			if (client) {
				await platform.updateClient(input)
				return null
			}

			const created = await platform.createClient(input)
			return created.clientSecret ? { clientId: created.clientId, value: created.clientSecret } : null
		},
		onSuccess: onSaved,
	})

	return (
		<Card title={client ? `Editar ${client.displayName}` : "Nuevo cliente"}>
			<Form
				onSubmit={(event) => {
					const values = formValues(
						event,
						"clientId",
						"displayName",
						"isConfidential",
						"redirectUris",
						"postLogoutRedirectUris",
						"scopes",
					)
					save.mutate({
						clientId: client?.clientId ?? values.clientId,
						displayName: values.displayName,
						isConfidential: client?.isConfidential ?? values.isConfidential === "on",
						redirectUris: lines(values.redirectUris),
						postLogoutRedirectUris: lines(values.postLogoutRedirectUris),
						scopes: lines(values.scopes),
					})
				}}
			>
				<div className="grid gap-4 sm:grid-cols-2">
					<Field label="Client id" hint="Minúsculas, números y guiones. No se puede cambiar.">
						<Input
							name="clientId"
							defaultValue={client?.clientId}
							disabled={client !== null}
							pattern="[a-z0-9][a-z0-9\-]*"
							minLength={3}
							maxLength={100}
							required
						/>
					</Field>
					<Field label="Nombre visible">
						<Input name="displayName" defaultValue={client?.displayName} maxLength={100} required />
					</Field>
				</div>
				{!client && (
					<Checkbox
						name="isConfidential"
						defaultChecked
						label="Cliente confidencial"
						hint="Tiene secreto (una BFF o un backend). Desmárcalo para una app pública con PKCE."
					/>
				)}
				<Field label="Redirect URIs" hint="Una por línea. HTTPS absoluto (http solo para localhost).">
					<Textarea name="redirectUris" defaultValue={client?.redirectUris.join("\n")} required />
				</Field>
				<Field label="URIs tras cerrar sesión" hint="Una por línea. Opcional.">
					<Textarea name="postLogoutRedirectUris" defaultValue={client?.postLogoutRedirectUris.join("\n")} />
				</Field>
				<Field label="Scopes" hint="Separados por espacios.">
					<Input
						name="scopes"
						defaultValue={client?.scopes.join(" ") ?? defaultScopes}
						className="font-mono"
						required
					/>
				</Field>
				<ErrorAlert error={save.error} />
				<div className="flex gap-2">
					<Button type="submit" loading={save.isPending}>
						{client ? "Guardar" : "Crear cliente"}
					</Button>
					<Button variant="ghost" onClick={onClose}>
						Cancelar
					</Button>
				</div>
			</Form>
		</Card>
	)
}
