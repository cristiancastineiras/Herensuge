// @paquetes/utiles: helpers sin dependencias.

/** Une clases CSS ignorando los valores falsos: cn("a", activo && "b"). */
export function cn(...classes: Array<string | false | null | undefined>): string {
	return classes.filter(Boolean).join(" ")
}

/**
 * Lee los campos de un formulario al enviarlo y evita la recarga de la página.
 *   onSubmit={(event) => signIn.mutate(formValues(event, "email", "password"))}
 * Un checkbox marcado vale "on"; uno sin marcar, "".
 */
export function formValues<K extends string>(
	event: { preventDefault(): void; currentTarget: HTMLFormElement },
	...names: K[]
): Record<K, string> {
	event.preventDefault()
	const data = new FormData(event.currentTarget)
	const values = {} as Record<K, string>
	for (const name of names) {
		const value = data.get(name)
		values[name] = typeof value === "string" ? value : ""
	}

	return values
}

const dateTime = new Intl.DateTimeFormat("es-ES", { dateStyle: "medium", timeStyle: "short" })
const date = new Intl.DateTimeFormat("es-ES", { dateStyle: "medium" })

/** "12 nov 2026, 21:48" */
export function formatDateTime(iso: string | null | undefined): string {
	return iso ? dateTime.format(new Date(iso)) : "—"
}

/** "12 nov 2026" */
export function formatDate(iso: string | null | undefined): string {
	return iso ? date.format(new Date(iso)) : "—"
}
