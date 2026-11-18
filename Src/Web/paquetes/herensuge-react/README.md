# @herensuge/react

SDK de Herensuge para SPAs React servidas por una BFF de Herensuge (`Herensuge.Bff` o una copia suya). La sesión
vive en una cookie `HttpOnly` de la BFF: ningún token llega al navegador y **no hay nada que configurar** (ni
URLs ni client id; todo es mismo origen).

## Instalar

```bash
# Dentro de este monorepo
pnpm --filter <tu-app> add @herensuge/react@workspace:*

# En otro repositorio: genera el paquete y añádelo (o publícalo en tu registro con `pnpm publish`)
pnpm --filter @herensuge/react build
pnpm --filter @herensuge/react pack          # → herensuge-react-0.1.0.tgz
pnpm add ./herensuge-react-0.1.0.tgz
```

Requiere React 19.

## Usar

```tsx
import { HerensugeProvider, RedirectToSignIn, useHerensuge, useUser } from "@herensuge/react"

createRoot(root).render(
	<HerensugeProvider>
		<App />
	</HerensugeProvider>,
)

function App() {
	const session = useHerensuge()
	if (session.status === "loading") return <p>Cargando…</p>
	if (session.status === "error") return <p>No se ha podido conectar.</p>
	if (session.status === "signedOut") return <RedirectToSignIn /> // o tu página pública
	return <Home />
}

function Home() {
	const user = useUser()
	return <a href={user.logoutUrl}>Cerrar la sesión de {user.name}</a>
}
```

Llamadas a tu API (a través de la BFF, que añade el access token):

```ts
import { herensugeFetch, signIn } from "@herensuge/react"

const response = await herensugeFetch("/api/v1/me") // añade X-CSRF: 1
if (response.status === 401) signIn()
```

## API

- `<HerensugeProvider>`: carga la sesión (`/bff/user`) y la vigila en tiempo real (`/bff/events`).
- `useHerensuge()`: la sesión, `{ status, user }`, con `status` = `loading`, `signedIn`, `signedOut` o `error`.
- `useUser()`: el usuario; lanza un error si no hay sesión.
- `<RedirectToSignIn />`: lleva al login y vuelve a la página actual.
- `signIn()` y `signOut(user)`: navegan a `/bff/login` y a `user.logoutUrl`.
- `reauthenticate()`: login reciente para operaciones sensibles (`max_age=0`).
- `switchTenant(tenantId)`: cambia de organización.
- `herensugeFetch(url, init)`: `fetch` con `X-CSRF: 1`.
- `useCan(permission)`, `<Can permission fallback>` y `can(user, permission)`: ¿puede el usuario hacer esa acción
  en esta aplicación?
- `useEntitled(feature)`: ¿incluye el plan de la organización esa funcionalidad?
- `user.profileFields`: los datos extra que esta aplicación pide al registrarse (`{ company: "Acme" }`), configurados en
  Plataforma → Clientes OIDC → Pantalla de login.
- `fetchUser()` y `watchSession(onSignedOut, onPermissionsChanged)`: lo mismo que hace el provider, sin React.

## Permisos

`user.permissions` son los permisos de tu aplicación (los de su manifiesto) que el usuario tiene sin condición;
`user.conditionalPermissions`, los que solo valen con una condición (por ejemplo, haber entrado con MFA). `useCan`
responde `true` en los dos casos: sirve para **mostrar u ocultar**, nunca para decidir. Decide siempre tu API (con
el SDK .NET `Herensuge.Access.AspNetCore` o preguntando al punto de decisión), que pedirá el _step-up_ si falta la
condición.

```tsx
import { Can, useCan } from "@herensuge/react"

function InvoiceActions({ approve }: { approve: () => void }) {
	const canEdit = useCan("invoice:edit")
	return (
		<>
			<Can permission="invoice:approve" fallback={<span>Sin permiso para aprobar</span>}>
				<button onClick={approve}>Aprobar</button>
			</Can>
			<button disabled={!canEdit}>Editar</button>
		</>
	)
}
```

Cuando alguien cambia los roles del usuario, la BFF refresca la sesión y emite `permissions-changed` por
`/bff/events`; el provider vuelve a pedir `/bff/user` y `useCan` responde con los permisos nuevos sin recargar.

## Cierre de sesión en tiempo real

Cuando una sesión se cierra —en esta pestaña o en otra, desde otro dispositivo en "Sesiones", o al cambiar la
contraseña— la BFF lo emite por `/bff/events` y `status` pasa a `signedOut` al momento. Es solo el aviso: el
acceso ya está cortado en el servidor (la API y el IdP comprueban la sesión en cada petición).

## Mantener

Todo el SDK es `src/index.tsx`. En el monorepo se consume desde `src/` (sin build); `pnpm build` genera `dist/`
(JS + tipos) con `tsc`, que es lo que se empaqueta (`publishConfig`).
