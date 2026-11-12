import { describeErrorCode, errorCode, type SignInResponse } from "@paquetes/api"
import { Alert, Button, ErrorAlert, Field, Form, Input, buttonClass } from "@paquetes/componentes"
import { formValues } from "@paquetes/utiles"
import { useMutation, useQuery } from "@tanstack/react-query"
import { Link, useNavigate, useSearchParams } from "react-router"
import { auth, providerNames, signInWithPasskey } from "../api"
import { AuthLayout, linkClass } from "../AuthLayout"
import { continueTo, useReturnUrl, withReturnUrl } from "../navigation"

export function SignInPage() {
	const returnUrl = useReturnUrl()
	const [params] = useSearchParams()
	const navigate = useNavigate()
	const providers = useQuery({ queryKey: ["providers"], queryFn: auth.providers })

	const onSignedIn = (result: SignInResponse) => {
		if (result.status === "mfa-required") {
			navigate(withReturnUrl("/mfa", returnUrl))
		} else {
			continueTo(returnUrl)
		}
	}

	const password = useMutation({ mutationFn: auth.signIn, onSuccess: onSignedIn })
	const passkey = useMutation({ mutationFn: signInWithPasskey, onSuccess: onSignedIn })
	const resend = useMutation({ mutationFn: auth.resendVerification })
	const urlError = params.get("error")

	return (
		<AuthLayout
			title="Inicia sesión"
			footer={
				<p>
					¿No tienes cuenta?{" "}
					<Link className={linkClass} to={withReturnUrl("/register", returnUrl)}>
						Crear cuenta
					</Link>
				</p>
			}
		>
			{urlError && <Alert tone="error">{describeErrorCode(urlError)}</Alert>}

			<Form onSubmit={(event) => password.mutate(formValues(event, "email", "password"))}>
				<Field label="Email">
					<Input name="email" type="email" autoComplete="username webauthn" required autoFocus />
				</Field>
				<Field
					label="Contraseña"
					hint={
						<Link className={linkClass} to="/forgot-password">
							¿Has olvidado tu contraseña?
						</Link>
					}
				>
					<Input name="password" type="password" autoComplete="current-password" required />
				</Field>

				{errorCode(password.error) === "Users.EmailNotVerified" ? (
					<Alert tone="warning">
						<p>Todavía no has verificado tu email. Revisa tu bandeja de entrada.</p>
						{resend.isSuccess ? (
							<p className="mt-2 font-medium">Te hemos enviado un nuevo enlace.</p>
						) : (
							<button
								type="button"
								className="mt-2 font-medium underline underline-offset-4"
								onClick={() => resend.mutate(password.variables?.email ?? "")}
							>
								Reenviar el email de verificación
							</button>
						)}
					</Alert>
				) : (
					<ErrorAlert error={password.error} />
				)}

				<Button type="submit" full loading={password.isPending}>
					Continuar
				</Button>
			</Form>

			<Divider />

			<div className="space-y-2">
				<Button variant="secondary" full loading={passkey.isPending} onClick={() => passkey.mutate()}>
					Usar una passkey
				</Button>
				{providers.data?.providers.map((provider) => (
					<a
						key={provider}
						className={buttonClass("secondary", true)}
						href={auth.externalUrl(provider, returnUrl)}
					>
						Continuar con {providerNames[provider] ?? provider}
					</a>
				))}
				<ErrorAlert error={passkey.error} />
			</div>
		</AuthLayout>
	)
}

function Divider() {
	return (
		<div className="flex items-center gap-3 text-xs text-zinc-400">
			<span className="h-px flex-1 bg-zinc-200" />o<span className="h-px flex-1 bg-zinc-200" />
		</div>
	)
}
