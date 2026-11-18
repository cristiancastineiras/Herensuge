import type { ProfileField } from "@paquetes/api"
import { Checkbox, Field, Input, Select } from "@paquetes/componentes"
import type { ComponentProps } from "react"

// Campos extra que pide cada aplicación al registrarse (y en /complete-profile si faltan). Inputs nativos: el
// navegador valida el tipo y los obligatorios; la API vuelve a comprobarlo todo.

const prefix = "profile."

const inputs: Record<string, ComponentProps<"input">> = {
	text: { type: "text", maxLength: 200 },
	number: { type: "number", step: "any", inputMode: "decimal" },
	date: { type: "date" },
	phone: { type: "tel", autoComplete: "tel", maxLength: 25 },
}

/** Autocompletado para las claves habituales; el resto, a criterio del navegador. */
const autoComplete: Record<string, string> = {
	company: "organization",
	empresa: "organization",
	organization: "organization",
	job_title: "organization-title",
	cargo: "organization-title",
	country: "country-name",
	pais: "country-name",
	city: "address-level2",
	ciudad: "address-level2",
	birthdate: "bday",
	fecha_nacimiento: "bday",
}

const noValues: Record<string, string> = {}

export function ProfileFieldInputs({
	fields,
	values = noValues,
}: {
	fields: ProfileField[]
	/** Valores que ya tiene el usuario. */
	values?: Record<string, string>
}) {
	return fields.map((field) => <ProfileFieldInput key={field.key} field={field} value={values[field.key]} />)
}

function ProfileFieldInput({ field, value }: { field: ProfileField; value: string | undefined }) {
	const name = prefix + field.key
	if (field.type === "checkbox") {
		return (
			<Checkbox name={name} label={field.label} required={field.required} defaultChecked={value === "true"} />
		)
	}

	return (
		<Field label={field.required ? field.label : `${field.label} (opcional)`}>
			{field.type === "select" ? (
				<Select name={name} required={field.required} defaultValue={value ?? ""}>
					<option value="">Elige una opción</option>
					{field.options.map((option) => (
						<option key={option}>{option}</option>
					))}
				</Select>
			) : (
				<Input
					name={name}
					required={field.required}
					defaultValue={value ?? ""}
					autoComplete={autoComplete[field.key] ?? "on"}
					{...inputs[field.type]}
				/>
			)}
		</Field>
	)
}

/** Los valores de los campos extra de un formulario: { company: "Acme", terms: "true" }. */
export function readProfileFields(form: HTMLFormElement, fields: ProfileField[]): Record<string, string> {
	const data = new FormData(form)
	return Object.fromEntries(
		fields.map((field) => {
			const value = data.get(prefix + field.key)
			if (field.type === "checkbox") {
				return [field.key, String(value === "on")]
			}

			return [field.key, typeof value === "string" ? value : ""]
		}),
	)
}
