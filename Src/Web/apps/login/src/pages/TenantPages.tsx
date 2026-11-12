import { Badge, Button, ErrorAlert, Field, Form, Input, Loading } from "@paquetes/componentes"
import { formValues } from "@paquetes/utiles"
import { useMutation, useQuery } from "@tanstack/react-query"
import { Link } from "react-router"
import { auth } from "../api"
import { AuthLayout, linkClass } from "../AuthLayout"
import { continueTo, useReturnUrl, withReturnUrl } from "../navigation"

/** Rol que envía la API ("owner", "admin", "member") → texto. */
export const roleLabels: Record<string, string> = {
	owner: "Propietario",
	admin: "Administrador",
	member: "Miembro",
}

/** El usuario pertenece a varias organizaciones: elige con cuál entra. */
export function SelectTenantPage() {
	const returnUrl = useReturnUrl()
	const tenants = useQuery({ queryKey: ["tenants"], queryFn: auth.tenants })
	const select = useMutation({ mutationFn: auth.selectTenant, onSuccess: () => continueTo(returnUrl) })

	return (
		<AuthLayout
			title="Elige una organización"
			footer={
				<Link className={linkClass} to={withReturnUrl("/onboarding", returnUrl)}>
					Crear una organización nueva
				</Link>
			}
		>
			{tenants.isPending && <Loading />}
			<ErrorAlert error={tenants.error ?? select.error} />
			{tenants.data?.length === 0 && (
				<p className="text-sm text-zinc-600">Todavía no perteneces a ninguna organización.</p>
			)}
			<ul className="space-y-2">
				{tenants.data?.map((tenant) => (
					<li key={tenant.id}>
						<button
							type="button"
							disabled={!tenant.isActive || select.isPending}
							onClick={() => select.mutate(tenant.id)}
							className="flex w-full items-center justify-between gap-3 rounded-md border border-zinc-200 px-4 py-3 text-left hover:border-zinc-400 hover:bg-zinc-50 disabled:opacity-50"
						>
							<span>
								<span className="block text-sm font-medium text-zinc-900">{tenant.name}</span>
								<span className="block text-xs text-zinc-500">{tenant.slug}</span>
							</span>
							{tenant.isActive ? (
								<Badge>{roleLabels[tenant.role] ?? tenant.role}</Badge>
							) : (
								<Badge tone="error">Desactivada</Badge>
							)}
						</button>
					</li>
				))}
			</ul>
		</AuthLayout>
	)
}

/** Primera organización del usuario (o una nueva). */
export function OnboardingPage() {
	const returnUrl = useReturnUrl()
	const create = useMutation({ mutationFn: auth.createTenant, onSuccess: () => continueTo(returnUrl) })

	return (
		<AuthLayout
			title="Crea tu organización"
			description="Es el espacio de tu empresa o equipo. Podrás invitar a más personas después."
			footer={<p>¿Te han invitado? Abre el enlace del email de invitación.</p>}
		>
			<Form onSubmit={(event) => create.mutate(formValues(event, "name").name)}>
				<Field label="Nombre de la organización">
					<Input name="name" minLength={2} maxLength={100} required autoFocus />
				</Field>
				<ErrorAlert error={create.error} />
				<Button type="submit" full loading={create.isPending}>
					Crear y continuar
				</Button>
			</Form>
		</AuthLayout>
	)
}
