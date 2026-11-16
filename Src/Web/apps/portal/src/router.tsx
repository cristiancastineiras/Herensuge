import { lazy } from "react"
import { Navigate, createBrowserRouter } from "react-router"
import {
	AppLayout,
	NotFoundPage,
	RequirePermission,
	RequirePlatformAdmin,
	RequireTenantAdmin,
	RouteErrorPage,
} from "./AppLayout"
import { Permissions } from "./user"

// Cada página se descarga al visitarla: la sección Plataforma no llega a los usuarios normales.
const ProfilePage = lazy(() => import("./account/ProfilePage"))
const SecurityPage = lazy(() => import("./account/SecurityPage"))
const SessionsPage = lazy(() => import("./account/SessionsPage"))
const MyAccessPage = lazy(() => import("./account/MyAccessPage"))
const OrganizationPage = lazy(() => import("./organization/OrganizationPage"))
const MembersPage = lazy(() => import("./organization/MembersPage"))
const RolesPage = lazy(() => import("./organization/RolesPage"))
const GroupsPage = lazy(() => import("./organization/GroupsPage"))
const ServiceAccountsPage = lazy(() => import("./organization/ServiceAccountsPage"))
const SimulatorPage = lazy(() => import("./organization/SimulatorPage"))
const AccessRequestsPage = lazy(() => import("./organization/AccessRequestsPage"))
const OrganizationAuditPage = lazy(() => import("./organization/OrganizationAuditPage"))
const PlatformTenantsPage = lazy(() => import("./platform/PlatformTenantsPage"))
const ClientsPage = lazy(() => import("./platform/ClientsPage"))
const ApplicationsPage = lazy(() => import("./platform/ApplicationsPage"))
const PlansPage = lazy(() => import("./platform/PlansPage"))
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
			{ path: "mis-accesos", element: <MyAccessPage /> },

			// Organización: general y miembros (owner / admin); roles y auditoría, por permiso
			{
				path: "organizacion",
				children: [
					{
						element: <RequireTenantAdmin />,
						children: [
							{ index: true, element: <OrganizationPage /> },
							{ path: "miembros", element: <MembersPage /> },
						],
					},
					{
						element: <RequirePermission permission={Permissions.manageRoles} />,
						children: [
							{ path: "roles", element: <RolesPage /> },
							{ path: "grupos", element: <GroupsPage /> },
							{ path: "simulador", element: <SimulatorPage /> },
						],
					},
					{
						element: <RequirePermission permission={Permissions.manageServiceAccounts} />,
						children: [{ path: "cuentas-de-servicio", element: <ServiceAccountsPage /> }],
					},
					{
						element: <RequirePermission permission={Permissions.approveAccessRequests} />,
						children: [{ path: "accesos-temporales", element: <AccessRequestsPage /> }],
					},
					{
						element: <RequirePermission permission={Permissions.readAudit} />,
						children: [{ path: "auditoria", element: <OrganizationAuditPage /> }],
					},
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
					{ path: "aplicaciones", element: <ApplicationsPage /> },
					{ path: "planes", element: <PlansPage /> },
					{ path: "auditoria", element: <PlatformAuditPage /> },
				],
			},

			{ path: "*", element: <NotFoundPage /> },
		],
	},
])
