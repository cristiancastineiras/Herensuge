import { Alert, Button, ErrorAlert, Field, Form, Input } from "@paquetes/componentes"
import { formValues } from "@paquetes/utiles"
import { useMutation } from "@tanstack/react-query"
import { Link } from "react-router"
import { auth } from "../api"
import { AuthLayout, linkClass } from "../AuthLayout"
import { useLoginPage } from "../loginPage"
import { useReturnUrl, withReturnUrl } from "../navigation"
import { ProfileFieldInputs, readProfileFields } from "../ProfileFields"

export function SignUpPage() {
	const returnUrl = useReturnUrl()
	const page = useLoginPage()
	const { allowRegistration, profileFields, branding } = page.settings
	const register = useMutation({ mutationFn: auth.register })
	const footer = (
		<p>
			¿Ya tienes cuenta?{" "}
			<Link className={linkClass} to={withReturnUrl("/login", returnUrl)}>
				Inicia sesión
			</Link>
		</p>
	)

	if (!allowRegistration) {
		return (
			<AuthLayout title="Crea tu cuenta" footer={footer}>
				<Alert>
					{page.displayName} no permite crear cuentas desde aquí. Si tu organización la usa, pide que te
					inviten.
				</Alert>
			</AuthLayout>
		)
	}

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
			<Form
				onSubmit={(event) =>
					register.mutate({
						...formValues(event, "displayName", "email", "password"),
						clientId: page.clientId || null,
						profileFields: readProfileFields(event.currentTarget, profileFields),
					})
				}
			>
				<Field label="Nombre">
					<Input name="displayName" autoComplete="name" maxLength={100} required autoFocus />
				</Field>
				<Field label="Email">
					<Input name="email" type="email" autoComplete="username" spellCheck={false} required />
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
				<ProfileFieldInputs fields={profileFields} />
				{(branding.termsUrl || branding.privacyUrl) && <LegalNotice page={page} />}
				<ErrorAlert error={register.error} />
				<Button type="submit" full loading={register.isPending}>
					Crear cuenta
				</Button>
			</Form>
		</AuthLayout>
	)
}

function externalLink(href: string, text: string) {
	return (
		<a className={linkClass} href={href} target="_blank" rel="noreferrer">
			{text}
		</a>
	)
}

/** "Al crear la cuenta aceptas las condiciones y la política de privacidad de …", con los enlaces que haya. */
function LegalNotice({ page }: { page: ReturnType<typeof useLoginPage> }) {
	const { termsUrl, privacyUrl } = page.settings.branding
	return (
		<p className="text-xs text-zinc-500">
			Al crear la cuenta aceptas {termsUrl && externalLink(termsUrl, "las condiciones de uso")}
			{termsUrl && privacyUrl && " y "}
			{privacyUrl && externalLink(privacyUrl, "la política de privacidad")} de {page.displayName}.
		</p>
	)
}
