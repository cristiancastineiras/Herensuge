import type { GroupResponse } from "@paquetes/api"
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
	Loading,
	Table,
	Td,
} from "@paquetes/componentes"
import { formValues } from "@paquetes/utiles"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { useState } from "react"
import { groups, subjectRoles, tenants } from "../api"
import { PageHeader, QueryState } from "../components"
import { useUser } from "../user"
import { RolesEditor } from "./RolesEditor"

/** Lo que se edita de un grupo: datos, miembros o roles. */
type Editing = { group: GroupResponse; part: "details" | "members" | "roles" } | null

export default function GroupsPage() {
	const tenantId = useUser().tenantId ?? ""
	const queryClient = useQueryClient()
	const list = useQuery({ queryKey: ["groups", tenantId], queryFn: () => groups.list(tenantId) })
	const [editing, setEditing] = useState<Editing>(null)
	const refresh = () => queryClient.invalidateQueries({ queryKey: ["groups", tenantId] })
	const remove = useMutation({
		mutationFn: (groupId: string) => groups.remove(tenantId, groupId),
		onSuccess: refresh,
	})
	const done = () => {
		setEditing(null)
		void refresh()
	}

	return (
		<>
			<PageHeader
				title="Grupos"
				description="Personas que tienen los mismos roles. Añadir a alguien le da todos los roles del grupo."
			/>
			<CreateGroupCard onCreated={refresh} />
			<Card flush title="Grupos de la organización">
				<div className="px-5">
					<QueryState query={list} />
					<ErrorAlert error={remove.error} />
				</div>
				{list.data?.length === 0 && <p className="px-5 pb-4 text-sm text-zinc-500">Aún no hay grupos.</p>}
				{list.data && list.data.length > 0 && (
					<Table headers={["Grupo", "Miembros", "Roles", ""]}>
						{list.data.map((group) => (
							<tr key={group.id}>
								<Td>
									<span className="font-medium text-zinc-900">{group.name}</span>
									{group.description && (
										<span className="block text-xs text-zinc-500">{group.description}</span>
									)}
								</Td>
								<Td>{group.memberUserIds.length}</Td>
								<Td>{group.roles}</Td>
								<Td className="space-x-1 text-right whitespace-nowrap">
									<Button variant="ghost" onClick={() => setEditing({ group, part: "members" })}>
										Miembros
									</Button>
									<Button variant="ghost" onClick={() => setEditing({ group, part: "roles" })}>
										Roles
									</Button>
									<Button variant="ghost" onClick={() => setEditing({ group, part: "details" })}>
										Editar
									</Button>
									<ConfirmButton
										question="¿Eliminar el grupo y sus roles?"
										confirmLabel="Eliminar"
										onConfirm={() => remove.mutate(group.id)}
									>
										Eliminar
									</ConfirmButton>
								</Td>
							</tr>
						))}
					</Table>
				)}
				{editing && (
					<div className="border-t border-zinc-100 px-5 py-4" key={`${editing.group.id}:${editing.part}`}>
						{editing.part === "details" && <GroupDetails group={editing.group} onDone={done} />}
						{editing.part === "members" && <GroupMembers group={editing.group} onDone={done} />}
						{editing.part === "roles" && (
							<RolesEditor
								id={`group:${editing.group.id}`}
								title={`Roles del grupo ${editing.group.name}`}
								roles={subjectRoles.group(tenantId, editing.group.id)}
								onDone={done}
							/>
						)}
					</div>
				)}
			</Card>
		</>
	)
}

function CreateGroupCard({ onCreated }: { onCreated: () => void }) {
	const tenantId = useUser().tenantId ?? ""
	const create = useMutation({
		mutationFn: (body: { name: string; description: string | null }) => groups.create(tenantId, body),
		onSuccess: onCreated,
	})

	return (
		<Card title="Nuevo grupo">
			<form
				className="flex flex-wrap items-end gap-2"
				onSubmit={(event) => {
					const form = event.currentTarget
					const { name, description } = formValues(event, "name", "description")
					create.mutate(
						{ name: name.trim(), description: description.trim() || null },
						{ onSuccess: () => form.reset() },
					)
				}}
			>
				<Field label="Nombre">
					<Input name="name" required maxLength={100} className="w-56" />
				</Field>
				<Field label="Descripción">
					<Input name="description" maxLength={500} className="w-72" />
				</Field>
				<Button type="submit" loading={create.isPending}>
					Crear grupo
				</Button>
			</form>
			<ErrorAlert error={create.error} />
		</Card>
	)
}

function GroupDetails({ group, onDone }: { group: GroupResponse; onDone: () => void }) {
	const tenantId = useUser().tenantId ?? ""
	const update = useMutation({
		mutationFn: (body: { name: string; description: string | null }) =>
			groups.update(tenantId, group.id, body),
		onSuccess: onDone,
	})

	return (
		<Form
			onSubmit={(event) => {
				const { name, description } = formValues(event, "name", "description")
				update.mutate({ name: name.trim(), description: description.trim() || null })
			}}
		>
			<div className="grid gap-4 sm:grid-cols-2">
				<Field label="Nombre">
					<Input name="name" required maxLength={100} defaultValue={group.name} />
				</Field>
				<Field label="Descripción">
					<Input name="description" maxLength={500} defaultValue={group.description ?? ""} />
				</Field>
			</div>
			<ErrorAlert error={update.error} />
			<div className="flex gap-2">
				<Button type="submit" loading={update.isPending}>
					Guardar
				</Button>
				<Button variant="ghost" onClick={onDone}>
					Cancelar
				</Button>
			</div>
		</Form>
	)
}

/** Añadir exige tener todos los permisos del grupo (Access.CannotGrant); quitar no exige nada. */
function GroupMembers({ group, onDone }: { group: GroupResponse; onDone: () => void }) {
	const tenantId = useUser().tenantId ?? ""
	const members = useQuery({ queryKey: ["members", tenantId], queryFn: () => tenants.members(tenantId) })
	const save = useMutation({
		mutationFn: (userIds: string[]) => groups.setMembers(tenantId, group.id, userIds),
		onSuccess: onDone,
	})

	if (!members.data) {
		return members.error ? <ErrorAlert error={members.error} /> : <Loading />
	}

	const current = new Set(group.memberUserIds)
	return (
		<Form
			onSubmit={(event) => {
				event.preventDefault()
				save.mutate(new FormData(event.currentTarget).getAll("member").map(String))
			}}
		>
			<p className="text-sm font-semibold text-zinc-900">
				Miembros de {group.name} <Badge>{current.size}</Badge>
			</p>
			<div className="grid gap-2 sm:grid-cols-2">
				{members.data.map((member) => (
					<Checkbox
						key={member.userId}
						name="member"
						value={member.userId}
						defaultChecked={current.has(member.userId)}
						label={member.displayName}
						hint={member.email}
					/>
				))}
			</div>
			<ErrorAlert error={save.error} />
			<div className="flex gap-2">
				<Button type="submit" loading={save.isPending}>
					Guardar miembros
				</Button>
				<Button variant="ghost" onClick={onDone}>
					Cancelar
				</Button>
			</div>
		</Form>
	)
}
