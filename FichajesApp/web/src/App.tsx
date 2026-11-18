import { Can, RedirectToSignIn, signOut, useCan, useHerensuge, useUser } from "@herensuge/react"
import { useCallback, useEffect, useState } from "react"
import { api, type Shift } from "./api"

export function App() {
	const session = useHerensuge()
	switch (session.status) {
		case "loading":
			return <Message>Cargando…</Message>
		case "error":
			return <Message>No se ha podido conectar. Recarga la página.</Message>
		case "signedOut":
			// Sin sesión no hay nada que ver: al login de Herensuge, y de vuelta aquí.
			return <RedirectToSignIn />
		case "signedIn":
			return <Home />
	}
}

function Home() {
	const user = useUser()
	// Dato extra que Fichajes pide al registrarse (Pantalla de login del cliente fichajes-app).
	const department = user.profileFields.departamento

	return (
		<main className="mx-auto max-w-3xl space-y-8 px-4 py-8">
			<header className="flex flex-wrap items-center justify-between gap-3">
				<h1 className="text-2xl font-semibold">Fichajes</h1>
				<div className="flex items-center gap-3 text-sm">
					<span className="text-zinc-600">
						{user.name}
						{department ? ` · ${department}` : ""}
					</span>
					<button
						type="button"
						onClick={() => signOut(user)}
						className="rounded-lg border border-zinc-300 px-3 py-1.5 hover:bg-zinc-100"
					>
						Cerrar sesión
					</button>
				</div>
			</header>
			{user.actor && (
				<p className="rounded-lg bg-amber-100 px-4 py-2 text-sm text-amber-900">
					Sesión de soporte: estás viendo Fichajes como {user.name}, solo lectura.
				</p>
			)}
			<MyShifts />
			{/* Solo para mostrar u ocultar: quien decide es Fichajes.Api. */}
			<Can permission="shift:read-all">
				<TeamShifts />
			</Can>
		</main>
	)
}

function MyShifts() {
	const { shifts, error, setError, reload } = useShifts("/mine")
	const canClock = useCan("shift:create")
	const [busy, setBusy] = useState(false)
	const open = shifts?.find((shift) => shift.clockOut === null)

	async function clock() {
		setBusy(true)
		setError(null)
		try {
			await api("/clock", { method: "POST" })
			await reload()
		} catch (exception) {
			setError((exception as Error).message)
		} finally {
			setBusy(false)
		}
	}

	return (
		<section className="space-y-4">
			<div className="flex flex-wrap items-center justify-between gap-4 rounded-xl border border-zinc-200 bg-white p-5">
				<div>
					<p className="text-lg font-semibold">{open ? "Estás trabajando" : "Fuera"}</p>
					<p className="text-sm text-zinc-600">
						{open
							? `Has entrado a las ${time.format(new Date(open.clockIn))}.`
							: "Ficha la entrada al empezar."}
					</p>
				</div>
				{canClock && (
					<button
						type="button"
						onClick={clock}
						disabled={!shifts || busy}
						className="rounded-lg bg-zinc-900 px-5 py-3 font-medium text-white hover:bg-zinc-700 disabled:opacity-50"
					>
						{open ? "Fichar salida" : "Fichar entrada"}
					</button>
				)}
			</div>
			<ErrorMessage error={error} />
			<ShiftTable id="mis-fichajes" title="Mis fichajes" shifts={shifts} />
		</section>
	)
}

function TeamShifts() {
	const { shifts, error, setError, reload } = useShifts("/team")
	const canDelete = useCan("shift:delete")

	async function remove(shift: Shift) {
		if (!window.confirm(`¿Borrar la jornada de ${shift.name} del ${day.format(new Date(shift.clockIn))}?`)) {
			return
		}

		setError(null)
		try {
			await api(`/${shift.id}`, { method: "DELETE" })
			await reload()
		} catch (exception) {
			setError((exception as Error).message)
		}
	}

	return (
		<section className="space-y-4">
			<ErrorMessage error={error} />
			<ShiftTable
				id="equipo"
				title="Equipo · últimos 7 días"
				shifts={shifts}
				withNames
				onDelete={canDelete ? remove : undefined}
			/>
			{canDelete && (
				<p className="text-xs text-zinc-500">
					Para borrar un fichaje hay que haber iniciado sesión hace menos de 10 minutos: si no, se pide la
					contraseña.
				</p>
			)}
		</section>
	)
}

