import tailwindcss from "@tailwindcss/vite"
import react from "@vitejs/plugin-react"
import { defineConfig } from "vite"

// Web de login, servida por Herensuge.Api (id.<dominio>).
// En desarrollo el navegador entra por https://localhost:5001: la API reenvía a este servidor las rutas
// de la SPA (Spa:DevServerUrl), así se usan los mismos orígenes y cookies que en producción.
export default defineConfig({
	plugins: [react(), tailwindcss()],
	envDir: "entorno",
	publicDir: "../../paquetes/componentes/public",
	server: { port: 5173, strictPort: true },
	preview: { port: 4173, strictPort: true },
	build: { target: "es2022", sourcemap: false },
})
