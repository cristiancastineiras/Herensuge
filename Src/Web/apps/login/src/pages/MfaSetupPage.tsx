import { TotpSetup } from "@paquetes/componentes"
import { auth } from "../api"
import { AuthLayout } from "../AuthLayout"
import { continueTo, useReturnUrl } from "../navigation"

/** Alta de la app autenticadora cuando una organización exige MFA y el usuario aún no lo tiene. */
export function MfaSetupPage() {
	const returnUrl = useReturnUrl()

	return (
		<AuthLayout
			title="Activa la verificación en dos pasos"
			description="Tu organización la exige para continuar."
		>
			<TotpSetup begin={auth.beginTotp} confirm={auth.confirmTotp} onDone={() => continueTo(returnUrl)} />
		</AuthLayout>
	)
}
