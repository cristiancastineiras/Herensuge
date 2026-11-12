# Webs de Herensuge

Monorepo pnpm con las dos SPAs React del Nivel 1. Misma forma que `plantilla-react-monorepo`: `apps/` +
`paquetes/`, Turborepo, Vite 8, Tailwind v4, oxlint y oxfmt.

| App           | La sirve                     | URL local (Docker)                   | URL local (`dotnet run`) | Qué tiene                                                                                          |
| ------------- | ---------------------------- | ------------------------------------ | ------------------------ | -------------------------------------------------------------------------------------------------- |
| `apps/login`  | `Herensuge.Api` (`wwwroot/`) | `https://id.herensuge.localhost`     | `https://localhost:5001` | Login, MFA, passkey, registro, verificar email, recuperar contraseña, organizaciones, invitaciones |
| `apps/portal` | `Herensuge.Bff` (`wwwroot/`) | `https://portal.herensuge.localhost` | `https://localhost:5002` | Mi cuenta · Organización (owner/admin) · Plataforma (admin + passkey)                              |

## Comandos

```bash
pnpm install          # una vez (Node 24, pnpm 10)
pnpm dev              # Vite: login :5173, portal :5174 (se abren a través de los hosts .NET, ver abajo)
pnpm build            # dist/ de las dos apps
pnpm typecheck        # TypeScript en todo el monorepo
pnpm lint             # oxlint
pnpm format           # oxfmt (pnpm format:check en CI)
pnpm check            # typecheck + lint + format:check
pnpm api:generate     # contrato OpenAPI (dotnet build) → tipos TypeScript de la API
```

## Desarrollo

El navegador **no** entra por Vite, sino por los hosts .NET, que reenvían las rutas de la SPA a Vite
(`Spa:DevServerUrl`). Así se usan los mismos orígenes, cookies y redirecciones que en producción.

```bash
docker compose -f Docker/compose.dev.yaml up -d       # PostgreSQL + Valkey
pnpm -C Src/Web dev                                    # Vite
dotnet run --project Src/Hosts/Herensuge.Api           # https://localhost:5001 → login
dotnet run --project Src/Hosts/Herensuge.Bff           # https://localhost:5002 → portal
```

Abre `https://localhost:5002`. Los emails (verificación, recuperación, invitaciones) salen en la consola de la API.

## Estructura

```
apps/
├── login/src/
│   ├── main.tsx · router.tsx        # entrada y rutas (las que pide Herensuge.Api)
│   ├── api.ts                       # llamadas a /api/v1/auth
│   ├── navigation.ts                # returnUrl (solo rutas locales)
│   ├── AuthLayout.tsx               # marco de todas las pantallas
│   └── pages/                       # una pantalla (o un par relacionado) por fichero
└── portal/src/
    ├── main.tsx · router.tsx        # rutas; cada página se carga lazy
    ├── api.ts                       # /bff/*, /api/v1/me, /tenants, /platform; 401 → login
    ├── user.ts                      # usuario de la sesión (/bff/user) y reglas de rol
    ├── AppLayout.tsx                # cabecera, navegación por rol, guardas
    ├── components.tsx               # PageHeader, AuditLog…
    ├── account/                     # Perfil, Seguridad, Sesiones
    ├── organization/                # General, Miembros, Auditoría
    └── platform/                    # Organizaciones, Clientes OIDC, Auditoría
paquetes/
├── api/            # cliente tipado (openapi-fetch + X-CSRF), ProblemDetails → español; src/schema.d.ts GENERADO
├── componentes/    # Button, Field, Card, Alert, Table, ConfirmButton, TotpSetup… (+ public/logo.svg)
├── utiles/         # cn, formValues, formato de fechas
├── generador-api/  # openapi-typescript (con su propio TypeScript 5): Docs/API → api/src/schema.d.ts
└── configuracion-ts/
```

Los paquetes se consumen desde `src/` (sin build propio): Vite y TypeScript los resuelven por `exports`.

## Reglas

- **CSP estricta** (`script-src 'self'; style-src 'self'`): nada de `style={{…}}` ni scripts inline. Solo clases de Tailwind.
- **Ningún token en el navegador**: el portal solo conoce `/bff/user`; nada de auth en `localStorage`.
- **Toda petición** lleva `X-CSRF: 1` (lo pone `createHttpClient`).
- **Estado de servidor** en TanStack Query; estado de UI en `useState`. Sin store global.
- **Formularios** nativos + `formValues(event, "campo"…)`. La validación real la hace la API; los errores
  (`code` de ProblemDetails) se traducen en `paquetes/api/src/errors.ts`.
- **Tipos de la API generados, nunca a mano.** Cada `dotnet build` de `Herensuge.Api` escribe el contrato
  `Docs/API/Herensuge.Api.json`; `pnpm api:generate` lo regenera y crea `paquetes/api/src/schema.d.ts`. El cliente
  (`createApiClient`) comprueba al compilar rutas, parámetros, cuerpos y respuestas. La CI falla si el contrato o los
  tipos no están al día. `types.ts` solo da nombres cortos a los esquemas (y define el contrato de la BFF, `/bff/user`).
- **Rutas por rol** solo como comodidad: la autorización real la aplica siempre la API.
- Código en inglés; interfaz en español.

## Añadir…

- **Una pantalla al portal**: crea `src/<sección>/MiPagina.tsx` con `export default`, añádela en `router.tsx`
  (con `lazy`) y en la navegación de `AppLayout.tsx`.
- **Un endpoint**: en el backend, declara su respuesta con `.Produces<T>()`; luego `pnpm api:generate` y una línea en
  el `api.ts` de la app: `call(api.GET("/api/v1/…"))`. TypeScript avisa si la ruta o el cuerpo no existen.
- **Un error nuevo de la API**: su texto en `paquetes/api/src/errors.ts`.
- **Una variable de entorno**: en `apps/<app>/entorno/.env.*` y declarada en `src/vite-env.d.ts`.

## Variables de entorno

| App    | Variable          | Para qué                                      | Docker                                         |
| ------ | ----------------- | --------------------------------------------- | ---------------------------------------------- |
| login  | `VITE_URL_PORTAL` | Botón "Ir al portal"                          | `https://${HERENSUGE_PORTAL_HOST}` (build arg) |
| portal | `VITE_URL_IDP`    | Vincular Google/Microsoft (navegación al IdP) | `https://${HERENSUGE_ID_HOST}` (build arg)     |

Son públicas (acaban en el bundle). En desarrollo salen de `entorno/.env.desarrollo`.

## Docker

`Docker/Herensuge.Api.Dockerfile` y `Docker/Herensuge.Bff.Dockerfile` compilan su web en una etapa Node y copian
`dist/` a `wwwroot/`. No hace falta compilar nada a mano antes de `docker compose up --build`.
