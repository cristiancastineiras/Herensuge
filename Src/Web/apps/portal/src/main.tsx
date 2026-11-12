import { ApiError } from "@paquetes/api"
import { QueryClient, QueryClientProvider } from "@tanstack/react-query"
import { StrictMode } from "react"
import { createRoot } from "react-dom/client"
import { RouterProvider } from "react-router/dom"
import { router } from "./router"
import "./index.css"

const queryClient = new QueryClient({
	defaultOptions: {
		queries: {
			refetchOnWindowFocus: false,
			// Solo se reintentan los fallos de red o del servidor, nunca un 4xx.
			retry: (count, error) =>
				count < 2 && !(error instanceof ApiError && error.status >= 400 && error.status < 500),
		},
	},
})

createRoot(document.getElementById("root") as HTMLElement).render(
	<StrictMode>
		<QueryClientProvider client={queryClient}>
			<RouterProvider router={router} />
		</QueryClientProvider>
	</StrictMode>,
)
