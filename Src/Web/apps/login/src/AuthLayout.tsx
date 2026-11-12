import { Logo } from "@paquetes/componentes"
import type { ReactNode } from "react"

type AuthLayoutProps = {
	title: string
	description?: ReactNode
	/** Enlaces bajo la tarjeta (crear cuenta, volver…). */
	footer?: ReactNode
	children?: ReactNode
}

/** Marco de todas las pantallas del login: logo, tarjeta centrada y pie. */
export function AuthLayout({ title, description, footer, children }: AuthLayoutProps) {
	return (
		<div className="flex min-h-dvh flex-col items-center justify-center px-4 py-12">
			<title>{`${title} · Herensuge`}</title>
			<div className="w-full max-w-sm">
				<div className="mb-8 flex flex-col items-center gap-3">
					<Logo className="size-12 rounded-lg" />
					<span className="text-sm font-semibold tracking-wide text-zinc-900">Herensuge</span>
				</div>
				<main className="rounded-xl border border-zinc-200 bg-white p-6 shadow-sm sm:p-8">
					<h1 className="text-xl font-semibold text-zinc-900">{title}</h1>
					{description && <p className="mt-1.5 text-sm text-zinc-500">{description}</p>}
					{children && <div className="mt-6 space-y-4">{children}</div>}
				</main>
				{footer && <div className="mt-6 space-y-2 text-center text-sm text-zinc-600">{footer}</div>}
			</div>
		</div>
	)
}

/** Enlace de texto con el estilo de la app. */
export const linkClass = "font-medium text-zinc-900 underline-offset-4 hover:underline"
