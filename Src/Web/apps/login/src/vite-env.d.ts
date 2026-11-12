/// <reference types="vite/client" />

// Variables de entorno (entorno/.env.*). Si añades una, declárala aquí.
interface ImportMetaEnv {
	/** URL del portal para el botón "Ir al portal". Vacía: no se muestra. */
	readonly VITE_URL_PORTAL?: string
}

interface ImportMeta {
	readonly env: ImportMetaEnv
}
