import { describeErrorCode } from "@paquetes/api"
import { Alert, buttonClass } from "@paquetes/componentes"
import { Link, useSearchParams } from "react-router"
import { AuthLayout } from "../AuthLayout"

const home = (
	<Link className={buttonClass("secondary", true)} to="/">
		Volver al inicio
	</Link>
)

/** /error?error=… : errores del flujo de inicio de sesión. */
export function ErrorPage() {
	const code = useSearchParams()[0].get("error")

	return (
		<AuthLayout title="No se ha podido continuar">
			<Alert tone="error">{code ? describeErrorCode(code) : "Ha ocurrido un error."}</Alert>
			{home}
		</AuthLayout>
	)
}

export function NotFoundPage() {
	return (
		<AuthLayout title="Página no encontrada" description="El enlace no existe o ha cambiado.">
			{home}
		</AuthLayout>
	)
}

/** Error inesperado al pintar una ruta (por ejemplo, tras desplegar una versión nueva). */
export function RouteErrorPage() {
	return (
		<AuthLayout title="Algo ha fallado" description="Recarga la página para volver a intentarlo.">
			<button type="button" className={buttonClass("primary", true)} onClick={() => window.location.reload()}>
				Recargar
			</button>
		</AuthLayout>
	)
}
