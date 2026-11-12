import { createBrowserRouter } from "react-router"
import { ConfirmEmailPage } from "./pages/ConfirmEmailPage"
import { ErrorPage, NotFoundPage, RouteErrorPage } from "./pages/ErrorPages"
import { ForgotPasswordPage, ResetPasswordPage } from "./pages/PasswordPages"
import { HomePage } from "./pages/HomePage"
import { InvitationPage } from "./pages/InvitationPage"
import { MfaPage } from "./pages/MfaPage"
import { MfaSetupPage } from "./pages/MfaSetupPage"
import { OnboardingPage, SelectTenantPage } from "./pages/TenantPages"
import { SignInPage } from "./pages/SignInPage"
import { SignUpPage } from "./pages/SignUpPage"
import { VerifyEmailPage } from "./pages/VerifyEmailPage"

// Rutas a las que redirige o enlaza Herensuge.Api (ver README: "Rutas que debe implementar la SPA de login").
export const router = createBrowserRouter([
	{
		errorElement: <RouteErrorPage />,
		children: [
			{ path: "/", element: <HomePage /> },
			{ path: "/login", element: <SignInPage /> },
			{ path: "/register", element: <SignUpPage /> },
			{ path: "/mfa", element: <MfaPage /> },
			{ path: "/mfa/setup", element: <MfaSetupPage /> },
			{ path: "/select-tenant", element: <SelectTenantPage /> },
			{ path: "/onboarding", element: <OnboardingPage /> },
			{ path: "/verify-email", element: <VerifyEmailPage /> },
			{ path: "/forgot-password", element: <ForgotPasswordPage /> },
			{ path: "/reset-password", element: <ResetPasswordPage /> },
			{ path: "/confirm-email", element: <ConfirmEmailPage /> },
			{ path: "/invitations/:token", element: <InvitationPage /> },
			{ path: "/error", element: <ErrorPage /> },
			{ path: "*", element: <NotFoundPage /> },
		],
	},
])
