import type { ApplicationCatalogResponse, RoleResponse } from "@paquetes/api"
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
import { access } from "../api"
import { PageHeader, QueryState, relationLabel, requirementLabels, roleLabels } from "../components"
import { useUser } from "../user"

export default function RolesPage() {
	const tenantId = useUser().tenantId ?? ""
	const catalog = useQuery({
		queryKey: ["access-catalog", tenantId],
		queryFn: () => access.catalog(tenantId),
	})

	return (
		<>
			<PageHeader
				title="Roles"
				description="Qué puede hacer cada persona en las aplicaciones de la organización."
			/>
			<QueryState query={catalog} />
			{catalog.data && <CustomRolesCard catalog={catalog.data} />}
			{catalog.data && <TemplatesCard catalog={catalog.data} />}
		</>
	)
}

/** null: cerrado; "new": rol nuevo; un RoleResponse: editándolo. */
type Editing = null | "new" | RoleResponse

function CustomRolesCard({ catalog }: { catalog: ApplicationCatalogResponse[] }) {
	const tenantId = useUser().tenantId ?? ""
	const queryClient = useQueryClient()
	const roles = useQuery({ queryKey: ["roles", tenantId], queryFn: () => access.roles(tenantId) })
	const [editing, setEditing] = useState<Editing>(null)
	const remove = useMutation({
		mutationFn: (roleId: string) => access.deleteRole(tenantId, roleId),
		onSuccess: () => queryClient.invalidateQueries({ queryKey: ["roles", tenantId] }),
	})

	return (
		<Card
			flush
			title="Roles de la organización"
			description="Combina permisos de varias aplicaciones. Solo puedes conceder permisos que tú tienes."
			actions={
				editing === null && (
					<Button variant="secondary" onClick={() => setEditing("new")}>
						Nuevo rol
					</Button>
				)
			}
		>
			<div className="px-5">
				<QueryState query={roles} />
				<ErrorAlert error={remove.error} />
			</div>
			{editing !== null && (
				<div className="border-y border-zinc-100 px-5 py-4">
					<RoleEditor
						key={editing === "new" ? "new" : editing.id}
						catalog={catalog}
						role={editing === "new" ? null : editing}
						onDone={() => setEditing(null)}
					/>
				</div>
			)}
			{roles.data?.length === 0 && editing === null && (
				<p className="px-5 pb-4 text-sm text-zinc-500">
					Aún no hay roles propios. Las plantillas de abajo ya se pueden asignar a los miembros.
				</p>
			)}
			{roles.data && roles.data.length > 0 && (
				<Table headers={["Rol", "Permisos", "Miembros", ""]}>
					{roles.data.map((role) => (
						<tr key={role.id}>
							<Td>
								<span className="font-medium text-zinc-900">{role.name}</span>
								{role.description && <span className="block text-xs text-zinc-500">{role.description}</span>}
							</Td>
							<Td>{role.permissionIds.length}</Td>
							<Td>{role.members}</Td>
							<Td className="space-x-2 text-right whitespace-nowrap">
								<Button variant="ghost" onClick={() => setEditing(role)}>
									Editar
								</Button>
								<ConfirmButton
									question={role.members > 0 ? `¿Quitarlo a ${role.members}?` : "¿Eliminar?"}
									confirmLabel="Eliminar"
									onConfirm={() => remove.mutate(role.id)}
								>
									Eliminar
								</ConfirmButton>
							</Td>
						</tr>
					))}
				</Table>
			)}
		</Card>
	)
}

function RoleEditor({
	catalog,
	role,
	onDone,
}: {
	catalog: ApplicationCatalogResponse[]
	role: RoleResponse | null
	onDone: () => void
}) {
	const tenantId = useUser().tenantId ?? ""
	const queryClient = useQueryClient()
	const save = useMutation({
		mutationFn: (body: { name: string; description: string | null; permissionIds: string[] }) =>
			role ? access.updateRole(tenantId, role.id, body) : access.createRole(tenantId, body),
		onSuccess: async () => {
			await queryClient.invalidateQueries({ queryKey: ["roles", tenantId] })
			onDone()
		},
	})
	const granted = new Set(role?.permissionIds)

	return (
		<Form
			onSubmit={(event) => {
				const permissionIds = new FormData(event.currentTarget).getAll("permission").map(String)
				const { name, description } = formValues(event, "name", "description")
				save.mutate({ name: name.trim(), description: description.trim() || null, permissionIds })
			}}
		>
			<div className="grid gap-4 sm:grid-cols-2">
				<Field label="Nombre">
					<Input name="name" required maxLength={100} defaultValue={role?.name} />
				</Field>
				<Field label="Descripción">
					<Input name="description" maxLength={500} defaultValue={role?.description ?? ""} />
				</Field>
			</div>
			{catalog.map((application) => (
				<fieldset key={application.key} className="space-y-2">
					<legend className="mb-2 text-sm font-semibold text-zinc-900">
						{application.name} <span className="font-mono text-xs text-zinc-400">{application.key}</span>
					</legend>
					<div className="grid gap-2 sm:grid-cols-2">
						{application.permissions.map((permission) => (
							<Checkbox
								key={permission.id}
								name="permission"
								value={permission.id}
								defaultChecked={granted.has(permission.id)}
								label={permission.key}
								hint={[
									permission.description,
									requirementLabels[permission.requires],
									permission.relation && relationLabel(permission.relation, permission.relationMode),
								]
									.filter(Boolean)
									.join(" · ")}
							/>
						))}
					</div>
				</fieldset>
			))}
			<ErrorAlert error={save.error} />
			<div className="flex gap-2">
				<Button type="submit" loading={save.isPending}>
					{role ? "Guardar" : "Crear rol"}
				</Button>
				<Button variant="ghost" onClick={onDone}>
					Cancelar
				</Button>
			</div>
		</Form>
	)
}

/** Roles que trae cada aplicación en su manifiesto: no se editan aquí. */
function TemplatesCard({ catalog }: { catalog: ApplicationCatalogResponse[] }) {
	const withTemplates = catalog.filter((application) => application.roles.length > 0)
	if (withTemplates.length === 0) {
		return null
	}

	return (
		<Card
			flush
			title="Plantillas de las aplicaciones"
			description="Las define cada aplicación. Las que indican un rol base se conceden solas a quien lo tiene."
		>
			<Table headers={["Plantilla", "Aplicación", "Permisos", "Se concede a"]}>
				{withTemplates.flatMap((application) =>
					application.roles.map((template) => (
						<tr key={template.id}>
							<Td>
								<span className="font-medium text-zinc-900">{template.name}</span>
								{template.description && (
									<span className="block text-xs text-zinc-500">{template.description}</span>
								)}
							</Td>
							<Td>{application.name}</Td>
							<Td className="font-mono text-xs">{template.permissionKeys.join(", ")}</Td>
							<Td className="space-x-1">
								{template.grantTo.length === 0
									? "—"
									: template.grantTo.map((base) => <Badge key={base}>{roleLabels[base] ?? base}</Badge>)}
							</Td>
						</tr>
					)),
				)}
			</Table>
		</Card>
	)
}
