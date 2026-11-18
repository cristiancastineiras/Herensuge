import type { ClientLoginPage } from "@paquetes/api"
import { useSuspenseQuery } from "@tanstack/react-query"
import { useEffect } from "react"
import { auth } from "./api"
import { clientIdOf, rememberReturnUrl, rememberedReturnUrl, useReturnUrl } from "./navigation"

/** Sin aplicación (o si no responde): la pantalla de Herensuge, con todos los métodos y el registro abierto. */
const herensuge: ClientLoginPage = {
	clientId: "",
	displayName: "Herensuge",
	settings: {
		branding: { logo: null, primaryColor: null, termsUrl: null, privacyUrl: null, homeUrl: null },
		methods: { password: true, passkey: true, google: true, microsoft: true },
		allowRegistration: true,
		allowTenantCreation: true,
		profileFields: [],
	},
}

/**
 * Pantalla de login de la aplicación en la que se está entrando (client_id del returnUrl): marca, métodos de acceso y
 * campos del registro. Se configura en el portal: Plataforma → Clientes OIDC → Pantalla de login.
 * Suspende hasta tenerla, así no se ve un instante la de Herensuge.
 */
export function useLoginPage(): ClientLoginPage {
	const returnUrl = useReturnUrl()
	const clientId = clientIdOf(returnUrl ?? rememberedReturnUrl())

	useEffect(() => {
		if (returnUrl && clientIdOf(returnUrl)) {
			rememberReturnUrl(returnUrl)
		}
	}, [returnUrl])

	return useSuspenseQuery({
		queryKey: ["login-page", clientId],
		queryFn: () => (clientId ? auth.loginPage(clientId).catch(() => herensuge) : herensuge),
		staleTime: Number.POSITIVE_INFINITY,
	}).data
}
