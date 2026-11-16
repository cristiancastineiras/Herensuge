import { Alert, Badge, Button, Card, ErrorAlert, Field, Form, Select } from "@paquetes/componentes"
import { cn } from "@paquetes/utiles"
import { useMutation, useQuery } from "@tanstack/react-query"
import { useState } from "react"
import { access, tenants } from "../api"
import { PageHeader, QueryState } from "../components"
import { useUser } from "../user"

const stepLabels: Record<string, string> = {
	permission: "Permiso",
	session: "Sesión",
	membership: "Organización",
	feature: "Plan",
	grant: "Roles",
	condition: "Condición",
}

const reasonLabels: Record<string, string> = {
	granted: "Permitido",
	granted_if_condition: "Permitido si cumple la condición",
	unknown_permission: "El permiso no existe",
	not_member: "No pertenece a la organización",
	not_entitled: "El plan no incluye esa funcionalidad",
	no_role: "Ningún rol lo concede",
}

/** ¿Qué obtendría un miembro? La sesión se supone activa y las condiciones se muestran sin evaluarse. */
export default function SimulatorPage() {
	const tenantId = useUser().tenantId ?? ""
	const members = useQuery({ queryKey: ["members", tenantId], queryFn: () => tenants.members(tenantId) })
	const catalog = useQuery({
		queryKey: ["access-catalog", tenantId],
		queryFn: () => access.catalog(tenantId),
	})
	const [application, setApplication] = useState("")
	const simulate = useMutation({
		mutationFn: (body: { userId: string; resourceServer: string; permission: string }) =>
			access.simulate(tenantId, body),
	})
	const permissions = catalog.data?.find((candidate) => candidate.key === application)?.permissions ?? []

	return (
		<>
			<PageHeader
				title="Simulador"
				description="Comprueba qué decidiría Herensuge para un miembro, una aplicación y un permiso, y por qué."
			/>
			<QueryState query={members.error ? members : catalog} />
			{members.data && catalog.data && (
				<Card>
					<Form
						onSubmit={(event) => {
							event.preventDefault()
							const data = new FormData(event.currentTarget)
							simulate.mutate({
								userId: String(data.get("userId")),
								resourceServer: String(data.get("resourceServer")),
								permission: String(data.get("permission")),
							})
						}}
					>
						<div className="grid gap-4 sm:grid-cols-3">
							<Field label="Miembro">
								<Select name="userId" required defaultValue="">
									<option value="" disabled>
										Elige…
									</option>
									{members.data.map((member) => (
										<option key={member.userId} value={member.userId}>
											{member.displayName} ({member.email})
										</option>
									))}
								</Select>
							</Field>
							<Field label="Aplicación">
								<Select
									name="resourceServer"
									required
									value={application}
									onChange={(event) => setApplication(event.target.value)}
								>
									<option value="" disabled>
										Elige…
									</option>
									{catalog.data.map((candidate) => (
										<option key={candidate.key} value={candidate.key}>
											{candidate.name}
										</option>
									))}
								</Select>
							</Field>
							<Field label="Permiso">
								<Select name="permission" required defaultValue="" key={application}>
									<option value="" disabled>
										Elige…
									</option>
									{permissions.map((permission) => (
										<option key={permission.id} value={permission.key}>
											{permission.key}
										</option>
									))}
								</Select>
							</Field>
						</div>
						<Button type="submit" loading={simulate.isPending}>
							Simular
						</Button>
					</Form>
					<ErrorAlert error={simulate.error} />
					{simulate.data && (
						<div className="space-y-3">
							<Alert tone={simulate.data.decision ? "success" : "error"}>
								{reasonLabels[simulate.data.reason] ?? simulate.data.reason}
							</Alert>
							<ol className="space-y-1">
								{simulate.data.steps.map((step) => (
									<li key={step.name} className="flex items-center gap-3 text-sm">
										<Badge tone={step.passed ? "success" : "error"}>{step.passed ? "✓" : "✗"}</Badge>
										<span className="w-28 font-medium text-zinc-900">
											{stepLabels[step.name] ?? step.name}
										</span>
										<span className={cn(step.passed ? "text-zinc-600" : "text-red-700")}>{step.detail}</span>
									</li>
								))}
							</ol>
						</div>
					)}
				</Card>
			)}
		</>
	)
}
