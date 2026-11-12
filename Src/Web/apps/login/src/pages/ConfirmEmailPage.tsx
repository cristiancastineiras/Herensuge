import { Alert, ErrorAlert, Loading } from "@paquetes/componentes"
import { useSearchParams } from "react-router"
import { auth } from "../api"
import { AuthLayout } from "../AuthLayout"
import { portalUrl } from "../navigation"
import { useOneShot } from "./VerifyEmailPage"

/** Enlace del email de cambio de dirección: /confirm-email?token=… */
export function ConfirmEmailPage() {
	const token = useSearchParams()[0].get("token") ?? ""
	const confirm = useOneShot(() => auth.confirmEmailChange(token))

	return (
		<AuthLayout title="Confirmar el nuevo email">
			{confirm.isPending && <Loading label="Confirmando…" />}
			<ErrorAlert error={confirm.error} />
			{confirm.isSuccess && (
				<Alert tone="success">
					Tu email se ha cambiado. A partir de ahora inicia sesión con la nueva dirección.
					{portalUrl && (
						<>
							{" "}
							<a className="font-medium underline underline-offset-4" href={portalUrl}>
								Ir al portal
							</a>
						</>
					)}
				</Alert>
			)}
		</AuthLayout>
	)
}
