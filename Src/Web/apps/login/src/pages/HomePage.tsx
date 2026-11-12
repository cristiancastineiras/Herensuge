import { Button, ErrorAlert, Loading, buttonClass } from "@paquetes/componentes"
import { useMutation, useQuery } from "@tanstack/react-query"
import { Navigate } from "react-router"
import { auth } from "../api"
import { AuthLayout } from "../AuthLayout"
import { portalUrl } from "../navigation"

/** Portada del IdP: quien entra directamente ve su sesión; si no hay sesión, va al login. */
export function HomePage() {
	const session = useQuery({ queryKey: ["session"], queryFn: auth.session })
	const signOut = useMutation({ mutationFn: auth.signOut, onSuccess: () => window.location.assign("/login") })

	if (session.isPending) {
		return (
			<AuthLayout title="Herensuge">
				<Loading />
			</AuthLayout>
		)
	}

	if (session.isError) {
		return (
			<AuthLayout title="Herensuge">
				<ErrorAlert error={session.error} />
			</AuthLayout>
		)
	}

	if (!session.data.signedIn) {
		return <Navigate to={session.data.mfaPending ? "/mfa" : "/login"} replace />
	}

	return (
		<AuthLayout
			title={`Hola, ${session.data.displayName ?? ""}`}
			description={`Has iniciado sesión como ${session.data.email}.`}
		>
			{portalUrl && (
				<a className={buttonClass("primary", true)} href={portalUrl}>
					Ir al portal
				</a>
			)}
			<ErrorAlert error={signOut.error} />
			<Button variant="secondary" full loading={signOut.isPending} onClick={() => signOut.mutate()}>
				Cerrar sesión
			</Button>
		</AuthLayout>
	)
}
