import type { RecoveryCodesResponse, TotpSetupResponse } from "@paquetes/api"
import { formValues } from "@paquetes/utiles"
import { useMutation } from "@tanstack/react-query"
import { useEffect, useMemo, useRef, useState } from "react"
import { renderSVG } from "uqr"
import { Button } from "./Button"
import { Field, Form, Input } from "./Form"
import { Alert, ErrorAlert, Loading } from "./Layout"

/** Código QR como imagen SVG (data URL, permitido por la CSP: img-src 'self' data:). */
export function QrCode({ value }: { value: string }) {
	const src = useMemo(
		() => `data:image/svg+xml;utf8,${encodeURIComponent(renderSVG(value, { border: 1 }))}`,
		[value],
	)
	return (
		<img
			src={src}
			alt="Código QR para la app autenticadora"
			className="size-44 rounded-md border border-zinc-200 p-2"
		/>
	)
}

export function RecoveryCodes({ codes }: { codes: string[] }) {
	const [copied, setCopied] = useState(false)

	return (
		<div className="space-y-3">
			<Alert tone="warning">
				Guarda estos códigos de recuperación en un lugar seguro. Cada uno sirve una sola vez si pierdes tu
				segundo factor y no se volverán a mostrar.
			</Alert>
			<ul className="grid grid-cols-2 gap-x-4 gap-y-1 rounded-md border border-zinc-200 bg-zinc-50 p-4 font-mono text-sm">
				{codes.map((code) => (
					<li key={code}>{code}</li>
				))}
			</ul>
			<Button
				variant="secondary"
				onClick={async () => {
					await navigator.clipboard.writeText(codes.join("\n"))
					setCopied(true)
				}}
			>
				{copied ? "Copiados" : "Copiar códigos"}
			</Button>
		</div>
	)
}

type TotpSetupProps = {
	begin: () => Promise<TotpSetupResponse>
	confirm: (code: string) => Promise<RecoveryCodesResponse>
	onDone: () => void
	doneLabel?: string
}

/** Alta de la app autenticadora: QR → código de 6 dígitos → códigos de recuperación. */
export function TotpSetup({ begin, confirm, onDone, doneLabel = "Continuar" }: TotpSetupProps) {
	const start = useMutation({ mutationFn: begin })
	const verify = useMutation({
		mutationFn: confirm,
		onSuccess: (result) => {
			if (result.recoveryCodes.length === 0) {
				onDone()
			}
		},
	})

	// Un solo secreto por montaje (StrictMode ejecuta los efectos dos veces en desarrollo).
	const started = useRef(false)
	useEffect(() => {
		if (!started.current) {
			started.current = true
			start.mutate()
		}
	}, [start])

	if (verify.data && verify.data.recoveryCodes.length > 0) {
		return (
			<div className="space-y-4">
				<Alert tone="success">La app autenticadora está activada.</Alert>
				<RecoveryCodes codes={verify.data.recoveryCodes} />
				<Button full onClick={onDone}>
					{doneLabel}
				</Button>
			</div>
		)
	}

	if (!start.data) {
		return start.isError ? <ErrorAlert error={start.error} /> : <Loading />
	}

	return (
		<Form
			onSubmit={(event) => {
				const { code } = formValues(event, "code")
				verify.mutate(code)
			}}
		>
			<p className="text-sm text-zinc-600">
				Escanea el código con tu app autenticadora (Google Authenticator, Microsoft Authenticator, 1Password…)
				y escribe el código de 6 dígitos que muestra.
			</p>
			<div className="flex justify-center">
				<QrCode value={start.data.authenticatorUri} />
			</div>
			<p className="text-xs text-zinc-500">
				¿No puedes escanearlo? Introduce esta clave a mano:{" "}
				<code className="font-mono break-all text-zinc-800">{start.data.sharedKey}</code>
			</p>
			<Field label="Código de 6 dígitos">
				<Input
					name="code"
					inputMode="numeric"
					autoComplete="one-time-code"
					pattern="[0-9]{6}"
					maxLength={6}
					required
				/>
			</Field>
			<ErrorAlert error={verify.error} />
			<Button type="submit" full loading={verify.isPending}>
				Activar
			</Button>
		</Form>
	)
}
