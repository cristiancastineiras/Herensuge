import type { RoleGrant } from "@paquetes/api"
import { Button, Checkbox, ErrorAlert, Form, Input, Loading } from "@paquetes/componentes"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { access, type SubjectRoles } from "../api"
import { roleLabels } from "../components"
import { useUser } from "../user"

const grantKey = (grant: { roleId: string | null; roleTemplateId: string | null }) =>
	grant.roleId ? `role:${grant.roleId}` : `template:${grant.roleTemplateId}`

/** Fecha de caducidad (yyyy-mm-dd, hora local) → fin de ese día en UTC. */
const endOfDay = (date: string) => (date ? new Date(`${date}T23:59:59`).toISOString() : null)

type RolesEditorProps = {
	/** Distingue la caché: ["subject-roles", id]. */
	id: string
	title: string
	roles: SubjectRoles
	/** Rol base del miembro: sus plantillas por rol base se muestran fijas. Grupos y cuentas no tienen. */
	baseRole?: string
	onDone: () => void
}

/**
 * Roles de un miembro, un grupo o una cuenta de servicio: plantillas de las aplicaciones y roles propios, cada uno
 * con caducidad opcional. Solo se puede conceder lo que uno tiene (la API responde Access.CannotGrant).
 */
export function RolesEditor({ id, title, roles, baseRole, onDone }: RolesEditorProps) {
	const tenantId = useUser().tenantId ?? ""
	const catalog = useQuery({
		queryKey: ["access-catalog", tenantId],
		queryFn: () => access.catalog(tenantId),
	})
	const customRoles = useQuery({ queryKey: ["roles", tenantId], queryFn: () => access.roles(tenantId) })
	const current = useQuery({ queryKey: ["subject-roles", id], queryFn: roles.load })
	const queryClient = useQueryClient()
	const save = useMutation({
		mutationFn: roles.save,
		onSuccess: async () => {
			await queryClient.invalidateQueries({ queryKey: ["subject-roles", id] })
			await queryClient.invalidateQueries({ queryKey: ["roles", tenantId] })
			onDone()
		},
	})

	if (!catalog.data || !customRoles.data || !current.data) {
		const error = catalog.error ?? customRoles.error ?? current.error
		return error ? <ErrorAlert error={error} /> : <Loading />
	}

	const held = new Map(current.data.map((grant) => [grantKey(grant), grant.expiresAtUtc?.slice(0, 10) ?? ""]))
	const options = [
		...catalog.data.flatMap((application) =>
			application.roles.map((template) => ({
				key: grantKey({ roleId: null, roleTemplateId: template.id }),
				label: template.name,
				group: application.name,
				byBaseRole: baseRole !== undefined && template.grantTo.includes(baseRole),
			})),
		),
		...customRoles.data.map((role) => ({
			key: grantKey({ roleId: role.id, roleTemplateId: null }),
			label: role.name,
			group: "Roles de la organización",
			byBaseRole: false,
		})),
	]

	return (
		<Form
			onSubmit={(event) => {
				event.preventDefault()
				const data = new FormData(event.currentTarget)
				const grants: RoleGrant[] = data.getAll("grant").map((value) => {
					const key = String(value)
					const [kind, grantId] = key.split(":") as [string, string]
					return {
						roleId: kind === "role" ? grantId : null,
						roleTemplateId: kind === "template" ? grantId : null,
						expiresAtUtc: endOfDay(String(data.get(`expires:${key}`) ?? "")),
					}
				})
				save.mutate(grants)
			}}
		>
			<p className="text-sm font-semibold text-zinc-900">{title}</p>
			{options.length === 0 && (
				<p className="text-sm text-zinc-500">Todavía no hay plantillas ni roles que asignar.</p>
			)}
			<ul className="divide-y divide-zinc-100">
				{options.map((option) => (
					<li key={option.key} className="flex flex-wrap items-center justify-between gap-3 py-2">
						{option.byBaseRole ? (
							<Checkbox
								checked
								readOnly
								disabled
								label={option.label}
								hint={`${option.group} · por su rol base (${roleLabels[baseRole ?? ""] ?? baseRole})`}
							/>
						) : (
							<>
								<Checkbox
									name="grant"
									value={option.key}
									defaultChecked={held.has(option.key)}
									label={option.label}
									hint={option.group}
								/>
								<span className="flex items-center gap-2 text-xs text-zinc-500">
									<span aria-hidden="true">Caduca</span>
									<Input
										type="date"
										name={`expires:${option.key}`}
										aria-label={`Caducidad de ${option.label}`}
										defaultValue={held.get(option.key) ?? ""}
										className="w-40 py-1"
									/>
								</span>
							</>
						)}
					</li>
				))}
			</ul>
			<ErrorAlert error={save.error} />
			<div className="flex gap-2">
				<Button type="submit" loading={save.isPending}>
					Guardar roles
				</Button>
				<Button variant="ghost" onClick={onDone}>
					Cancelar
				</Button>
			</div>
		</Form>
	)
}
