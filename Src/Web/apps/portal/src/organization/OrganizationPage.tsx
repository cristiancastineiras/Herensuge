import type { TenantResponse } from "@paquetes/api"
import {
	Alert,
	Badge,
	Button,
	Card,
	Checkbox,
	ErrorAlert,
	Field,
	Form,
	Input,
	Textarea,
} from "@paquetes/componentes"
import { formValues, formatDate } from "@paquetes/utiles"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { tenants } from "../api"
import { PageHeader, QueryState } from "../components"
import { useUser } from "../user"

const lines = (value: string) => value.split(/\s+/).filter(Boolean)

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
			<PlanCard />
		</>
	)
}

function SettingsCard({ tenant }: { tenant: TenantResponse }) {
	const queryClient = useQueryClient()
	const update = useMutation({
		mutationFn: (body: { name: string; requireMfa: boolean; trustedNetworks: string[] }) =>
			tenants.update(tenant.id, body),
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
					const { name, requireMfa, trustedNetworks } = formValues(
						event,
						"name",
						"requireMfa",
						"trustedNetworks",
					)
					update.mutate({ name, requireMfa: requireMfa === "on", trustedNetworks: lines(trustedNetworks) })
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
				<Field
					label="Redes de confianza"
					hint="Rangos públicos en CIDR, uno por línea (p. ej. 203.0.113.0/24). Los permisos que exigen red de confianza solo se conceden desde ellas; sin rangos, desde cualquier IP pública."
				>
					<Textarea
						name="trustedNetworks"
						rows={3}
						defaultValue={tenant.trustedNetworks.join("\n")}
						spellCheck={false}
					/>
				</Field>
				<ErrorAlert error={update.error} />
				{update.isSuccess && <Alert tone="success">Guardado.</Alert>}
				<Button type="submit" loading={update.isPending}>
					Guardar
				</Button>
			</Form>
		</Card>
	)
}

/** Plan contratado: solo lectura (lo asigna la plataforma). */
function PlanCard() {
	const tenantId = useUser().tenantId ?? ""
	const plan = useQuery({ queryKey: ["tenant-plan", tenantId], queryFn: () => tenants.plan(tenantId) })
	if (!plan.data) {
		return <QueryState query={plan} />
	}

	const { plan: name, isDefault, applications } = plan.data
	return (
		<Card
			title="Plan"
			description={
				name
					? `${name}${isDefault ? " (plan por defecto)" : ""}. Lo asigna el administrador de la plataforma.`
					: "Sin planes en esta instalación: todas las funcionalidades están incluidas."
			}
		>
			{applications.length === 0 && (
				<p className="text-sm text-zinc-500">Ninguna aplicación declara funcionalidades contratables.</p>
			)}
			{applications.map((application) => (
				<div key={application.key}>
					<p className="mb-1 text-sm font-medium text-zinc-900">{application.name}</p>
					<div className="flex flex-wrap gap-1">
						{application.features.map((feature) => (
							<Badge key={feature.key} tone={feature.included ? "success" : "neutral"}>
								{feature.included ? "✓" : "—"} {feature.name}
							</Badge>
						))}
					</div>
				</div>
			))}
		</Card>
	)
}
