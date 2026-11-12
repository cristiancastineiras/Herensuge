import type { CurrentUserResponse, PasskeyResponse } from "@paquetes/api"
import {
	Alert,
	Badge,
	Button,
	Card,
	ConfirmButton,
	ErrorAlert,
	Field,
	Form,
	Input,
	RecoveryCodes,
	TotpSetup,
	buttonClass,
} from "@paquetes/componentes"
import { formValues, formatDate, formatDateTime } from "@paquetes/utiles"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { useState } from "react"
import { addPasskey, me, reauthenticate } from "../api"
import { PageHeader, QueryState } from "../components"
import { idpUrl, signedInRecently, useUser } from "../user"

/** Invalida los datos del usuario tras un cambio de seguridad. */
function useRefreshProfile() {
	const queryClient = useQueryClient()
	return () => queryClient.invalidateQueries({ queryKey: ["me"] })
}

export default function SecurityPage() {
	const user = useUser()
	const profile = useQuery({ queryKey: ["me"], queryFn: me.get })

	return (
		<>
			<PageHeader
				title="Seguridad"
				description="Contraseña, verificación en dos pasos, passkeys y cuentas vinculadas."
			/>
			{!signedInRecently(user) && (
				<Alert tone="neutral">
					Por seguridad, para hacer cambios aquí tendrás que confirmar tu identidad.{" "}
					<button
						type="button"
						className="font-medium underline underline-offset-4"
						onClick={() => reauthenticate()}
					>
						Confirmar ahora
					</button>
				</Alert>
			)}
			<QueryState query={profile} />
			{profile.data && (
				<>
					<PasswordCard hasPassword={profile.data.hasPassword} />
					<TotpCard profile={profile.data} />
					<PhoneCard phoneNumber={profile.data.phoneNumber} />
				</>
			)}
			<PasskeysCard />
			<ExternalLoginsCard />
		</>
	)
}

function PasswordCard({ hasPassword }: { hasPassword: boolean }) {
	const change = useMutation({ mutationFn: me.changePassword })
	const [mismatch, setMismatch] = useState(false)

	return (
		<Card
			title="Contraseña"
			description={
				hasPassword ? undefined : "Todavía no tienes contraseña: entras con passkey o con Google/Microsoft."
			}
		>
			{change.isSuccess ? (
				<Alert tone="success">Contraseña cambiada. Hemos cerrado tus otras sesiones.</Alert>
			) : (
				<Form
					onSubmit={(event) => {
						const { currentPassword, newPassword, repeat } = formValues(
							event,
							"currentPassword",
							"newPassword",
							"repeat",
						)
						setMismatch(newPassword !== repeat)
						if (newPassword === repeat) {
							change.mutate({ currentPassword: hasPassword ? currentPassword : null, newPassword })
						}
					}}
				>
					{hasPassword && (
						<Field label="Contraseña actual">
							<Input name="currentPassword" type="password" autoComplete="current-password" required />
						</Field>
					)}
					<div className="grid gap-4 sm:grid-cols-2">
						<Field label="Contraseña nueva" hint="Mínimo 12 caracteres.">
							<Input
								name="newPassword"
								type="password"
								autoComplete="new-password"
								minLength={12}
								maxLength={128}
								required
							/>
						</Field>
						<Field label="Repite la contraseña nueva">
							<Input name="repeat" type="password" autoComplete="new-password" required />
						</Field>
					</div>
					{mismatch && <Alert tone="error">Las contraseñas no coinciden.</Alert>}
					<ErrorAlert error={change.error} />
					<Button type="submit" loading={change.isPending}>
						{hasPassword ? "Cambiar contraseña" : "Crear contraseña"}
					</Button>
				</Form>
			)}
		</Card>
	)
}

function TotpCard({ profile }: { profile: CurrentUserResponse }) {
	const refresh = useRefreshProfile()
	const [settingUp, setSettingUp] = useState(false)
	const disable = useMutation({ mutationFn: me.disableTotp, onSuccess: refresh })
	const regenerate = useMutation({ mutationFn: me.regenerateRecoveryCodes, onSuccess: refresh })

	return (
		<Card
			title="App autenticadora"
			description="Códigos de 6 dígitos de Google Authenticator, Microsoft Authenticator, 1Password…"
			actions={profile.totpEnabled ? <Badge tone="success">Activada</Badge> : <Badge>Desactivada</Badge>}
		>
			{settingUp ? (
				<TotpSetup
					begin={me.beginTotp}
					confirm={me.confirmTotp}
					doneLabel="Hecho"
					onDone={() => {
						setSettingUp(false)
						refresh()
					}}
				/>
			) : profile.totpEnabled ? (
				<>
					<p className="text-sm text-zinc-600">
						Te quedan {profile.recoveryCodesLeft} códigos de recuperación sin usar.
					</p>
					{regenerate.data && <RecoveryCodes codes={regenerate.data.recoveryCodes} />}
					<ErrorAlert error={regenerate.error ?? disable.error} />
					<div className="flex flex-wrap gap-2">
						<ConfirmButton
							question="Los códigos anteriores dejarán de valer."
							confirmLabel="Generar nuevos"
							loading={regenerate.isPending}
							onConfirm={() => regenerate.mutate()}
						>
							Nuevos códigos de recuperación
						</ConfirmButton>
						<ConfirmButton
							question="¿Desactivar la app autenticadora?"
							confirmLabel="Desactivar"
							loading={disable.isPending}
							onConfirm={() => disable.mutate()}
						>
							Desactivar
						</ConfirmButton>
					</div>
				</>
			) : (
				<Button onClick={() => setSettingUp(true)}>Activar</Button>
			)}
		</Card>
	)
}

