import { ApiError, type BffUser } from "@paquetes/api"
import { Alert, Button, Card, ErrorAlert, Loading, Logo, Select, buttonClass } from "@paquetes/componentes"
import { cn } from "@paquetes/utiles"
import { useQuery } from "@tanstack/react-query"
import { Suspense, type ReactNode } from "react"
import { Link, NavLink, Outlet, useSearchParams } from "react-router"
import { bff, me, reauthenticate, signIn, switchTenant } from "./api"
import { PageHeader } from "./components"
import { UserContext, isTenantAdmin, usedPasskey, useUser } from "./user"

/** Carga la sesión (/bff/user) y pinta la cabecera, la navegación y la página. */
export function AppLayout() {
	// La BFF vuelve a /?error=… si el inicio de sesión falla: no se reintenta solo (evita un bucle).
	const callbackError = useSearchParams()[0].get("error")
	const user = useQuery({
		queryKey: ["bff-user"],
		queryFn: bff.user,
		enabled: !callbackError,
		staleTime: Infinity,
	})

	if (callbackError) {
		return (
			<CenteredMessage title="No se ha podido iniciar sesión">
				<Alert tone="error">El inicio de sesión no se ha completado ({callbackError}).</Alert>
				<Button onClick={() => signIn("/")}>Volver a intentarlo</Button>
			</CenteredMessage>
		)
	}

	if (user.isPending || (user.error instanceof ApiError && user.error.status === 401)) {
		// Un 401 ya está redirigiendo a /bff/login.
		return (
			<CenteredMessage title="Herensuge">
				<Loading />
			</CenteredMessage>
		)
	}

	if (user.isError) {
		return (
			<CenteredMessage title="No se ha podido cargar el portal">
				<ErrorAlert error={user.error} />
				<Button onClick={() => window.location.reload()}>Recargar</Button>
			</CenteredMessage>
		)
	}

	return (
		<UserContext value={user.data}>
			<Header user={user.data} />
			<div className="mx-auto grid max-w-6xl gap-8 px-4 py-8 md:grid-cols-[200px_minmax(0,1fr)]">
				<Navigation user={user.data} />
				<main className="min-w-0 space-y-6">
					<Suspense fallback={<Loading />}>
						<Outlet />
					</Suspense>
				</main>
			</div>
		</UserContext>
	)
}

function Header({ user }: { user: BffUser }) {
	return (
		<header className="border-b border-zinc-200 bg-white">
			<div className="mx-auto flex h-14 max-w-6xl items-center gap-4 px-4">
				<Link to="/" className="flex items-center gap-2">
					<Logo className="size-7" />
					<span className="text-sm font-semibold text-zinc-900">Herensuge</span>
				</Link>
				<TenantSwitcher user={user} />
				<div className="ml-auto flex items-center gap-2">
					<span className="hidden text-sm text-zinc-600 sm:inline">{user.name ?? user.email}</span>
					<a className={buttonClass("ghost")} href={user.logoutUrl}>
						Cerrar sesión
					</a>
				</div>
			</div>
		</header>
	)
}

/** Organización activa. Cambiarla pide al IdP tokens nuevos para la elegida. */
function TenantSwitcher({ user }: { user: BffUser }) {
	const tenants = useQuery({ queryKey: ["my-tenants"], queryFn: me.tenants })
	if (!tenants.data || tenants.data.length === 0) {
		return null
	}

	if (tenants.data.length === 1) {
		return <span className="truncate text-sm text-zinc-500">{tenants.data[0]?.name}</span>
	}

	return (
		<Select
			aria-label="Organización"
			className="w-auto max-w-56 py-1.5"
			value={user.tenantId ?? ""}
			onChange={(event) => switchTenant(event.target.value)}
		>
			{tenants.data.map((tenant) => (
				<option key={tenant.id} value={tenant.id} disabled={!tenant.isActive}>
					{tenant.name}
				</option>
			))}
		</Select>
	)
}

type NavItem = { to: string; label: string; end?: boolean }

