import { Alert, Button, ErrorAlert, Loading, buttonClass } from "@paquetes/componentes"
import { formatDate } from "@paquetes/utiles"
import { useMutation, useQuery } from "@tanstack/react-query"
import { Link, useParams } from "react-router"
import { auth } from "../api"
import { AuthLayout } from "../AuthLayout"
import { portalUrl, withReturnUrl } from "../navigation"
import { roleLabels } from "./TenantPages"

/** Enlace del email de invitación: /invitations/{token} */
export function InvitationPage() {
	const token = useParams().token ?? ""
	const preview = useQuery({ queryKey: ["invitation", token], queryFn: () => auth.invitation(token) })
	const session = useQuery({ queryKey: ["session"], queryFn: auth.session })
	const accept = useMutation({ mutationFn: () => auth.acceptInvitation(token) })

	if (preview.isPending || session.isPending) {
		return (
			<AuthLayout title="Invitación">
				<Loading />
			</AuthLayout>
		)
	}

	if (preview.isError || !preview.data.isValid) {
		return (
			<AuthLayout title="Invitación no válida">
				{preview.isError ? (
					<ErrorAlert error={preview.error} />
				) : (
					<Alert tone="error">La invitación ya se ha usado, se ha revocado o ha caducado.</Alert>
				)}
			</AuthLayout>
		)
	}

	const invitation = preview.data
	const here = `/invitations/${encodeURIComponent(token)}`

	if (accept.isSuccess) {
		return (
			<AuthLayout title={`Bienvenido a ${invitation.tenantName}`}>
				<Alert tone="success">Ya eres miembro de la organización.</Alert>
				{portalUrl && (
					<a className={buttonClass("primary", true)} href={portalUrl}>
						Ir al portal
					</a>
				)}
			</AuthLayout>
		)
	}

	return (
		<AuthLayout
			title={`Únete a ${invitation.tenantName}`}
			description={`Te han invitado como ${(roleLabels[invitation.role] ?? invitation.role).toLowerCase()} con el email ${invitation.email}. Caduca el ${formatDate(invitation.expiresAtUtc)}.`}
		>
			{session.data?.signedIn ? (
				<>
					<p className="text-sm text-zinc-600">
						Has iniciado sesión como <strong>{session.data.email}</strong>.
					</p>
					<ErrorAlert error={accept.error} />
					<Button full loading={accept.isPending} onClick={() => accept.mutate()}>
						Aceptar invitación
					</Button>
				</>
			) : (
				<>
					<p className="text-sm text-zinc-600">
						Inicia sesión o crea una cuenta con {invitation.email} para aceptarla.
					</p>
					<Link className={buttonClass("primary", true)} to={withReturnUrl("/login", here)}>
						Iniciar sesión
					</Link>
					<Link className={buttonClass("secondary", true)} to={withReturnUrl("/register", here)}>
						Crear cuenta
					</Link>
				</>
			)}
		</AuthLayout>
	)
}
