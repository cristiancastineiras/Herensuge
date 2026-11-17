import { Alert, Button, Card, ErrorAlert, Field, Form, Input } from "@paquetes/componentes"
import { formValues, formatDateTime } from "@paquetes/utiles"
import { useMutation } from "@tanstack/react-query"
import { support } from "../api"
import { PageHeader } from "../components"

/**
 * Sesiones de soporte (plan D11): solo lectura, 30 minutos, con aviso por email al usuario. El enlace caduca en 5
 * minutos y solo lo canjea tu passkey, en una ventana privada.
 */
export default function SupportPage() {
	const start = useMutation({ mutationFn: support.start })

	return (
		<>
			<PageHeader
				title="Soporte"
				description="Entra en la cuenta de un usuario para ver lo que ve él, sin poder cambiar nada."
			/>
			<Card title="Abrir una sesión de soporte">
				<Form
					onSubmit={(event) => {
						const { email, reason } = formValues(event, "email", "reason")
						start.mutate({ email: email.trim(), reason: reason.trim() })
					}}
				>
					<Field label="Email del usuario">
						<Input name="email" type="email" required />
					</Field>
					<Field label="Motivo" hint="Lo recibe el usuario por email y queda en la auditoría.">
						<Input
							name="reason"
							required
							maxLength={500}
							placeholder="p. ej. ticket #1234: no ve sus facturas"
						/>
					</Field>
					<ErrorAlert error={start.error} />
					<Button type="submit" loading={start.isPending}>
						Crear enlace
					</Button>
				</Form>
				{start.data && (
					<Alert tone="warning">
						<p>
							Ábrelo en una <strong>ventana privada</strong> y confirma con tu passkey antes de las{" "}
							{formatDateTime(start.data.expiresAtUtc)}. No lo compartas.
						</p>
						<code className="mt-2 block font-mono break-all text-zinc-900">{start.data.url}</code>
					</Alert>
				)}
			</Card>
			<Card title="Durante la sesión">
				<ul className="list-disc space-y-1 pl-5 text-sm text-zinc-600">
					<li>Todo es de solo lectura: cualquier cambio responde «Support.ReadOnly».</li>
					<li>Los permisos que exigen MFA, inicio de sesión reciente o red de confianza se deniegan.</li>
					<li>Solo funciona en el portal; las demás aplicaciones la rechazan.</li>
					<li>Termina al cerrar sesión o a los 30 minutos.</li>
				</ul>
			</Card>
		</>
	)
}
