import type { CSSProperties } from "react"

/**
 * Variables CSS que cambian el color principal (botones) por el de una marca: style={brandStyle("#1d4ed8")}.
 * El texto sobre el color es negro o blanco, el que mejor contraste. Sin color, no cambia nada.
 */
export function brandStyle(color: string | null | undefined): CSSProperties | undefined {
	if (!color || !/^#[0-9a-f]{6}$/i.test(color)) {
		return undefined
	}

	return {
		"--color-primary": color,
		"--color-primary-hover": `color-mix(in oklab, ${color} 82%, white)`,
		"--color-primary-foreground": isLight(color) ? "#18181b" : "#ffffff",
	} as CSSProperties
}

/** Luminancia relativa (WCAG) por encima del punto en que el texto negro contrasta más que el blanco. */
function isLight(hex: string): boolean {
	const [r = 0, g = 0, b = 0] = [1, 3, 5].map((start) => {
		const channel = Number.parseInt(hex.slice(start, start + 2), 16) / 255
		return channel <= 0.04045 ? channel / 12.92 : ((channel + 0.055) / 1.055) ** 2.4
	})
	return 0.2126 * r + 0.7152 * g + 0.0722 * b > 0.179
}