function PhoneCard({ phoneNumber }: { phoneNumber: string | null }) {
	const refresh = useRefreshProfile()
	const start = useMutation({ mutationFn: me.startPhoneVerification })
	const confirm = useMutation({ mutationFn: me.confirmPhone, onSuccess: refresh })
	const remove = useMutation({ mutationFn: me.removePhone, onSuccess: refresh })

	return (
		<Card
			title="Teléfono (SMS)"
			description="Alternativa para recibir el código de verificación por SMS."
			actions={phoneNumber ? <Badge tone="success">Verificado</Badge> : undefined}
		>
			{confirm.data && confirm.data.recoveryCodes.length > 0 && (
				<RecoveryCodes codes={confirm.data.recoveryCodes} />
			)}
			{phoneNumber ? (
				<div className="flex flex-wrap items-center justify-between gap-3">
					<span className="font-mono text-sm">{phoneNumber}</span>
					<ConfirmButton
						question="¿Eliminar el teléfono?"
						confirmLabel="Eliminar"
						loading={remove.isPending}
						onConfirm={() => remove.mutate()}
					>
						Eliminar
					</ConfirmButton>
				</div>
			) : start.isSuccess ? (
				<Form onSubmit={(event) => confirm.mutate(formValues(event, "code").code)}>
					<Alert tone="success">
						Te hemos enviado un SMS a {start.variables}. El código caduca en 10 minutos.
					</Alert>
					<Field label="Código de 6 dígitos">
						<Input
							name="code"
							inputMode="numeric"
							autoComplete="one-time-code"
							pattern="[0-9]{6}"
							maxLength={6}
							required
							autoFocus
						/>
					</Field>
					<ErrorAlert error={confirm.error} />
					<div className="flex gap-2">
						<Button type="submit" loading={confirm.isPending}>
							Verificar
						</Button>
						<Button variant="ghost" onClick={() => start.reset()}>
							Cambiar número
						</Button>
					</div>
				</Form>
			) : (
				<Form
					onSubmit={(event) => start.mutate(formValues(event, "phoneNumber").phoneNumber.replaceAll(" ", ""))}
				>
					<Field label="Teléfono" hint="Con prefijo internacional, por ejemplo +34600111222.">
						<Input name="phoneNumber" type="tel" autoComplete="tel" placeholder="+34600111222" required />
					</Field>
					<ErrorAlert error={start.error} />
					<Button type="submit" variant="secondary" loading={start.isPending}>
						Enviar código
					</Button>
				</Form>
			)}
			<ErrorAlert error={remove.error} />
		</Card>
	)
}

function PasskeysCard() {
	const queryClient = useQueryClient()
	const passkeys = useQuery({ queryKey: ["passkeys"], queryFn: me.passkeys })
	const refresh = () => queryClient.invalidateQueries({ queryKey: ["passkeys"] })
	const add = useMutation({ mutationFn: addPasskey, onSuccess: refresh })

	return (
		<Card
			title="Passkeys"
			description="Entra sin contraseña con la huella, la cara o el PIN de tu dispositivo. Cuentan como verificación en dos pasos."
		>
			<QueryState query={passkeys} />
			{passkeys.data && passkeys.data.length > 0 && (
				<ul className="divide-y divide-zinc-100 rounded-md border border-zinc-200">
					{passkeys.data.map((passkey) => (
						<PasskeyRow key={passkey.id} passkey={passkey} onChange={refresh} />
					))}
				</ul>
			)}
			<form
				className="flex flex-wrap items-end gap-2"
				onSubmit={(event) => {
					const form = event.currentTarget
					add.mutate(formValues(event, "name").name.trim(), { onSuccess: () => form.reset() })
				}}
			>
				<Field label="Nombre (opcional)">
					<Input name="name" placeholder="Portátil del trabajo" maxLength={60} className="w-64" />
				</Field>
				<Button type="submit" variant="secondary" loading={add.isPending}>
					Añadir passkey
				</Button>
			</form>
			<ErrorAlert error={add.error} />
		</Card>
	)
}

