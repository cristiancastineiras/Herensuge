import { describeError } from "@paquetes/api"
import { cn } from "@paquetes/utiles"
import type { ComponentProps, ReactNode } from "react"
import { Spinner } from "./Button"

type CardProps = {
	title?: ReactNode
	description?: ReactNode
	actions?: ReactNode
	/** Sin relleno interior (para tablas). */
	flush?: boolean
	className?: string
	children?: ReactNode
}

export function Card({ title, description, actions, flush = false, className, children }: CardProps) {
	return (
		<section className={cn("rounded-lg border border-zinc-200 bg-white", className)}>
			{(title || actions) && (
				<header className="flex flex-wrap items-start justify-between gap-3 border-b border-zinc-100 px-5 py-4">
					<div>
						{title && <h2 className="text-base font-semibold text-zinc-900">{title}</h2>}
						{description && <p className="mt-0.5 text-sm text-zinc-500">{description}</p>}
					</div>
					{actions}
				</header>
			)}
			{children && <div className={cn(!flush && "space-y-4 px-5 py-4")}>{children}</div>}
		</section>
	)
}

type Tone = "neutral" | "success" | "warning" | "error"

const alertTones: Record<Tone, string> = {
	neutral: "border-zinc-200 bg-zinc-50 text-zinc-700",
	success: "border-green-200 bg-green-50 text-green-800",
	warning: "border-amber-200 bg-amber-50 text-amber-900",
	error: "border-red-200 bg-red-50 text-red-800",
}

export function Alert({ tone = "neutral", children }: { tone?: Tone; children: ReactNode }) {
	return (
		<div
			role={tone === "error" ? "alert" : "status"}
			className={cn("rounded-md border px-3.5 py-2.5 text-sm", alertTones[tone])}
		>
			{children}
		</div>
	)
}

/** Muestra el mensaje en español de un error de la API (o nada si no hay error). */
export function ErrorAlert({ error }: { error: unknown }) {
	return error ? <Alert tone="error">{describeError(error)}</Alert> : null
}

const badgeTones: Record<Tone, string> = {
	neutral: "bg-zinc-100 text-zinc-700",
	success: "bg-green-100 text-green-800",
	warning: "bg-amber-100 text-amber-900",
	error: "bg-red-100 text-red-800",
}

export function Badge({ tone = "neutral", children }: { tone?: Tone; children: ReactNode }) {
	return (
		<span
			className={cn("inline-flex items-center rounded px-1.5 py-0.5 text-xs font-medium", badgeTones[tone])}
		>
			{children}
		</span>
	)
}

export function Loading({ label = "Cargando…" }: { label?: string }) {
	return (
		<div className="flex items-center gap-2 py-6 text-sm text-zinc-500">
			<Spinner />
			{label}
		</div>
	)
}

/** Tabla simple. Las cabeceras deben ser distintas entre sí (se usan como key); "" para la de acciones. */
export function Table({ headers, children }: { headers: string[]; children: ReactNode }) {
	return (
		<div className="overflow-x-auto">
			<table className="w-full text-left text-sm">
				<thead className="border-b border-zinc-200 text-xs text-zinc-500">
					<tr>
						{headers.map((header) => (
							<th key={header} scope="col" className="px-5 py-2.5 font-medium whitespace-nowrap">
								{header}
							</th>
						))}
					</tr>
				</thead>
				<tbody className="divide-y divide-zinc-100">{children}</tbody>
			</table>
		</div>
	)
}

export function Td({ className, ...props }: ComponentProps<"td">) {
	return <td className={cn("px-5 py-3 align-middle text-zinc-700", className)} {...props} />
}

/** Logo de Herensuge. El fichero está en paquetes/componentes/public (publicDir de las dos apps). */
export function Logo({ className }: { className?: string }) {
	return <img src="/logo.svg" alt="" className={cn("size-8 rounded-md", className)} />
}
