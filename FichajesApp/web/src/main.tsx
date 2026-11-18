import { HerensugeProvider } from "@herensuge/react"
import { StrictMode } from "react"
import { createRoot } from "react-dom/client"
import { App } from "./App"
import "./index.css"

// HerensugeProvider carga la sesión de la BFF (/bff/user) y la vigila en tiempo real (/bff/events).
createRoot(document.getElementById("root")!).render(
	<StrictMode>
		<HerensugeProvider>
			<App />
		</HerensugeProvider>
	</StrictMode>,
)