function PasskeyRow({ passkey, onChange }: { passkey: PasskeyResponse; onChange: () => void }) {
	const [editing, setEditing] = useState(false)
	const rename = useMutation({
		mutationFn: (name: string) => me.renamePasskey(passkey.id, name),
		onSuccess: () => {
			setEditing(false)
			onChange()
		},
	})
	const remove = useMutation({ mutationFn: () => me.removePasskey(passkey.id), onSuccess: onChange })

	return (
		<li className="space-y-2 px-4 py-3">
			{editing ? (
				<form
					className="flex flex-wrap gap-2"
					onSubmit={(event) => rename.mutate(formValues(event, "name").name.trim())}
				>
					<Input
						name="name"
						defaultValue={passkey.name}
						maxLength={60}
						required
						autoFocus
						className="w-64"
						aria-label="Nombre"
					/>
					<Button type="submit" loading={rename.isPending}>
						Guardar
					</Button>
					<Button variant="ghost" onClick={() => setEditing(false)}>
						Cancelar
					</Button>
				</form>
			) : (
				<div className="flex flex-wrap items-center justify-between gap-3">
					<div>
						<p className="text-sm font-medium text-zinc-900">{passkey.name}</p>
						<p className="text-xs text-zinc-500">
							Añadida el {formatDate(passkey.createdAtUtc)} · Último uso:{" "}
							{formatDateTime(passkey.lastUsedAtUtc)}
						</p>
					</div>
					<div className="flex gap-2">
						<Button variant="ghost" onClick={() => setEditing(true)}>
							Renombrar
						</Button>
						<ConfirmButton
							question="¿Eliminar?"
							confirmLabel="Eliminar"
							loading={remove.isPending}
							onConfirm={() => remove.mutate()}
						>
							Eliminar
						</ConfirmButton>
					</div>
				</div>
			)}
			<ErrorAlert error={rename.error ?? remove.error} />
		</li>
	)
}

const providerNames: Record<string, string> = { google: "Google", microsoft: "Microsoft" }

/** Vincular es una navegación al IdP, que vuelve a esta página cuando termina. */
const linkUrl = (provider: string) =>
	`${idpUrl}/api/v1/auth/external/${provider}?mode=link&returnUrl=${encodeURIComponent(`${window.location.origin}/seguridad`)}`

function ExternalLoginsCard() {
	const queryClient = useQueryClient()
	const linked = useQuery({ queryKey: ["external-logins"], queryFn: me.externalLogins })
	const providers = useQuery({ queryKey: ["providers"], queryFn: me.providers })
	const unlink = useMutation({
		mutationFn: me.unlinkExternalLogin,
		onSuccess: () => queryClient.invalidateQueries({ queryKey: ["external-logins"] }),
	})

	const linkedProviders = new Set(linked.data?.map((login) => login.provider))
	const available = (providers.data?.providers ?? []).filter((provider) => !linkedProviders.has(provider))

	return (
		<Card title="Cuentas vinculadas" description="Entra también con Google o Microsoft.">
			<QueryState query={linked} />
			{linked.data?.length === 0 && (
				<p className="text-sm text-zinc-500">No tienes cuentas externas vinculadas.</p>
			)}
			<ul className="divide-y divide-zinc-100">
				{linked.data?.map((login) => (
					<li
						key={login.provider}
						className="flex flex-wrap items-center justify-between gap-3 py-3 first:pt-0"
					>
						<div>
							<p className="text-sm font-medium text-zinc-900">
								{providerNames[login.provider] ?? login.provider}
							</p>
							<p className="text-xs text-zinc-500">
								{login.email ?? "Sin email"} · desde el {formatDate(login.linkedAtUtc)}
							</p>
						</div>
						<ConfirmButton
							question="¿Desvincular?"
							confirmLabel="Desvincular"
							onConfirm={() => unlink.mutate(login.provider)}
						>
							Desvincular
						</ConfirmButton>
					</li>
				))}
			</ul>
			<ErrorAlert error={unlink.error} />
			{idpUrl && available.length > 0 && (
				<div className="flex flex-wrap gap-2">
					{available.map((provider) => (
						<a key={provider} className={buttonClass("secondary")} href={linkUrl(provider)}>
							Vincular {providerNames[provider] ?? provider}
						</a>
					))}
				</div>
			)}
			{providers.isSuccess && providers.data.providers.length === 0 && (
				<p className="text-xs text-zinc-500">No hay proveedores externos configurados en este entorno.</p>
			)}
		</Card>
	)
}
