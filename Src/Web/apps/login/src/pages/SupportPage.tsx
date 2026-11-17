import { Alert, Button, ErrorAlert } from "@paquetes/componentes"
import { useMutation } from "@tanstack/react-query"
import { useEffect, useState } from "react"
import { redeemSupportLink } from "../api"
import { AuthLayout } from "../AuthLayout"
import { portalUrl } from "../navigation"

/**
 * Enlace de una sesión de soporte (plan D11): /support#token. El token va en el fragmento, que el navegador nunca
 * envía, y se quita de la barra de direcciones al cargar. Hay que abrirlo en una ventana privada (sin otra sesión).
 */
export function SupportPage() {
	const [token] = useState(() => window.location.hash.slice(1))
	useEffect(() => {
		if (token) {
			window.history.replaceState(null, "", "/support")
		}
	}, [token])

	const redeem = useMutation({
		mutationFn: () => redeemSupportLink(token),
		onSuccess: () => window.location.assign(portalUrl ?? "/"),
	})

	if (!token) {
		return (
			<AuthLayout title="Enlace de soporte no válido">
				<Alert tone="error">Pide un enlace nuevo desde Plataforma → Soporte.</Alert>
			</AuthLayout>
		)
	}

	return (
		<AuthLayout
			title="Sesión de soporte"
			description="Vas a entrar en la cuenta de un usuario en modo solo lectura durante 30 minutos. El usuario recibe un aviso por email."
		>
			<Alert tone="warning">
				Usa una ventana privada. Confirma con la passkey de la cuenta de administrador que pidió el enlace.
			</Alert>
			<ErrorAlert error={redeem.error} />
			<Button full loading={redeem.isPending} onClick={() => redeem.mutate()}>
				Confirmar con mi passkey
			</Button>
		</AuthLayout>
	)
}
