import type { TenantResponse } from "@paquetes/api"
import { Alert, Button, Card, Checkbox, ErrorAlert, Field, Form, Input } from "@paquetes/componentes"
import { formValues, formatDate } from "@paquetes/utiles"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { tenants } from "../api"
import { PageHeader, QueryState } from "../components"
import { useUser } from "../user"

export default function OrganizationPage() {
	const tenantId = useUser().tenantId ?? ""
	const tenant = useQuery({ queryKey: ["tenant", tenantId], queryFn: () => tenants.get(tenantId) })

	return (
		<>
			<PageHeader
				title="Organización"
				description="Datos y política de seguridad de la organización activa."
			/>
			<QueryState query={tenant} />
			{tenant.data && <SettingsCard tenant={tenant.data} />}
		</>
	)
}

function SettingsCard({ tenant }: { tenant: TenantResponse }) {
	const queryClient = useQueryClient()
	const update = useMutation({
		mutationFn: (body: { name: string; requireMfa: boolean }) => tenants.update(tenant.id, body),
		onSuccess: () => {
			void queryClient.invalidateQueries({ queryKey: ["tenant", tenant.id] })
			void queryClient.invalidateQueries({ queryKey: ["my-tenants"] })
		},
	})

	return (
		<Card
			title={tenant.name}
			description={`${tenant.slug} · ${tenant.memberCount} miembros · creada el ${formatDate(tenant.createdAtUtc)}`}
		>
			<Form
				onSubmit={(event) => {
					const { name, requireMfa } = formValues(event, "name", "requireMfa")
					update.mutate({ name, requireMfa: requireMfa === "on" })
				}}
			>
				<Field label="Nombre">
					<Input name="name" defaultValue={tenant.name} minLength={2} maxLength={100} required />
				</Field>
				<Checkbox
					name="requireMfa"
					defaultChecked={tenant.requireMfa}
					label="Exigir verificación en dos pasos"
					hint="Los miembros sin app autenticadora ni passkey tendrán que configurarla al entrar."
				/>
				<ErrorAlert error={update.error} />
				{update.isSuccess && <Alert tone="success">Guardado.</Alert>}
				<Button type="submit" loading={update.isPending}>
					Guardar
				</Button>
			</Form>
		</Card>
	)
}
