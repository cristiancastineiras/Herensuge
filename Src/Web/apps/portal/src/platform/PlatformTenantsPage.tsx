import { Badge, Button, Card, ConfirmButton, ErrorAlert, Input, Table, Td } from "@paquetes/componentes"
import { formValues, formatDate } from "@paquetes/utiles"
import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { useState } from "react"
import { platform } from "../api"
import { PageHeader, QueryState } from "../components"

const pageSize = 25

export default function PlatformTenantsPage() {
	const queryClient = useQueryClient()
	const [search, setSearch] = useState("")
	const [page, setPage] = useState(1)
	const list = useQuery({
		queryKey: ["platform-tenants", search, page],
		queryFn: () => platform.tenants({ search, page, pageSize }),
		placeholderData: keepPreviousData,
	})
	const setStatus = useMutation({
		mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) =>
			platform.setTenantStatus(id, isActive),
		onSuccess: () => queryClient.invalidateQueries({ queryKey: ["platform-tenants"] }),
	})
	const pages = list.data ? Math.max(1, Math.ceil(list.data.totalCount / pageSize)) : 1

	return (
		<>
			<PageHeader title="Organizaciones" description="Todas las organizaciones de la plataforma." />
			<Card
				flush
				title={
					list.data
						? `${list.data.totalCount} ${list.data.totalCount === 1 ? "organización" : "organizaciones"}`
						: "Organizaciones"
				}
				actions={
					<form
						className="flex gap-2"
						onSubmit={(event) => {
							setSearch(formValues(event, "search").search.trim())
							setPage(1)
						}}
					>
						<Input
							name="search"
							type="search"
							placeholder="Buscar por nombre o slug"
							aria-label="Buscar"
							className="w-56 py-1.5"
						/>
						<Button type="submit" variant="secondary">
							Buscar
						</Button>
					</form>
				}
			>
				<div className="px-5">
					<QueryState query={list} />
					<ErrorAlert error={setStatus.error} />
				</div>
				{list.data && (
					<Table headers={["Organización", "Miembros", "MFA", "Creada", "Estado", ""]}>
						{list.data.items.map((tenant) => (
							<tr key={tenant.id}>
								<Td>
									<span className="font-medium text-zinc-900">{tenant.name}</span>
									<span className="block text-xs text-zinc-500">{tenant.slug}</span>
								</Td>
								<Td>{tenant.memberCount}</Td>
								<Td>{tenant.requireMfa ? "Obligatorio" : "Opcional"}</Td>
								<Td className="whitespace-nowrap">{formatDate(tenant.createdAtUtc)}</Td>
								<Td>
									{tenant.isActive ? (
										<Badge tone="success">Activa</Badge>
									) : (
										<Badge tone="error">Desactivada</Badge>
									)}
								</Td>
								<Td className="text-right">
									{tenant.isActive ? (
										<ConfirmButton
											question="Sus miembros no podrán entrar."
											confirmLabel="Desactivar"
											onConfirm={() => setStatus.mutate({ id: tenant.id, isActive: false })}
										>
											Desactivar
										</ConfirmButton>
									) : (
										<Button
											variant="secondary"
											onClick={() => setStatus.mutate({ id: tenant.id, isActive: true })}
										>
											Activar
										</Button>
									)}
								</Td>
							</tr>
						))}
					</Table>
				)}
				{pages > 1 && (
					<div className="flex items-center justify-between border-t border-zinc-100 px-5 py-3 text-sm text-zinc-600">
						<span>
							Página {page} de {pages}
						</span>
						<div className="flex gap-2">
							<Button variant="secondary" disabled={page <= 1} onClick={() => setPage(page - 1)}>
								Anterior
							</Button>
							<Button variant="secondary" disabled={page >= pages} onClick={() => setPage(page + 1)}>
								Siguiente
							</Button>
						</div>
					</div>
				)}
			</Card>
		</>
	)
}
