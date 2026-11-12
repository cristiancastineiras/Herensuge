import tailwindcss from "@tailwindcss/vite"
import react from "@vitejs/plugin-react"
import { defineConfig } from "vite"

// Portal (Mi cuenta, Organización, Plataforma), servido por Herensuge.Bff (portal.<dominio>).
// En desarrollo el navegador entra por https://localhost:5002: la BFF reenvía a este servidor las rutas
// de la SPA (Spa:DevServerUrl), así se usan los mismos orígenes y cookies que en producción.
export default defineConfig({
	plugins: [react(), tailwindcss()],
	envDir: "entorno",
	publicDir: "../../paquetes/componentes/public",
	server: { port: 5174, strictPort: true },
	preview: { port: 4174, strictPort: true },
	build: { target: "es2022", sourcemap: false },
})
