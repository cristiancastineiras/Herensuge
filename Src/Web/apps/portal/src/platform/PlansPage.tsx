import type { FeatureResponse, PlanResponse } from "@paquetes/api"
import {
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
} from "@paquetes/componentes"
import { formValues } from "@paquetes/utiles"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { useState } from "react"
import { platform } from "../api"
import { PageHeader, QueryState } from "../components"

/** Editor abierto: un plan nuevo o uno existente. */
type Editing = { mode: "create" } | { mode: "edit"; plan: PlanResponse } | null

export default function PlansPage() {
	const queryClient = useQueryClient()
	const plans = useQuery({ queryKey: ["plans"], queryFn: platform.plans })
	const features = useQuery({ queryKey: ["features"], queryFn: platform.features })
	const [editing, setEditing] = useState<Editing>(null)
	const refresh = () => queryClient.invalidateQueries({ queryKey: ["plans"] })
	const setDefault = useMutation({ mutationFn: platform.setDefaultPlan, onSuccess: refresh })
	const remove = useMutation({ mutationFn: platform.deletePlan, onSuccess: refresh })

	return (
		<>
			<PageHeader
				title="Planes"
				description="Conjuntos de funcionalidades de las aplicaciones. Sin planes, todo está incluido; con planes, cada organización tiene el suyo o el plan por defecto."
				actions={
					editing === null && <Button onClick={() => setEditing({ mode: "create" })}>Nuevo plan</Button>
				}
			/>
			{editing && features.data && (
				<PlanEditor
					key={editing.mode === "edit" ? editing.plan.key : "new"}
					plan={editing.mode === "edit" ? editing.plan : null}
					features={features.data}
					onDone={() => {
						setEditing(null)
						void refresh()
					}}
				/>
			)}
			<Card flush title="Planes">
				<div className="px-5">
					<QueryState query={plans.error ? plans : features} />
					<ErrorAlert error={setDefault.error ?? remove.error} />
				</div>
				{plans.data?.length === 0 && (
					<p className="px-5 pb-4 text-sm text-zinc-500">
						No hay planes: todas las organizaciones tienen todas las funcionalidades.
					</p>
				)}
				{plans.data && plans.data.length > 0 && (
					<Table headers={["Plan", "Funcionalidades", "Organizaciones", ""]}>
						{plans.data.map((plan) => (
							<tr key={plan.key}>
								<Td>
									<span className="font-medium text-zinc-900">{plan.name}</span>{" "}
									{plan.isDefault && <Badge tone="success">Por defecto</Badge>}
									<span className="block font-mono text-xs text-zinc-500">{plan.key}</span>
								</Td>
								<Td className="font-mono text-xs">{plan.features.join(", ") || "—"}</Td>
								<Td>{plan.tenants}</Td>
								<Td className="space-x-1 text-right whitespace-nowrap">
									{!plan.isDefault && (
										<Button variant="ghost" onClick={() => setDefault.mutate(plan.key)}>
											Marcar por defecto
										</Button>
									)}
									<Button variant="ghost" onClick={() => setEditing({ mode: "edit", plan })}>
										Editar
									</Button>
									<ConfirmButton
										question="Sus organizaciones pasan al plan por defecto."
										confirmLabel="Eliminar"
										onConfirm={() => remove.mutate(plan.key)}
									>
										Eliminar
									</ConfirmButton>
								</Td>
							</tr>
						))}
					</Table>
				)}
			</Card>
		</>
	)
}

function PlanEditor({
	plan,
	features,
	onDone,
}: {
	plan: PlanResponse | null
	features: FeatureResponse[]
	onDone: () => void
}) {
	const save = useMutation({
		mutationFn: (body: { key: string; name: string; features: string[] }) =>
			plan ? platform.updatePlan(body) : platform.createPlan(body),
		onSuccess: onDone,
	})
	const included = new Set(plan?.features)
	const byApplication = new Map<string, FeatureResponse[]>()
	for (const feature of features) {
		byApplication.set(feature.resourceServerName, [
			...(byApplication.get(feature.resourceServerName) ?? []),
			feature,
		])
	}

	return (
		<Card title={plan ? `Editar ${plan.name}` : "Nuevo plan"}>
			<Form
				onSubmit={(event) => {
					const selected = new FormData(event.currentTarget).getAll("feature").map(String)
					const { key, name } = formValues(event, "key", "name")
					save.mutate({ key: plan?.key ?? key.trim(), name: name.trim(), features: selected })
				}}
			>
				<div className="grid gap-4 sm:grid-cols-2">
					<Field label="Clave" hint="Minúsculas, números y guiones; no se puede cambiar.">
						<Input
							name="key"
							required
							pattern="[a-z][a-z0-9-]*"
							maxLength={50}
							defaultValue={plan?.key}
							disabled={!!plan}
						/>
					</Field>
					<Field label="Nombre">
						<Input name="name" required maxLength={100} defaultValue={plan?.name} />
					</Field>
				</div>
				{features.length === 0 && (
					<p className="text-sm text-zinc-500">
						Ninguna aplicación declara funcionalidades (<code className="font-mono">features</code> en su
						manifiesto).
					</p>
				)}
				{[...byApplication].map(([application, appFeatures]) => (
					<fieldset key={application} className="space-y-2">
						<legend className="mb-2 text-sm font-semibold text-zinc-900">{application}</legend>
						<div className="grid gap-2 sm:grid-cols-2">
							{appFeatures.map((feature) => {
								const value = `${feature.resourceServer}/${feature.key}`
								return (
									<Checkbox
										key={value}
										name="feature"
										value={value}
										defaultChecked={included.has(value)}
										label={feature.name}
										hint={value}
									/>
								)
							})}
						</div>
					</fieldset>
				))}
				<ErrorAlert error={save.error} />
				<div className="flex gap-2">
					<Button type="submit" loading={save.isPending}>
						{plan ? "Guardar" : "Crear plan"}
					</Button>
					<Button variant="ghost" onClick={onDone}>
						Cancelar
					</Button>
				</div>
			</Form>
		</Card>
	)
}
