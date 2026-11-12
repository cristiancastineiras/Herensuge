import { Alert, Button, ErrorAlert, Field, Form, Input, Loading } from "@paquetes/componentes"
import { cn, formValues } from "@paquetes/utiles"
import { useMutation, useQuery } from "@tanstack/react-query"
import { useState } from "react"
import { Link } from "react-router"
import { auth, signInWithPasskey } from "../api"
import { AuthLayout, linkClass } from "../AuthLayout"
import { continueTo, useReturnUrl, withReturnUrl } from "../navigation"

/** Métodos que envía la API → texto. */
const methodLabels: Record<string, string> = {
	totp: "App autenticadora",
	sms: "SMS",
	recovery_code: "Código de recuperación",
}

/** Segundo factor: tras la contraseña, o cuando la organización exige MFA a una sesión que no lo tiene. */
export function MfaPage() {
	const returnUrl = useReturnUrl()
	const challenge = useQuery({ queryKey: ["mfa-challenge"], queryFn: auth.mfaChallenge })
	const [selected, setSelected] = useState<string | null>(null)
	const verify = useMutation({ mutationFn: auth.verifyMfa, onSuccess: () => continueTo(returnUrl) })
	const sms = useMutation({ mutationFn: auth.sendSmsCode })
	const passkey = useMutation({ mutationFn: signInWithPasskey, onSuccess: () => continueTo(returnUrl) })

	if (challenge.isPending) {
		return (
			<AuthLayout title="Verificación en dos pasos">
				<Loading />
			</AuthLayout>
		)
	}

	if (challenge.isError) {
		return (
			<AuthLayout title="Verificación en dos pasos">
				<ErrorAlert error={challenge.error} />
				<Link className={linkClass} to={withReturnUrl("/login", returnUrl)}>
					Volver a iniciar sesión
				</Link>
			</AuthLayout>
		)
	}

	const { methods, phoneNumberHint } = challenge.data
	const method = selected ?? methods[0] ?? null

	return (
		<AuthLayout
			title="Verificación en dos pasos"
			description="Confirma que eres tú con tu segundo factor."
			footer={
				<Link className={linkClass} to={withReturnUrl("/login", returnUrl)}>
					Usar otra cuenta
				</Link>
			}
		>
			{methods.length > 1 && (
				<div
					role="tablist"
					className="grid grid-flow-col gap-1 rounded-md bg-zinc-100 p-1 text-xs font-medium"
				>
					{methods.map((item) => (
						<button
							key={item}
							type="button"
							role="tab"
							aria-selected={item === method}
							className={cn("rounded px-2 py-1.5", item === method ? "bg-white shadow-sm" : "text-zinc-600")}
							onClick={() => {
								setSelected(item)
								verify.reset()
							}}
						>
							{methodLabels[item] ?? item}
						</button>
					))}
				</div>
			)}

			{method && (
				<Form onSubmit={(event) => verify.mutate({ method, code: formValues(event, "code").code.trim() })}>
					{method === "sms" && (
						<div className="space-y-2">
							<p className="text-sm text-zinc-600">Te enviaremos un código al teléfono {phoneNumberHint}.</p>
							{sms.isSuccess ? (
								<Alert tone="success">Código enviado. Caduca en 5 minutos.</Alert>
							) : (
								<Button variant="secondary" loading={sms.isPending} onClick={() => sms.mutate()}>
									Enviarme el código
								</Button>
							)}
							<ErrorAlert error={sms.error} />
						</div>
					)}
					<Field
						label={method === "recovery_code" ? "Código de recuperación" : "Código de 6 dígitos"}
						hint={method === "totp" ? "Ábrelo en tu app autenticadora." : undefined}
					>
						{method === "recovery_code" ? (
							<Input key={method} name="code" autoComplete="off" maxLength={20} required autoFocus />
						) : (
							<Input
								key={method}
								name="code"
								inputMode="numeric"
								autoComplete="one-time-code"
								pattern="[0-9]{6}"
								maxLength={6}
								required
								autoFocus
							/>
						)}
					</Field>
					<ErrorAlert error={verify.error} />
					<Button type="submit" full loading={verify.isPending}>
						Verificar
					</Button>
				</Form>
			)}

			<div className="space-y-2 border-t border-zinc-100 pt-4">
				<Button variant="secondary" full loading={passkey.isPending} onClick={() => passkey.mutate()}>
					Usar una passkey
				</Button>
				<ErrorAlert error={passkey.error} />
			</div>
		</AuthLayout>
	)
}
