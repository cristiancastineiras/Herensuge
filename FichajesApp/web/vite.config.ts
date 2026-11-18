import tailwindcss from "@tailwindcss/vite"
import react from "@vitejs/plugin-react"
import { defineConfig } from "vite"

// Web de Fichajes. La sirve su BFF (Herensuge.Bff) desde wwwroot: /bff/* es la sesión y /api/* va a Fichajes.Api,
// todo en el mismo origen y sin tokens en el navegador.
export default defineConfig({
	plugins: [react(), tailwindcss()],
	// @herensuge/react se enlaza desde el repositorio (link:): una sola copia de React.
	resolve: { dedupe: ["react", "react-dom"] },
	build: { target: "es2022", sourcemap: false },
})
