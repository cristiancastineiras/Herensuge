import { Alert, Button, ErrorAlert, Field, Form, Input, Loading } from "@paquetes/componentes"
import { formValues } from "@paquetes/utiles"
import { useMutation, useQuery } from "@tanstack/react-query"
import { useState } from "react"
import { Link } from "react-router"
import { auth, signInWithPasskey } from "../api"
import { AuthLayout, linkClass } from "../AuthLayout"
import { continueTo, useReturnUrl, withReturnUrl } from "../navigation"

/** Métodos que envía la API: título del campo y texto para elegirlo como alternativa. */
const methodText: Record<string, { title: string; other: string; hint?: string }> = {
	totp: {
		title: "Código de la app autenticadora",
		other: "Usar la app autenticadora",
		hint: "Ábrela y escribe el código de 6 dígitos.",
	},
	sms: { title: "Código recibido por SMS", other: "Recibir un código por SMS" },
	email: { title: "Código recibido por email", other: "Recibir un código por email" },
	recovery_code: {
		title: "Código de recuperación",
		other: "Usar un código de recuperación",
		hint: "Uno de los que guardaste al activar la verificación. Cada uno sirve una sola vez.",
	},
}

/** Segundo factor: tras la contraseña, o cuando la organización exige MFA a una sesión que no lo tiene. */
export function MfaPage() {
	const returnUrl = useReturnUrl()
	const challenge = useQuery({ queryKey: ["mfa-challenge"], queryFn: auth.mfaChallenge })
	const [selected, setSelected] = useState<string | null>(null)
	const verify = useMutation({ mutationFn: auth.verifyMfa, onSuccess: () => continueTo(returnUrl) })
	const send = useMutation({ mutationFn: auth.sendCode })
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

	const { methods, phoneNumberHint, emailHint } = challenge.data
	const method = selected ?? methods[0] ?? null
	const choose = (item: string) => {
		setSelected(item)
		verify.reset()
		send.reset()
	}

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
			{method && (
				<Form onSubmit={(event) => verify.mutate({ method, code: formValues(event, "code").code.trim() })}>
					{(method === "sms" || method === "email") && (
						<div className="space-y-2">
							<p className="text-sm text-zinc-600">
								Te enviaremos un código de 6 dígitos {method === "sms" ? "al teléfono" : "a"}{" "}
								{(method === "sms" ? phoneNumberHint : emailHint) ?? "que tienes verificado"}.
							</p>
							{send.isSuccess && <Alert tone="success">Código enviado. Caduca en 5 minutos.</Alert>}
							<Button variant="secondary" loading={send.isPending} onClick={() => send.mutate(method)}>
								{send.isSuccess ? "Reenviar el código" : "Enviarme el código"}
							</Button>
							<ErrorAlert error={send.error} />
						</div>
					)}
					<Field label={methodText[method]?.title ?? method} hint={methodText[method]?.hint}>
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
				<p className="text-xs font-medium text-zinc-500">¿No puedes usar este método?</p>
				{methods
					.filter((item) => item !== method)
					.map((item) => (
						<Button key={item} variant="secondary" full onClick={() => choose(item)}>
							{methodText[item]?.other ?? item}
						</Button>
					))}
				<Button variant="secondary" full loading={passkey.isPending} onClick={() => passkey.mutate()}>
					Usar una passkey
				</Button>
				<ErrorAlert error={passkey.error} />
			</div>
		</AuthLayout>
	)
}
