import type { ServiceAccountResponse } from "@paquetes/api"
import {
	Alert,
	Button,
	Card,
	ConfirmButton,
	ErrorAlert,
	Field,
	Input,
	Table,
	Td,
} from "@paquetes/componentes"
import { formValues, formatDate } from "@paquetes/utiles"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { useState } from "react"
import { serviceAccounts, subjectRoles } from "../api"
import { PageHeader, QueryState } from "../components"
import { useUser } from "../user"
import { RolesEditor } from "./RolesEditor"

export default function ServiceAccountsPage() {
	const tenantId = useUser().tenantId ?? ""
	const queryClient = useQueryClient()
	const list = useQuery({
		queryKey: ["service-accounts", tenantId],
		queryFn: () => serviceAccounts.list(tenantId),
	})
	const refresh = () => queryClient.invalidateQueries({ queryKey: ["service-accounts", tenantId] })
	const [editingRoles, setEditingRoles] = useState<ServiceAccountResponse | null>(null)
	// El secreto solo se puede ver una vez: al crear la cuenta o al rotarlo.
	const [secret, setSecret] = useState<{ clientId: string; value: string } | null>(null)

	const create = useMutation({
		mutationFn: (name: string) => serviceAccounts.create(tenantId, name),
		onSuccess: (created) => {
			setSecret({ clientId: created.clientId, value: created.clientSecret })
			void refresh()
		},
	})
	const rotate = useMutation({
		mutationFn: (account: ServiceAccountResponse) => serviceAccounts.rotateSecret(tenantId, account.id),
		onSuccess: (rotated, account) => setSecret({ clientId: account.clientId, value: rotated.clientSecret }),
	})
	const remove = useMutation({
		mutationFn: (accountId: string) => serviceAccounts.remove(tenantId, accountId),
		onSuccess: refresh,
	})

	return (
		<>
			<PageHeader
				title="Cuentas de servicio"
				description="Máquinas de la organización (integraciones, CI) que llaman a las APIs de las aplicaciones con client_credentials."
			/>
			{secret && (
				<Alert tone="warning">
					<p>
						Secreto de <strong className="font-mono">{secret.clientId}</strong>. Cópialo ahora, no se volverá
						a mostrar:
					</p>
					<code className="mt-2 block font-mono break-all text-zinc-900">{secret.value}</code>
				</Alert>
			)}
			<Card title="Nueva cuenta de servicio">
				<form
					className="flex flex-wrap items-end gap-2"
					onSubmit={(event) => {
						const form = event.currentTarget
						create.mutate(formValues(event, "name").name.trim(), { onSuccess: () => form.reset() })
					}}
				>
					<Field label="Nombre">
						<Input
							name="name"
							required
							maxLength={100}
							placeholder="p. ej. Integración ERP"
							className="w-72"
						/>
					</Field>
					<Button type="submit" loading={create.isPending}>
						Crear cuenta
					</Button>
				</form>
				<ErrorAlert error={create.error} />
				<p className="text-xs text-zinc-500">
					Pide un token con <code className="font-mono">grant_type=client_credentials</code> y el scope de una
					aplicación (<code className="font-mono">facturas.api</code>). Lleva los permisos de sus roles; las
					condiciones (MFA…) nunca se cumplen para una máquina.
				</p>
			</Card>
			<Card flush title="Cuentas">
				<div className="px-5">
					<QueryState query={list} />
					<ErrorAlert error={rotate.error ?? remove.error} />
				</div>
				{list.data?.length === 0 && (
					<p className="px-5 pb-4 text-sm text-zinc-500">Aún no hay cuentas de servicio.</p>
				)}
				{list.data && list.data.length > 0 && (
					<Table headers={["Cuenta", "Client id", "Roles", "Creada", ""]}>
						{list.data.map((account) => (
							<tr key={account.id}>
								<Td className="font-medium text-zinc-900">{account.name}</Td>
								<Td className="font-mono text-xs">{account.clientId}</Td>
								<Td>{account.roles}</Td>
								<Td className="whitespace-nowrap">{formatDate(account.createdAtUtc)}</Td>
								<Td className="space-x-1 text-right whitespace-nowrap">
									<Button variant="ghost" onClick={() => setEditingRoles(account)}>
										Roles
									</Button>
									<ConfirmButton
										question="El secreto actual dejará de valer."
										confirmLabel="Rotar"
										onConfirm={() => rotate.mutate(account)}
									>
										Rotar secreto
									</ConfirmButton>
									<ConfirmButton
										question="Sus tokens valen hasta que caducan (5 min)."
										confirmLabel="Eliminar"
										onConfirm={() => remove.mutate(account.id)}
									>
										Eliminar
									</ConfirmButton>
								</Td>
							</tr>
						))}
					</Table>
				)}
				{editingRoles && (
					<div className="border-t border-zinc-100 px-5 py-4">
						<RolesEditor
							key={editingRoles.id}
							id={`client:${editingRoles.id}`}
							title={`Roles de ${editingRoles.name}`}
							roles={subjectRoles.serviceAccount(tenantId, editingRoles.id)}
							onDone={() => {
								setEditingRoles(null)
								void refresh()
							}}
						/>
					</div>
				)}
			</Card>
		</>
	)
}
