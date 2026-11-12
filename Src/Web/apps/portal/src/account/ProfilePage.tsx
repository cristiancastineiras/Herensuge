import {
	Alert,
	Badge,
	Button,
	Card,
	ConfirmButton,
	ErrorAlert,
	Field,
	Form,
	Input,
} from "@paquetes/componentes"
import { formValues } from "@paquetes/utiles"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { me, switchTenant, tenants } from "../api"
import { PageHeader, QueryState, roleLabels } from "../components"
import { useUser } from "../user"

export default function ProfilePage() {
	const profile = useQuery({ queryKey: ["me"], queryFn: me.get })

	return (
		<>
			<PageHeader title="Perfil" description="Tus datos y las organizaciones a las que perteneces." />
			<QueryState query={profile} />
			{profile.data && (
				<>
					<ProfileCard displayName={profile.data.displayName} />
					<EmailCard email={profile.data.email} verified={profile.data.emailVerified} />
				</>
			)}
			<OrganizationsCard />
			<DeleteAccountCard />
		</>
	)
}

function ProfileCard({ displayName }: { displayName: string }) {
	const queryClient = useQueryClient()
	const update = useMutation({
		mutationFn: me.update,
		onSuccess: () => queryClient.invalidateQueries({ queryKey: ["me"] }),
	})

	return (
		<Card title="Datos personales">
			<Form onSubmit={(event) => update.mutate(formValues(event, "displayName").displayName)}>
				<Field label="Nombre">
					<Input name="displayName" defaultValue={displayName} autoComplete="name" maxLength={100} required />
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

function EmailCard({ email, verified }: { email: string; verified: boolean }) {
	const change = useMutation({ mutationFn: me.changeEmail })

	return (
		<Card
			title="Email"
			description={
				<>
					{email}{" "}
					{verified ? <Badge tone="success">Verificado</Badge> : <Badge tone="warning">Sin verificar</Badge>}
				</>
			}
		>
			{change.isSuccess ? (
				<Alert tone="success">
					Te hemos enviado un enlace a <strong>{change.variables}</strong> para confirmar el cambio. Caduca en
					1 hora.
				</Alert>
			) : (
				<Form onSubmit={(event) => change.mutate(formValues(event, "newEmail").newEmail)}>
					<Field label="Nuevo email" hint="El cambio se aplica al abrir el enlace que te enviaremos.">
						<Input name="newEmail" type="email" autoComplete="email" required />
					</Field>
					<ErrorAlert error={change.error} />
					<Button type="submit" variant="secondary" loading={change.isPending}>
						Cambiar email
					</Button>
				</Form>
			)}
		</Card>
	)
}

function OrganizationsCard() {
	const user = useUser()
	const list = useQuery({ queryKey: ["my-tenants"], queryFn: me.tenants })
	const leave = useMutation({
		mutationFn: () => tenants.removeMember(user.tenantId ?? "", user.subject),
		// Los tokens actuales son de la organización que se abandona: se piden otros.
		onSuccess: () => window.location.assign("/bff/login?returnUrl=/"),
	})

	return (
		<Card title="Organizaciones">
			<QueryState query={list} />
			<ul className="divide-y divide-zinc-100">
				{list.data?.map((tenant) => {
					const active = tenant.id === user.tenantId
					return (
						<li
							key={tenant.id}
							className="flex flex-wrap items-center justify-between gap-3 py-3 first:pt-0 last:pb-0"
						>
							<div>
								<p className="text-sm font-medium text-zinc-900">
									{tenant.name} {active && <Badge tone="success">Activa</Badge>}{" "}
									{!tenant.isActive && <Badge tone="error">Desactivada</Badge>}
								</p>
								<p className="text-xs text-zinc-500">{roleLabels[tenant.role] ?? tenant.role}</p>
							</div>
							{active ? (
								<ConfirmButton
									question="¿Abandonar la organización?"
									confirmLabel="Abandonar"
									loading={leave.isPending}
									onConfirm={() => leave.mutate()}
								>
									Abandonar
								</ConfirmButton>
							) : (
								<Button
									variant="secondary"
									disabled={!tenant.isActive}
									onClick={() => switchTenant(tenant.id)}
								>
									Cambiar a esta
								</Button>
							)}
						</li>
					)
				})}
			</ul>
			<ErrorAlert error={leave.error} />
		</Card>
	)
}

function DeleteAccountCard() {
	const user = useUser()
	const remove = useMutation({
		mutationFn: me.remove,
		onSuccess: () => window.location.assign(user.logoutUrl),
	})

	return (
		<Card title="Eliminar cuenta" description="Se borran tus datos y tus sesiones. No se puede deshacer.">
			<ErrorAlert error={remove.error} />
			<ConfirmButton
				variant="danger"
				question="¿Seguro? Se eliminará tu cuenta para siempre."
				confirmLabel="Sí, eliminar mi cuenta"
				loading={remove.isPending}
				onConfirm={() => remove.mutate()}
			>
				Eliminar mi cuenta
			</ConfirmButton>
		</Card>
	)
}
