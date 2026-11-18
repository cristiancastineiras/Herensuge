# FichajesApp

Una aplicación de fichar (entrada, salida y horas trabajadas) protegida por completo con Herensuge. La aplicación es
mínima a propósito; lo que importa es cómo se conecta a Herensuge:

- **Inicio de sesión:** lo hace Herensuge, con su pantalla de login (y la marca y los campos de Fichajes).
- **Sesión:** la guarda una BFF (`Herensuge.Bff`). El navegador no ve nunca un token.
- **Permisos:** los declara el manifiesto `herensuge.access.json` y los asigna cada organización a sus miembros. La API
  los comprueba en cada petición.
- **Datos:** cada organización solo ve lo suyo.

| Pieza | Qué es |
|---|---|
| `herensuge.access.json` | Manifiesto: los permisos de Fichajes y dos roles (Empleado y Responsable) |
| `Fichajes.Api/` | API mínima en .NET 10 + PostgreSQL, con el SDK `Herensuge.Access.AspNetCore` |
| `web/` | Web en React (una página), con el SDK `@herensuge/react` |
| `Fichajes.Bff.Dockerfile` | La BFF: la misma `Herensuge.Bff` del portal, con la web de Fichajes |
| `compose.yaml` | Base de datos, API, BFF y Caddy (TLS) |

## Cómo encaja

```
Navegador ── https://fichajes.herensuge.localhost:8443 ──► Caddy ──► fichajes-bff (Herensuge.Bff + web)
                                                                     │  /bff/*  sesión: cookie HttpOnly; tokens en Valkey
                                                                     │  /api/*  + access token ──► fichajes-api ──► PostgreSQL
                                                                     ▼                                │
                               red de Herensuge:  herensuge-api (OIDC, userinfo, decisiones) ◄────────┘
                                                  herensuge-valkey (sesiones cerradas, permisos cambiados)
```

1. La web pide `/bff/user`. Sin sesión, va a `/bff/login`.
2. La BFF manda al usuario a Herensuge (`/connect/authorize` con `client_id=fichajes-app`, código + PKCE).
3. Herensuge le pide sus credenciales (y MFA, y la organización, si hace falta) y lo devuelve a `/bff/callback`.
4. La BFF canjea el código con su secreto por la red interna y guarda los tokens en Valkey. Al navegador solo le da
   una cookie `__Host-` `HttpOnly`.
5. Cada `/api/*` pasa por la BFF, que exige la cabecera `X-CSRF: 1` y añade el access token. `fichajes-api` valida el
   token (firma con el JWKS de Herensuge, emisor, audiencia `fichajes-api`) y el permiso de cada ruta.

## Pasos

### 0. Herensuge levantado y una cuenta de admin de plataforma

```bash
docker compose -f Docker/compose.yaml --profile observability up -d --build
```

Regístrate en https://portal.herensuge.localhost con el email de `BOOTSTRAP_ADMIN_EMAIL` (`Docker/.env`). Al verificarlo
eres admin de plataforma y ves el menú **Plataforma**. El enlace de verificación sale en el log:

```bash
docker compose -f Docker/compose.yaml logs herensuge-api | grep -A8 "\[EMAIL\]"
```

### 1. Publicar el manifiesto

Portal → **Plataforma → Aplicaciones → Sincronizar un manifiesto**: pega `FichajesApp/herensuge.access.json`.

Herensuge crea el scope `fichajes.api`, la audiencia `fichajes-api`, los cuatro permisos y los dos roles:

| Rol | Permisos | Lo tienen sin asignarlo |
|---|---|---|
| Empleado (`employee`) | `shift:create`, `shift:read` | Todos los miembros |
| Responsable (`manager`) | `shift:read-all`, `shift:delete` (`recent_auth`) | Propietarios y administradores de la organización |

En producción, el manifiesto lo publica la CI de la aplicación con su propio token (ver "En producción").

### 2. Crear el cliente OIDC

Portal → **Plataforma → Clientes OIDC → Nuevo cliente**:

| Campo | Valor |
|---|---|
| Client id | `fichajes-app` |
| Nombre visible | `Fichajes` |
| Cliente confidencial | Sí |
| Redirect URIs | `https://fichajes.herensuge.localhost:8443/bff/callback` |
| URIs tras cerrar sesión | `https://fichajes.herensuge.localhost:8443/bff/signout-callback` |
| Scopes | `openid profile email offline_access fichajes.api herensuge.access` |

Copia el **secreto**: solo se muestra una vez. Lo usan la BFF (para canjear el código) y la API (para preguntar al punto de
decisión con `herensuge.access`). Si se pierde: **Rotar secreto**.

### 3. La pantalla de login de Fichajes

En el mismo cliente → **Pantalla de login**:

