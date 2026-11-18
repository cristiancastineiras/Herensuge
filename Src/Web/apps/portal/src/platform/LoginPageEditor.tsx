import type { ClientResponse, LoginSettings, ProfileField, ProfileFieldType } from "@paquetes/api"
import {
	Button,
	Card,
	Checkbox,
	ErrorAlert,
	Field,
	Form,
	Input,
	Logo,
	Select,
	Textarea,
	brandStyle,
} from "@paquetes/componentes"
import { formValues } from "@paquetes/utiles"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { useState, type ChangeEvent, type ReactNode } from "react"
import { me, platform } from "../api"
import { QueryState } from "../components"

// Pantalla de login de una aplicación (un cliente OIDC): su marca, los métodos de acceso que ofrece y los datos que
// pide al registrarse. La web de login la aplica cuando el returnUrl es /connect/authorize?client_id=…

const maxLogoBytes = 100 * 1024
const maxFields = 15

const fieldTypes: Record<ProfileFieldType, string> = {
	text: "Texto",
	number: "Número",
	date: "Fecha",
	phone: "Teléfono",
	select: "Lista desplegable",
	checkbox: "Casilla (sí / no)",
}

/** Campo en edición: `id` solo para React; `autoKey` = la clave sigue al texto hasta que se edita a mano. */
type FieldDraft = ProfileField & { id: string; autoKey: boolean; optionsText: string }

export function LoginPageEditor({ client, onClose }: { client: ClientResponse; onClose: () => void }) {
	const page = useQuery({
		queryKey: ["login-page", client.clientId],
		queryFn: () => platform.loginPage(client.clientId),
	})

	return (
		<Card
			title={`Pantalla de login de ${client.displayName}`}
			description="Lo que ve quien entra o se registra en esta aplicación."
		>
			<QueryState query={page} />
			{page.data && (
				<LoginPageForm
					clientId={client.clientId}
					displayName={client.displayName}
					initial={page.data.settings}
					onClose={onClose}
				/>
			)}
		</Card>
	)
}

type LoginPageFormProps = {
	clientId: string
	displayName: string
	initial: LoginSettings
	onClose: () => void
}

