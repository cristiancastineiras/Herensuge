import { Alert, Button, ErrorAlert, Field, Form, Input } from "@paquetes/componentes"
import { formValues } from "@paquetes/utiles"
import { useMutation } from "@tanstack/react-query"
import { Link } from "react-router"
import { auth } from "../api"
import { AuthLayout, linkClass } from "../AuthLayout"
import { useReturnUrl, withReturnUrl } from "../navigation"

export function SignUpPage() {
	const returnUrl = useReturnUrl()
	const register = useMutation({ mutationFn: auth.register })
	const footer = (
		<p>
			¿Ya tienes cuenta?{" "}
			<Link className={linkClass} to={withReturnUrl("/login", returnUrl)}>
				Inicia sesión
			</Link>
		</p>
	)

	if (register.isSuccess) {
		return (
			<AuthLayout title="Revisa tu email" footer={footer}>
				<Alert tone="success">
					Te hemos enviado un enlace a <strong>{register.variables.email}</strong> para verificar tu cuenta.
					Caduca en 24 horas.
				</Alert>
			</AuthLayout>
		)
	}

	return (
		<AuthLayout title="Crea tu cuenta" footer={footer}>
			<Form onSubmit={(event) => register.mutate(formValues(event, "displayName", "email", "password"))}>
				<Field label="Nombre">
					<Input name="displayName" autoComplete="name" maxLength={100} required autoFocus />
				</Field>
				<Field label="Email">
					<Input name="email" type="email" autoComplete="email" required />
				</Field>
				<Field
					label="Contraseña"
					hint="Mínimo 12 caracteres. Una frase larga es más segura que una contraseña rara."
				>
					<Input
						name="password"
						type="password"
						autoComplete="new-password"
						minLength={12}
						maxLength={128}
						required
					/>
				</Field>
				<ErrorAlert error={register.error} />
				<Button type="submit" full loading={register.isPending}>
					Crear cuenta
				</Button>
			</Form>
		</AuthLayout>
	)
}