function Navigation({ user }: { user: BffUser }) {
	const sections: { title: string; items: NavItem[] }[] = [
		{
			title: "Mi cuenta",
			items: [
				{ to: "/", label: "Perfil", end: true },
				{ to: "/seguridad", label: "Seguridad" },
				{ to: "/sesiones", label: "Sesiones" },
			],
		},
	]

	if (isTenantAdmin(user)) {
		sections.push({
			title: "Organización",
			items: [
				{ to: "/organizacion", label: "General", end: true },
				{ to: "/organizacion/miembros", label: "Miembros" },
				{ to: "/organizacion/auditoria", label: "Auditoría" },
			],
		})
	}

	if (user.isPlatformAdmin) {
		sections.push({
			title: "Plataforma",
			items: [
				{ to: "/plataforma/organizaciones", label: "Organizaciones" },
				{ to: "/plataforma/clientes", label: "Clientes OIDC" },
				{ to: "/plataforma/auditoria", label: "Auditoría" },
			],
		})
	}

	return (
		<nav className="flex flex-wrap gap-x-8 gap-y-4 md:block md:space-y-6">
			{sections.map((section) => (
				<div key={section.title}>
					<p className="mb-1 px-3 text-xs font-medium text-zinc-400">{section.title}</p>
					<ul>
						{section.items.map((item) => (
							<li key={item.to}>
								<NavLink
									to={item.to}
									end={item.end ?? false}
									className={({ isActive }) =>
										cn(
											"block rounded-md px-3 py-1.5 text-sm",
											isActive
												? "bg-zinc-200/70 font-medium text-zinc-900"
												: "text-zinc-600 hover:bg-zinc-100 hover:text-zinc-900",
										)
									}
								>
									{item.label}
								</NavLink>
							</li>
						))}
					</ul>
				</div>
			))}
		</nav>
	)
}

/** Las rutas por rol son solo comodidad: la autorización real la aplica siempre la API. */
export function RequireTenantAdmin() {
	return isTenantAdmin(useUser()) ? <Outlet /> : <NoAccess />
}

export function RequirePlatformAdmin() {
	const user = useUser()
	if (!user.isPlatformAdmin) {
		return <NoAccess />
	}

	if (!usedPasskey(user)) {
		return (
			<>
				<PageHeader title="Plataforma" />
				<Card title="Entra con tu passkey">
					<p className="text-sm text-zinc-600">
						Por seguridad, la administración de la plataforma exige haber iniciado sesión con una passkey. Si
						aún no tienes una, añádela en{" "}
						<Link className="font-medium text-zinc-900 underline underline-offset-4" to="/seguridad">
							Seguridad
						</Link>{" "}
						y vuelve a entrar con ella.
					</p>
					<Button onClick={() => reauthenticate()}>Iniciar sesión con passkey</Button>
				</Card>
			</>
		)
	}

	return <Outlet />
}

function NoAccess() {
	return (
		<>
			<PageHeader title="Sin acceso" />
			<Alert>Tu rol no permite ver esta sección.</Alert>
		</>
	)
}

export function NotFoundPage() {
	return (
		<>
			<PageHeader title="Página no encontrada" />
			<Link className={buttonClass("secondary")} to="/">
				Volver al inicio
			</Link>
		</>
	)
}

export function RouteErrorPage() {
	return (
		<CenteredMessage title="Algo ha fallado">
			<p className="text-sm text-zinc-600">Recarga la página para volver a intentarlo.</p>
			<Button onClick={() => window.location.reload()}>Recargar</Button>
		</CenteredMessage>
	)
}

function CenteredMessage({ title, children }: { title: string; children: ReactNode }) {
	return (
		<div className="flex min-h-dvh items-center justify-center px-4">
			<div className="w-full max-w-sm space-y-4 text-center">
				<Logo className="mx-auto size-12 rounded-lg" />
				<h1 className="text-lg font-semibold">{title}</h1>
				{children}
			</div>
		</div>
	)
}
