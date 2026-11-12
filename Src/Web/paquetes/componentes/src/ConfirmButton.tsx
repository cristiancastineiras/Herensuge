import { useState, type ReactNode } from "react"
import { Button, type ButtonVariant } from "./Button"

type ConfirmButtonProps = {
	children: ReactNode
	/** Pregunta que aparece al pulsar, por ejemplo "¿Eliminar la passkey?". */
	question?: string
	confirmLabel?: string
	variant?: ButtonVariant
	loading?: boolean
	disabled?: boolean
	onConfirm: () => void
}

/** Botón en dos pasos para acciones destructivas: pulsar → confirmar o cancelar. Sin diálogos modales. */
export function ConfirmButton({
	children,
	question,
	confirmLabel = "Confirmar",
	variant = "secondary",
	loading = false,
	disabled = false,
	onConfirm,
}: ConfirmButtonProps) {
	const [asking, setAsking] = useState(false)

	if (!asking) {
		return (
			<Button variant={variant} disabled={disabled} loading={loading} onClick={() => setAsking(true)}>
				{children}
			</Button>
		)
	}

	return (
		<span className="inline-flex flex-wrap items-center gap-2">
			{question && <span className="text-sm text-zinc-600">{question}</span>}
			<Button
				variant="danger"
				loading={loading}
				onClick={() => {
					onConfirm()
					setAsking(false)
				}}
			>
				{confirmLabel}
			</Button>
			<Button variant="ghost" onClick={() => setAsking(false)}>
				Cancelar
			</Button>
		</span>
	)
}