function useShifts(path: string) {
	const [shifts, setShifts] = useState<Shift[]>()
	const [error, setError] = useState<string | null>(null)
	const reload = useCallback(
		() => api<Shift[]>(path).then(setShifts, (exception: Error) => setError(exception.message)),
		[path],
	)

	useEffect(() => {
		void reload()
	}, [reload])

	return { shifts, error, setError, reload }
}

function ShiftTable({
	id,
	title,
	shifts,
	withNames = false,
	onDelete,
}: {
	id: string
	title: string
	shifts: Shift[] | undefined
	withNames?: boolean
	onDelete?: (shift: Shift) => void
}) {
	const columns = 4 + (withNames ? 1 : 0) + (onDelete ? 1 : 0)
	const cell = "px-4 py-2 whitespace-nowrap"

	return (
		// Con scroll horizontal en pantallas estrechas; el caption da nombre a la región.
		<section aria-labelledby={id} className="overflow-x-auto rounded-xl border border-zinc-200 bg-white">
			<table className="w-full text-left text-sm">
				<caption id={id} className="px-4 pt-4 pb-2 text-left text-base font-semibold">
					{title}
				</caption>
				<thead className="text-zinc-500">
					<tr>
						{withNames && (
							<th scope="col" className={`${cell} font-medium`}>
								Persona
							</th>
						)}
						<th scope="col" className={`${cell} font-medium`}>
							Día
						</th>
						<th scope="col" className={`${cell} font-medium`}>
							Entrada
						</th>
						<th scope="col" className={`${cell} font-medium`}>
							Salida
						</th>
						<th scope="col" className={`${cell} font-medium`}>
							Tiempo
						</th>
						{onDelete && (
							<th scope="col" className={cell}>
								<span className="sr-only">Acciones</span>
							</th>
						)}
					</tr>
				</thead>
				<tbody>
					{!shifts && (
						<tr>
							<td colSpan={columns} className="px-4 py-6 text-zinc-500">
								Cargando…
							</td>
						</tr>
					)}
					{shifts?.length === 0 && (
						<tr>
							<td colSpan={columns} className="px-4 py-6 text-zinc-500">
								Todavía no hay fichajes.
							</td>
						</tr>
					)}
					{shifts?.map((shift) => (
						<tr key={shift.id} className="border-t border-zinc-100">
							{withNames && (
								<th scope="row" className={`${cell} font-medium`}>
									{shift.name}
								</th>
							)}
							<td className={cell}>{day.format(new Date(shift.clockIn))}</td>
							<td className={cell}>
								<time dateTime={shift.clockIn}>{time.format(new Date(shift.clockIn))}</time>
							</td>
							<td className={cell}>
								{shift.clockOut ? (
									<time dateTime={shift.clockOut}>{time.format(new Date(shift.clockOut))}</time>
								) : (
									"—"
								)}
							</td>
							<td className={cell}>{worked(shift.minutes)}</td>
							{onDelete && (
								<td className={`${cell} text-right`}>
									<button
										type="button"
										onClick={() => onDelete(shift)}
										className="text-red-700 hover:underline"
									>
										Borrar
									</button>
								</td>
							)}
						</tr>
					))}
				</tbody>
			</table>
		</section>
	)
}

function ErrorMessage({ error }: { error: string | null }) {
	return error ? (
		<p role="alert" className="rounded-lg bg-red-50 px-4 py-2 text-sm text-red-800">
			{error}
		</p>
	) : null
}

function Message({ children }: { children: string }) {
	return <p className="p-8 text-center text-zinc-600">{children}</p>
}

const day = new Intl.DateTimeFormat("es-ES", { weekday: "short", day: "numeric", month: "short" })
const time = new Intl.DateTimeFormat("es-ES", { hour: "2-digit", minute: "2-digit" })
// Baseline desde 2025: los navegadores anteriores ven "7 h 30 min".
const duration = "DurationFormat" in Intl ? new Intl.DurationFormat("es-ES", { style: "short" }) : null

/** Minutos trabajados (calculados por la API) como "7 h, 30 min"; una jornada abierta está "en curso". */
function worked(minutes: number | null): string {
	if (minutes === null) {
		return "En curso"
	}

	if (minutes < 1) {
		return "Menos de 1 min"
	}

	const parts = { hours: Math.floor(minutes / 60), minutes: minutes % 60 }
	return duration ? duration.format(parts) : `${parts.hours} h ${parts.minutes} min`
}
