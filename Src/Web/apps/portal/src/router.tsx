import { lazy } from "react"
import { Navigate, createBrowserRouter } from "react-router"
import {
	AppLayout,
	NotFoundPage,
	RequirePlatformAdmin,
	RequireTenantAdmin,
	RouteErrorPage,
} from "./AppLayout"

// Cada página se descarga al visitarla: la sección Plataforma no llega a los usuarios normales.
const ProfilePage = lazy(() => import("./account/ProfilePage"))
const SecurityPage = lazy(() => import("./account/SecurityPage"))
const SessionsPage = lazy(() => import("./account/SessionsPage"))
const OrganizationPage = lazy(() => import("./organization/OrganizationPage"))
const MembersPage = lazy(() => import("./organization/MembersPage"))
const OrganizationAuditPage = lazy(() => import("./organization/OrganizationAuditPage"))
const PlatformTenantsPage = lazy(() => import("./platform/PlatformTenantsPage"))
const ClientsPage = lazy(() => import("./platform/ClientsPage"))
const PlatformAuditPage = lazy(() => import("./platform/PlatformAuditPage"))

export const router = createBrowserRouter([
	{
		element: <AppLayout />,
		errorElement: <RouteErrorPage />,
		children: [
			// Mi cuenta (todos)
			{ index: true, element: <ProfilePage /> },
			{ path: "seguridad", element: <SecurityPage /> },
			{ path: "sesiones", element: <SessionsPage /> },

			// Organización (owner / admin)
			{
				path: "organizacion",
				element: <RequireTenantAdmin />,
				children: [
					{ index: true, element: <OrganizationPage /> },
					{ path: "miembros", element: <MembersPage /> },
					{ path: "auditoria", element: <OrganizationAuditPage /> },
				],
			},

			// Plataforma (admin de plataforma + passkey)
			{
				path: "plataforma",
				element: <RequirePlatformAdmin />,
				children: [
					{ index: true, element: <Navigate to="organizaciones" replace /> },
					{ path: "organizaciones", element: <PlatformTenantsPage /> },
					{ path: "clientes", element: <ClientsPage /> },
					{ path: "auditoria", element: <PlatformAuditPage /> },
				],
			},

			{ path: "*", element: <NotFoundPage /> },
		],
	},
])