function LoginPageForm({ clientId, displayName, initial, onClose }: LoginPageFormProps) {
	const queryClient = useQueryClient()
	const providers = useQuery({ queryKey: ["providers"], queryFn: me.providers })
	const configured = (provider: string) => providers.data?.providers.includes(provider) ?? true
	const [logo, setLogo] = useState(initial.branding.logo)
	const [logoError, setLogoError] = useState<string | null>(null)
	const [color, setColor] = useState(initial.branding.primaryColor)
	const [fields, setFields] = useState<FieldDraft[]>(() =>
		initial.profileFields.map((field) => ({
			...field,
			id: crypto.randomUUID(),
			autoKey: false,
			optionsText: field.options.join("\n"),
		})),
	)

	const save = useMutation({
		mutationFn: (settings: LoginSettings) => platform.updateLoginPage(clientId, settings),
		onSuccess: async () => {
			await queryClient.invalidateQueries({ queryKey: ["login-page", clientId] })
			onClose()
		},
	})

	const pickLogo = async (event: ChangeEvent<HTMLInputElement>) => {
		const file = event.currentTarget.files?.[0]
		event.currentTarget.value = ""
		if (!file) {
			return
		}

		setLogoError(file.size > maxLogoBytes ? "El logo no puede pasar de 100 KB." : null)
		if (file.size <= maxLogoBytes) {
			setLogo(await readAsDataUrl(file))
		}
	}

	const update = (id: string, change: Partial<FieldDraft>) =>
		setFields((current) => current.map((field) => (field.id === id ? { ...field, ...change } : field)))
	const move = (index: number, offset: number) =>
		setFields((current) => {
			const next = [...current]
			const [moved] = next.splice(index, 1)
			if (moved) {
				next.splice(index + offset, 0, moved)
			}
			return next
		})

	return (
		<Form
			onSubmit={(event) => {
				const values = formValues(
					event,
					"password",
					"passkey",
					"google",
					"microsoft",
					"allowRegistration",
					"allowTenantCreation",
					"homeUrl",
					"termsUrl",
					"privacyUrl",
				)
				save.mutate({
					branding: {
						logo,
						primaryColor: color,
						termsUrl: values.termsUrl.trim() || null,
						privacyUrl: values.privacyUrl.trim() || null,
						homeUrl: values.homeUrl.trim() || null,
					},
					methods: {
						password: values.password === "on",
						passkey: values.passkey === "on",
						google: values.google === "on",
						microsoft: values.microsoft === "on",
					},
					allowRegistration: values.allowRegistration === "on",
					allowTenantCreation: values.allowTenantCreation === "on",
					profileFields: fields.map(({ key, label, type, required, optionsText }) => ({
						key,
						label: label.trim(),
						type,
						required,
						options:
							type === "select"
								? optionsText
										.split("\n")
										.map((option) => option.trim())
										.filter(Boolean)
								: [],
					})),
				})
			}}
		>
			<Section title="Marca">
				<div className="grid gap-4 sm:grid-cols-[1fr_16rem]">
					<div className="space-y-4">
						<Field
							label="Logo"
							hint="PNG, JPEG, WebP o SVG de hasta 100 KB. Sustituye al de Herensuge y al nombre."
						>
							<Input
								type="file"
								accept="image/png,image/jpeg,image/webp,image/svg+xml"
								onChange={(event) => void pickLogo(event)}
							/>
						</Field>
						{logoError && <p className="text-sm text-red-700">{logoError}</p>}
						{logo && (
							<Button variant="ghost" onClick={() => setLogo(null)}>
								Quitar el logo
							</Button>
						)}
						<Field label="Color de los botones" hint="El texto encima se pone en negro o blanco solo.">
							<span className="flex items-center gap-2">
								<input
									type="color"
									value={color ?? "#18181b"}
									onChange={(event) => setColor(event.currentTarget.value)}
									className="h-9 w-14 cursor-pointer rounded-md border border-zinc-300 bg-white p-1"
								/>
								<span className="font-mono text-sm text-zinc-500">{color ?? "por defecto"}</span>
							</span>
						</Field>
						{color && (
							<Button variant="ghost" onClick={() => setColor(null)}>
								Volver al color por defecto
							</Button>
						)}
					</div>
					<Preview displayName={displayName} logo={logo} color={color} />
				</div>
				<Field
					label="Dirección de la aplicación"
					hint="URL HTTPS. Adónde se va tras aceptar una invitación o desde los emails (por ejemplo, https://fichajes.example.com)."
				>
					<Input name="homeUrl" type="url" defaultValue={initial.branding.homeUrl ?? ""} />
				</Field>
				<div className="grid gap-4 sm:grid-cols-2">
					<Field label="Condiciones de uso" hint="URL HTTPS. Se enlaza en el registro.">
						<Input name="termsUrl" type="url" defaultValue={initial.branding.termsUrl ?? ""} />
					</Field>
					<Field label="Política de privacidad" hint="URL HTTPS. Se enlaza en el registro.">
						<Input name="privacyUrl" type="url" defaultValue={initial.branding.privacyUrl ?? ""} />
					</Field>
				</div>
			</Section>

			<Section title="Inicio de sesión" description="Al menos uno. Se comprueba también al emitir el código.">
				<Checkbox name="password" label="Email y contraseña" defaultChecked={initial.methods.password} />
				<Checkbox name="passkey" label="Passkey" defaultChecked={initial.methods.passkey} />
				<Checkbox
					name="google"
					label="Google"
					hint={configured("google") ? undefined : "No está configurado en esta instalación: no se mostrará."}
					defaultChecked={initial.methods.google}
				/>
				<Checkbox
					name="microsoft"
					label="Microsoft"
					hint={
						configured("microsoft") ? undefined : "No está configurado en esta instalación: no se mostrará."
					}
					defaultChecked={initial.methods.microsoft}
				/>
			</Section>

			<Section
				title="Registro"
				description="Nombre, email y contraseña se piden siempre. Estos campos se añaden debajo; si a alguien le falta uno obligatorio (entró con Google o se registró en otra aplicación), se le pide antes de entrar."
			>
				<Checkbox
					name="allowRegistration"
					label="Permitir crear cuentas"
					hint="Desmárcalo y solo entrarán quienes ya tengan cuenta o una invitación."
					defaultChecked={initial.allowRegistration}
				/>
				<Checkbox
					name="allowTenantCreation"
					label="Permitir crear organizaciones"
					hint="Quien entra sin organización puede crear la suya (alta de una empresa nueva). Desmárcalo si las empresas las da de alta su administrador en el portal y su gente entra por invitación: verá «pide a tu empresa que te invite»."
					defaultChecked={initial.allowTenantCreation}
				/>
				{fields.map((field, index) => (
					<FieldEditor
						key={field.id}
						field={field}
						first={index === 0}
						last={index === fields.length - 1}
						onChange={(change) => update(field.id, change)}
						onMove={(offset) => move(index, offset)}
						onRemove={() => setFields((current) => current.filter((other) => other.id !== field.id))}
					/>
				))}
				{fields.length < maxFields && (
					<Button
						variant="secondary"
						onClick={() =>
							setFields((current) => [
								...current,
								{
									id: crypto.randomUUID(),
									key: "",
									label: "",
									type: "text",
									required: false,
									options: [],
									autoKey: true,
									optionsText: "",
								},
							])
						}
					>
						Añadir campo
					</Button>
				)}
			</Section>

			<ErrorAlert error={save.error} />
			<div className="flex gap-2">
				<Button type="submit" loading={save.isPending}>
					Guardar
				</Button>
				<Button variant="ghost" onClick={onClose}>
					Cancelar
				</Button>
			</div>
		</Form>
	)
}

