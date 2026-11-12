import { Alert, Button, ErrorAlert, Field, Form, Input, buttonClass } from "@paquetes/componentes"
import { formValues } from "@paquetes/utiles"
import { useMutation } from "@tanstack/react-query"
import { useState } from "react"
import { Link, useSearchParams } from "react-router"
import { auth } from "../api"
import { AuthLayout, linkClass } from "../AuthLayout"

const backToSignIn = (
	<Link className={linkClass} to="/login">
		Volver a iniciar sesión
	</Link>
)

export function ForgotPasswordPage() {
	const forgot = useMutation({ mutationFn: auth.forgotPassword })

	return (
		<AuthLayout
			title="Recupera tu contraseña"
			description="Te enviaremos un enlace para elegir una contraseña nueva."
			footer={backToSignIn}
		>
			{forgot.isSuccess ? (
				<Alert tone="success">
					Si existe una cuenta con <strong>{forgot.variables}</strong>, recibirás un email con el enlace.
					Caduca en 15 minutos.
				</Alert>
			) : (
				<Form onSubmit={(event) => forgot.mutate(formValues(event, "email").email)}>
					<Field label="Email">
						<Input name="email" type="email" autoComplete="email" required autoFocus />
					</Field>
					<ErrorAlert error={forgot.error} />
					<Button type="submit" full loading={forgot.isPending}>
						Enviar enlace
					</Button>
				</Form>
			)}
		</AuthLayout>
	)
}

/** Enlace del email de recuperación: /reset-password?token=… */
export function ResetPasswordPage() {
	const token = useSearchParams()[0].get("token") ?? ""
	const reset = useMutation({ mutationFn: auth.resetPassword })
	const [mismatch, setMismatch] = useState(false)

	if (reset.isSuccess) {
		return (
			<AuthLayout title="Contraseña cambiada">
				<Alert tone="success">
					Ya puedes iniciar sesión con tu nueva contraseña. Hemos cerrado tus otras sesiones.
				</Alert>
				<Link className={buttonClass("primary", true)} to="/login">
					Iniciar sesión
				</Link>
			</AuthLayout>
		)
	}

	return (
		<AuthLayout title="Elige una contraseña nueva" footer={backToSignIn}>
			<Form
				onSubmit={(event) => {
					const { newPassword, repeat } = formValues(event, "newPassword", "repeat")
					setMismatch(newPassword !== repeat)
					if (newPassword === repeat) {
						reset.mutate({ token, newPassword })
					}
				}}
			>
				<Field label="Contraseña nueva" hint="Mínimo 12 caracteres.">
					<Input
						name="newPassword"
						type="password"
						autoComplete="new-password"
						minLength={12}
						maxLength={128}
						required
						autoFocus
					/>
				</Field>
				<Field label="Repite la contraseña">
					<Input name="repeat" type="password" autoComplete="new-password" required />
				</Field>
				{mismatch && <Alert tone="error">Las contraseñas no coinciden.</Alert>}
				<ErrorAlert error={reset.error} />
				<Button type="submit" full loading={reset.isPending}>
					Cambiar contraseña
				</Button>
			</Form>
		</AuthLayout>
	)
}
