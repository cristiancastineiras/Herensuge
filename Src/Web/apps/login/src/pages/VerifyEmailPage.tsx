import { Alert, ErrorAlert, Loading, buttonClass } from "@paquetes/componentes"
import { useMutation } from "@tanstack/react-query"
import { useEffect, useRef } from "react"
import { Link, useSearchParams } from "react-router"
import { auth } from "../api"
import { AuthLayout } from "../AuthLayout"

/** Enlace del email de registro: /verify-email?token=… */
export function VerifyEmailPage() {
	const token = useSearchParams()[0].get("token") ?? ""
	const verify = useOneShot(() => auth.verifyEmail(token))

	return (
		<AuthLayout title="Verificar email">
			{verify.isPending && <Loading label="Verificando…" />}
			<ErrorAlert error={verify.error} />
			{verify.isSuccess && <Alert tone="success">Tu email está verificado. Ya puedes iniciar sesión.</Alert>}
			{!verify.isPending && (
				<Link className={buttonClass("primary", true)} to="/login">
					Iniciar sesión
				</Link>
			)}
		</AuthLayout>
	)
}

/**
 * Lanza una petición una sola vez al abrir la página (los tokens de los emails son de un solo uso y
 * StrictMode ejecuta los efectos dos veces en desarrollo).
 */
export function useOneShot(action: () => Promise<unknown>) {
	const mutation = useMutation({ mutationFn: action })
	const started = useRef(false)
	useEffect(() => {
		if (!started.current) {
			started.current = true
			mutation.mutate()
		}
	}, [mutation])

	return { ...mutation, isPending: mutation.isPending || mutation.isIdle }
}