type FieldEditorProps = {
	field: FieldDraft
	first: boolean
	last: boolean
	onChange: (change: Partial<FieldDraft>) => void
	onMove: (offset: number) => void
	onRemove: () => void
}

function FieldEditor({ field, first, last, onChange, onMove, onRemove }: FieldEditorProps) {
	return (
		<fieldset className="space-y-3 rounded-md border border-zinc-200 p-4">
			<legend className="px-1 text-sm font-medium text-zinc-800">{field.label || "Campo nuevo"}</legend>
			<div className="grid gap-3 sm:grid-cols-3">
				<Field label="Texto">
					<Input
						value={field.label}
						onChange={(event) => {
							const label = event.currentTarget.value
							onChange(field.autoKey ? { label, key: toKey(label) } : { label })
						}}
						placeholder="Empresa"
						maxLength={80}
						required
					/>
				</Field>
				<Field
					label="Clave"
					hint={
						field.autoKey
							? "En los tokens: profile_fields."
							: "Si la cambias, no se verán los datos guardados con la anterior."
					}
				>
					<Input
						value={field.key}
						onChange={(event) => onChange({ key: event.currentTarget.value, autoKey: false })}
						className="font-mono"
						placeholder="company"
						pattern="[a-z][a-z0-9_]*"
						maxLength={40}
						required
					/>
				</Field>
				<Field label="Tipo">
					<Select value={field.type} onChange={(event) => onChange({ type: event.currentTarget.value })}>
						{Object.entries(fieldTypes).map(([type, label]) => (
							<option key={type} value={type}>
								{label}
							</option>
						))}
					</Select>
				</Field>
			</div>
			{field.type === "select" && (
				<Field label="Opciones" hint="Una por línea.">
					<Textarea
						value={field.optionsText}
						onChange={(event) => onChange({ optionsText: event.currentTarget.value })}
						required
					/>
				</Field>
			)}
			<div className="flex flex-wrap items-center justify-between gap-2">
				<Checkbox
					label="Obligatorio"
					hint={field.type === "checkbox" ? "Hay que marcarla (p. ej. aceptar unas condiciones)." : undefined}
					checked={field.required}
					onChange={(event) => onChange({ required: event.currentTarget.checked })}
				/>
				<div className="flex gap-1">
					<Button variant="ghost" disabled={first} onClick={() => onMove(-1)} aria-label="Subir">
						↑
					</Button>
					<Button variant="ghost" disabled={last} onClick={() => onMove(1)} aria-label="Bajar">
						↓
					</Button>
					<Button variant="ghost" onClick={onRemove}>
						Quitar
					</Button>
				</div>
			</div>
		</fieldset>
	)
}

/** Cabecera del login con la marca: así se ve encima del formulario. */
function Preview({
	displayName,
	logo,
	color,
}: {
	displayName: string
	logo: string | null
	color: string | null
}) {
	return (
		<div
			aria-hidden="true"
			className="flex flex-col items-center justify-center gap-3 rounded-md border border-zinc-200 bg-zinc-50 p-4"
			style={brandStyle(color)}
		>
			<span className="text-xs text-zinc-400">Vista previa</span>
			{logo ? (
				<img src={logo} alt="" className="h-12 max-w-full object-contain" />
			) : (
				<>
					<Logo className="size-10 rounded-lg" />
					<span className="text-sm font-semibold text-zinc-900">{displayName}</span>
				</>
			)}
			<span className="bg-primary text-primary-foreground block w-full rounded-md px-3.5 py-2 text-center text-sm font-medium">
				Continuar
			</span>
		</div>
	)
}

function Section({
	title,
	description,
	children,
}: {
	title: string
	description?: string
	children: ReactNode
}) {
	return (
		<section className="space-y-3 border-t border-zinc-100 pt-4 first:border-t-0 first:pt-0">
			<div>
				<h3 className="text-sm font-semibold text-zinc-900">{title}</h3>
				{description && <p className="mt-0.5 text-sm text-zinc-500">{description}</p>}
			</div>
			{children}
		</section>
	)
}

/** "Tamaño de la empresa" → "tamano_de_la_empresa" (la clave que guarda el valor). */
function toKey(label: string): string {
	return label
		.normalize("NFD")
		.replace(/\p{Diacritic}/gu, "")
		.toLowerCase()
		.replace(/[^a-z0-9]+/g, "_")
		.replace(/^[^a-z]+/, "")
		.slice(0, 40)
		.replace(/_+$/, "")
}

function readAsDataUrl(file: File): Promise<string> {
	return new Promise((resolve, reject) => {
		const reader = new FileReader()
		reader.addEventListener("load", () => resolve(String(reader.result)))
		reader.addEventListener("error", () => reject(reader.error))
		reader.readAsDataURL(file)
	})
}