- **Dirección de la aplicación:** `https://fichajes.herensuge.localhost:8443/`. Es adonde va el botón «Ir a Fichajes»
  cuando alguien acepta una invitación.
- **Permitir crear organizaciones:** **desmárcalo**. Así, quien entra sin empresa ve «Todavía no perteneces a ninguna
  organización: pide que te inviten» en lugar de «Crea tu organización». Ver [Empresas y trabajadores](#empresas-y-trabajadores).
- **Opcional:**
  - **Logo y color** de Fichajes.
  - **Métodos:** por ejemplo, solo email y contraseña y passkey.
  - **Campo extra:** etiqueta `Departamento` (la clave se genera sola: `departamento`), tipo texto, obligatorio. Quien se
    registre desde Fichajes lo rellena al registrarse. Quien ya tenía cuenta lo rellena la primera vez que entra
    (`/complete-profile`). La web lo muestra junto al nombre (`user.profileFields.departamento`).

### 4. Configurar los secretos

```bash
cp FichajesApp/.env.example FichajesApp/.env
```

| Variable | Valor |
|---|---|
| `POSTGRES_PASSWORD` | `openssl rand -base64 24` |
| `FICHAJES_CLIENT_SECRET` | El secreto del paso 2 |
| `HERENSUGE_VALKEY_PASSWORD` | La `VALKEY_PASSWORD` de `Docker/.env` |
| `BFF_KEY_RING_KEY` | `openssl rand -base64 32` (distinta de la del portal) |

### 5. Levantar Fichajes

```bash
docker compose -f FichajesApp/compose.yaml up -d --build
```

Abre **https://fichajes.herensuge.localhost:8443**. Este Caddy tiene su propia CA local, así que la primera vez acepta
el aviso del certificado. Te lleva al login de Herensuge y, al volver, ya puedes fichar.

### 6. Dar de alta una empresa y a su gente

Ver la sección siguiente: la empresa la crea su administrador en el portal y los trabajadores entran por invitación.

## Empresas y trabajadores

En Herensuge, una **organización** es una empresa cliente de Fichajes. Cada persona entra siempre en una organización, y
su papel en ella (propietario, administrador o miembro) decide qué rol de Fichajes tiene sin asignar nada:

| En la organización | En Fichajes (`grantTo` del manifiesto) |
|---|---|
| Propietario o administrador | Empleado + Responsable: ficha, ve a todo el equipo y corrige |
| Miembro | Empleado: ficha y ve lo suyo |

Así se monta una empresa:

1. **El administrador crea la empresa** en el portal (https://portal.herensuge.localhost). Al entrar por primera vez sin
   organización, el portal le pide crearla, y queda como propietario. Si hay más administradores, los invita con el rol
   «Administrador».
2. **Invita a los trabajadores** en el portal → **Organización → Miembros → Invitar**, con su email y el rol
   «Miembro». Cada uno recibe un email con el enlace.
3. **Cada trabajador abre el enlace**, inicia sesión o crea su cuenta con ese email, pulsa **Aceptar invitación** y luego
   **Ir a Fichajes**. Entra como miembro de la empresa, sin crear nada.

Si un trabajador entra en Fichajes **sin invitación**, no puede crear una empresa propia: ve «Todavía no perteneces a
ninguna organización, pide que te inviten», porque «Permitir crear organizaciones» está desmarcado (paso 3). Cuando acepta la
invitación, ya entra.

Una persona puede estar en varias empresas. Al entrar, Herensuge le pregunta con cuál, y la web de Fichajes solo ve los
fichajes de esa.

## Qué probar

| Prueba | Qué tiene que pasar |
|---|---|
| Entrar sin sesión | La web te lleva al login de Herensuge y vuelve |
| Fichar entrada y salida | La jornada aparece en "Mis fichajes" con las horas |
| Alguien sin invitación | Se registra desde Fichajes y ve «pide que te inviten»; no puede crear una empresa |
| Un miembro (no admin) | Invítalo (portal → Organización → Miembros), acepta, «Ir a Fichajes»: ficha, pero no ve "Equipo" (y `/api/shifts/team` le da `403`) |
| Un responsable | Propietario o admin: ve "Equipo" con los fichajes de toda la organización |
| Borrar un fichaje | Si iniciaste sesión hace más de 10 minutos, pide la contraseña y vuelve. Después vuelve a pulsar **Borrar** |
| Quitar el rol en vivo | Portal → Organización → Roles/Miembros: "Equipo" desaparece en Fichajes sin recargar |
| Cerrar la sesión desde otro sitio | Portal → Mi cuenta → Sesiones → cerrar: Fichajes vuelve al login al momento |
| Otra organización | Crea otra y entra con ella: no ve ningún fichaje de la primera |
| La API sin la BFF | No tiene puertos publicados. Un token de otra aplicación (otra audiencia) recibe `401` |
| `/api` sin `X-CSRF: 1` | La BFF responde `403` |
| Trazas | Grafana (http://127.0.0.1:3000) → Explore → Tempo: `fichajes-bff` → `fichajes-api` → `herensuge-api` |

## Qué protege cada pieza

| Riesgo | Cómo se cubre |
|---|---|
| Robo de tokens (XSS) | Los tokens viven en la BFF (Valkey, cifrados con Data Protection). El navegador solo tiene una cookie `__Host-`, `HttpOnly`, `Secure`, `SameSite=Strict` |
| CSRF | La BFF exige `X-CSRF: 1` en `/api/*` (`herensugeFetch` la pone) |
| Tokens falsos o de otra aplicación | `AddHerensugeAccess` valida firma (JWKS), emisor y audiencia `fichajes-api` |
| Acciones sin permiso | `.RequirePermission(...)` en cada ruta. La web solo esconde botones (`<Can>`, `useCan`); decide la API |
| Ver datos de otra organización | Todas las consultas filtran por el `tenant_id` del token |
| Acciones sensibles con una sesión vieja | `shift:delete` pide `recent_auth`: el SDK responde `401` con el reto de step-up (RFC 9470) y la web llama a `reauthenticate()` |
| Sesión cerrada en otro sitio | Herensuge lo publica en Valkey. La BFF borra la sesión y la web se entera por `/bff/events` |
| Permisos retirados | La BFF ve los contadores de versión en Valkey, refresca la sesión y avisa a la web (`permissions-changed`) |
| Nombre falso en los fichajes | La API pide el nombre a Herensuge (`GetUserInfoAsync`, userinfo) con el token del usuario; no lo acepta del navegador |
| Herensuge no responde | El SDK deniega (*fail closed*) |
| Exposición de la API | Sin puertos publicados: solo la alcanza la BFF por la red interna |

## Dónde está la integración en el código

- **API:** [`Fichajes.Api/Program.cs`](Fichajes.Api/Program.cs).
  - `AddHerensugeAccess` lee la sección `Herensuge`: `Authority`, `ResourceServer`, `ClientId`, `ClientSecret` y
    `BackchannelAddress`.
  - El grupo de rutas exige `tenant_id`; cada ruta lleva su `.RequirePermission`.
  - Al fichar la entrada, el nombre sale de `GetUserInfoAsync`.
- **Web:**
  - [`web/src/main.tsx`](web/src/main.tsx): `<HerensugeProvider>`.
  - [`web/src/App.tsx`](web/src/App.tsx): `useHerensuge`, `RedirectToSignIn`, `useUser`, `<Can>`, `useCan` y `signOut`.
  - [`web/src/api.ts`](web/src/api.ts): `herensugeFetch`, y `reauthenticate()` o `signIn()` según el `401`.
- **BFF:** la configuración está en [`compose.yaml`](compose.yaml): `Bff__*`, el destino de `/api` y
  `Bff__InstanceName=fichajes-bff`. Ese nombre es el prefijo de su estado en el Valkey compartido, distinto del portal.

## En producción

- **Dominios y certificados:** cambia `FICHAJES_HOST` y `HERENSUGE_ID_HOST` por los reales. Caddy pide los
  certificados a Let's Encrypt. Actualiza las redirect URIs del cliente.
- **API y BFF fuera de la red de Docker de Herensuge:** quita los dos `BackchannelAddress`; las llamadas irán a la URL
  pública. Con otro Valkey, la BFF funciona igual, pero sin el aviso al momento de sesiones cerradas y permisos
  cambiados (llega al refrescar el token).
- **Manifiesto desde la CI**, con el token de la propia aplicación:

  ```bash
  TOKEN=$(curl -s https://id.example.com/connect/token \
    -d grant_type=client_credentials -d scope=herensuge.access \
    -d client_id=fichajes-app -d client_secret="$SECRET" | jq -r .access_token)
  curl -X PUT https://id.example.com/access/v1/manifest \
    -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
    --data @FichajesApp/herensuge.access.json
  ```

- **Base de datos:** la API crea la tabla al arrancar (`EnsureCreated`). Con cambios de esquema, usa migraciones de
  EF Core.

## Fuera de este repositorio

- **API:** el SDK .NET se instala como paquete NuGet (`Herensuge.Access.AspNetCore`) en lugar de `ProjectReference`.
- **Web:** `@herensuge/react` se instala con `pnpm add` (ver su README) en lugar de `link:`.
- **BFF:** la imagen `herensuge-bff` sirve si se monta la web en `/app/wwwroot`.
