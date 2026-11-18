import { Button, ErrorAlert, Form, Loading } from "@paquetes/componentes"
import { useMutation, useQuery } from "@tanstack/react-query"
import { Navigate } from "react-router"
import { auth } from "../api"
import { AuthLayout } from "../AuthLayout"
import { useLoginPage } from "../loginPage"
import { continueTo, useReturnUrl, withReturnUrl } from "../navigation"
import { ProfileFieldInputs, readProfileFields } from "../ProfileFields"

/**
 * /complete-profile?returnUrl=…: la aplicación pide datos que el usuario aún no ha dado (entró con Google o Microsoft,
 * o se registró en otra aplicación del SaaS). /connect/authorize redirige aquí hasta que estén.
 */
export function CompleteProfilePage() {
	const returnUrl = useReturnUrl()
	const page = useLoginPage()
	const fields = page.settings.profileFields
	const session = useQuery({ queryKey: ["session"], queryFn: auth.session })
	const save = useMutation({ mutationFn: auth.updateProfileFields, onSuccess: () => continueTo(returnUrl) })

	if (session.isPending) {
		return (
			<AuthLayout title="Completa tu perfil">
				<Loading />
			</AuthLayout>
		)
	}

	if (session.data && !session.data.signedIn) {
		return <Navigate to={withReturnUrl("/login", returnUrl)} replace />
	}

	return (
		<AuthLayout
			title="Completa tu perfil"
			description={`${page.displayName} necesita algunos datos más para continuar.`}
		>
			<ErrorAlert error={session.error} />
			<Form
				onSubmit={(event) => {
					event.preventDefault()
					if (!page.clientId) {
						continueTo(returnUrl)
						return
					}

					save.mutate({ clientId: page.clientId, values: readProfileFields(event.currentTarget, fields) })
				}}
			>
				<ProfileFieldInputs fields={fields} values={session.data?.profileFields} />
				<ErrorAlert error={save.error} />
				<Button type="submit" full loading={save.isPending}>
					Guardar y continuar
				</Button>
			</Form>
		</AuthLayout>
	)
}
