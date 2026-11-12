# Despliegue con Docker

`compose.yaml` levanta el stack completo detrás de **Caddy**, que termina el TLS. HTTPS es obligatorio: las cookies
`__Host-` y OpenIddict lo exigen.

```
Internet ──443──► Caddy ──► herensuge-api :8080  (id.<dominio>)
                        └─► herensuge-bff :8080  (portal.<dominio>) ──► herensuge-api (red interna)
                                    PostgreSQL 17 · Valkey 8 · migrador (una vez por despliegue)
```

| Servicio | Imagen | Notas |
|---|---|---|
| `herensuge-postgres` | `postgres:17-alpine` | Volumen `postgres-data`; puerto solo en `127.0.0.1` |
| `herensuge-valkey` | `valkey/valkey:8-alpine` | Con contraseña y AOF (las sesiones sobreviven a reinicios) |
| `herensuge-migrator` | `herensuge-api` | Aplica las migraciones y termina; la API arranca después |
| `herensuge-api` | `Herensuge.Api.Dockerfile` | Imagen *chiseled* sin shell y sin root; certificados OIDC en el volumen `oidc-keys` |
| `herensuge-bff` | `Herensuge.Bff.Dockerfile` | Habla con la API por la red interna (`Bff:BackchannelAddress`) |
| `herensuge-proxy` | `caddy:2.8-alpine` | Certificados Let's Encrypt automáticos; CA interna para `*.localhost` |
| `herensuge-otel` | `grafana/otel-lgtm` | Opcional (`--profile observability`): Grafana en `127.0.0.1:3000` |

## 1. Preparar la configuración

```bash
cp Docker/.env.example Docker/.env
```

Cambia **todos** los secretos (`openssl rand -base64 32`) y define los nombres públicos:

| Variable | Máquina local | Servidor |
|---|---|---|
| `HERENSUGE_ID_HOST` | `id.localhost` | `id.example.com` |
| `HERENSUGE_PORTAL_HOST` | `portal.localhost` | `portal.example.com` |
| `PASSKEYS_RP_ID` | `localhost` | `example.com` |
| `HERENSUGE_ENVIRONMENT` | `Staging` | `Staging` hasta tener un proveedor real de email/SMS |

En un servidor, los dos nombres deben resolver por DNS a la máquina, con los puertos 80 y 443 abiertos.

## 2. Arrancar

```bash
docker compose -f Docker/compose.yaml up -d --build
docker compose -f Docker/compose.yaml logs -f herensuge-migrator herensuge-api
```

Comprobaciones:

```bash
curl https://id.example.com/.well-known/openid-configuration
curl https://id.example.com/health/ready
```

En local con `*.localhost`, confía en la CA interna de Caddy en la máquina del navegador:

```bash
docker compose -f Docker/compose.yaml cp herensuge-proxy:/data/caddy/pki/authorities/local/root.crt ./caddy-root.crt
# Importar caddy-root.crt como entidad de certificación raíz de confianza (sistema o navegador).
```

## 3. Emails y SMS mientras no haya proveedor

Con el proveedor `Log`, los mensajes (enlaces de verificación, códigos SMS…) se escriben en el log de la API:

```bash
docker compose -f Docker/compose.yaml logs herensuge-api | grep -A8 "\[EMAIL\]"
```

`HERENSUGE_ENVIRONMENT=Production` **no arranca** con `Log`: hay que implementar antes un proveedor real
(una clase que implemente `IEmailSender` o `ISmsSender` en `Herensuge.Modules.Notifications.Infrastructure`).

## 4. Copias de seguridad

| Volumen | Contenido | Si se pierde |
|---|---|---|
| `postgres-data` | Todo el estado: usuarios, organizaciones, tokens, auditoría, claves de Data Protection | Pérdida total |
| `oidc-keys` | Certificados de firma y cifrado OIDC | Todos los tokens y refresh tokens dejan de valer |
| `valkey-data` | Sesiones de la BFF y su key ring | Los usuarios del portal vuelven a iniciar sesión |
| `caddy-data` | Certificados TLS | Caddy los vuelve a pedir |

## 5. Actualizar

```bash
git pull
docker compose -f Docker/compose.yaml up -d --build
```

El migrador aplica las migraciones nuevas antes de que arranque la nueva API.

## 6. Rotar las claves OIDC

Los certificados autogenerados duran 2 años. Para rotarlos, sustituye `signing.pfx` y `encryption.pfx` en el volumen
`oidc-keys` (mismo `OIDC_KEYS_PASSWORD`) y reinicia la API. Los refresh tokens emitidos con la clave anterior dejan de valer.

## 7. Escalar

La API y la BFF no guardan estado local: varias réplicas funcionan con el mismo PostgreSQL y Valkey. Los jobs de
outbox/inbox usan `FOR UPDATE SKIP LOCKED`. El único límite por instancia es el rate limiting de login (en memoria).
