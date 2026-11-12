import { cn } from "@paquetes/utiles"
import type { ComponentProps, ReactNode } from "react"

const control =
	"block w-full rounded-md border border-zinc-300 bg-white px-3 py-2 text-sm text-zinc-900 " +
	"placeholder:text-zinc-400 focus:border-zinc-900 focus:outline-none focus:ring-2 focus:ring-zinc-900/10 " +
	"disabled:bg-zinc-50 disabled:text-zinc-500"

/** Etiqueta + control + ayuda. El <label> envuelve al control, así quedan asociados sin ids. */
export function Field({ label, hint, children }: { label: string; hint?: ReactNode; children: ReactNode }) {
	return (
		<label className="block">
			<span className="mb-1.5 block text-sm font-medium text-zinc-800">{label}</span>
			{children}
			{hint && <span className="mt-1.5 block text-xs text-zinc-500">{hint}</span>}
		</label>
	)
}

export function Input({ className, ...props }: ComponentProps<"input">) {
	return <input className={cn(control, className)} {...props} />
}

export function Select({ className, ...props }: ComponentProps<"select">) {
	return <select className={cn(control, "pr-8", className)} {...props} />
}

export function Textarea({ className, ...props }: ComponentProps<"textarea">) {
	return <textarea className={cn(control, "font-mono", className)} rows={3} {...props} />
}

export function Checkbox({
	label,
	hint,
	...props
}: ComponentProps<"input"> & { label: string; hint?: string }) {
	return (
		<label className="flex items-start gap-3">
			<input type="checkbox" className="mt-0.5 size-4 accent-zinc-900" {...props} />
			<span>
				<span className="block text-sm font-medium text-zinc-800">{label}</span>
				{hint && <span className="block text-xs text-zinc-500">{hint}</span>}
			</span>
		</label>
	)
}

/** Formulario con separación vertical entre campos. */
export function Form({ className, ...props }: ComponentProps<"form">) {
	return <form className={cn("space-y-4", className)} {...props} />
}
