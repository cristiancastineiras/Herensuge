import { cn } from "@paquetes/utiles"
import type { ComponentProps } from "react"

export type ButtonVariant = "primary" | "secondary" | "danger" | "ghost"

const base =
	"inline-flex items-center justify-center gap-2 rounded-md px-3.5 py-2 text-sm font-medium transition-colors " +
	"focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-zinc-900 disabled:pointer-events-none disabled:opacity-50"

const variants: Record<ButtonVariant, string> = {
	primary: "bg-zinc-900 text-white hover:bg-zinc-700",
	secondary: "border border-zinc-300 bg-white text-zinc-900 hover:bg-zinc-50",
	danger: "bg-red-600 text-white hover:bg-red-500",
	ghost: "text-zinc-700 hover:bg-zinc-100",
}

/** Clases de botón para usar en enlaces: <a className={buttonClass("secondary")}>. */
export function buttonClass(variant: ButtonVariant = "primary", full = false): string {
	return cn(base, variants[variant], full && "w-full")
}

type ButtonProps = ComponentProps<"button"> & {
	variant?: ButtonVariant
	loading?: boolean
	full?: boolean
}

export function Button({
	variant = "primary",
	loading = false,
	full = false,
	type = "button",
	disabled,
	className,
	children,
	...props
}: ButtonProps) {
	return (
		<button
			type={type}
			disabled={disabled || loading}
			aria-busy={loading || undefined}
			className={cn(buttonClass(variant, full), className)}
			{...props}
		>
			{loading && <Spinner />}
			{children}
		</button>
	)
}

export function Spinner() {
	return (
		<span
			aria-hidden="true"
			className="size-4 animate-spin rounded-full border-2 border-current border-r-transparent"
		/>
	)
}
