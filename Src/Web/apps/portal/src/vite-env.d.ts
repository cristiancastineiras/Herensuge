/// <reference types="vite/client" />

// Variables de entorno (entorno/.env.*). Si añades una, declárala aquí.
interface ImportMetaEnv {
	/** URL pública del IdP, para vincular cuentas externas. Vacía: no se ofrece. */
	readonly VITE_URL_IDP?: string
}

interface ImportMeta {
	readonly env: ImportMetaEnv
}
