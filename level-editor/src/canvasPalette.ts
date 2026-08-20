import type { Theme } from "./theme";

/**
 * Canvas 2D fillStyle can't resolve CSS custom properties, so these mirror
 * the ember palette tokens in index.css (same source as AVN Hub's
 * styles/globals.css) as literal hex values, per theme.
 */
export interface CanvasPalette {
  /** Grid Designer's "will be playable" preview fill. */
  playable: string;
  /** Grid Designer's user-toggled wall fill. */
  wall: string;
  /** Grid Designer's "open to the outside, won't be generated" preview fill. */
  exterior: string;
  /** Level Editor / Preset Designer's neutral floor fill (a placed or placeable cell). */
  floor: string;
  /** Non-playable / out-of-shape cell fill. */
  inert: string;
  selectedTint: string;
  border: string;
  textMuted: string;
}

const DARK: CanvasPalette = {
  playable: "#1c6b30",
  wall: "#3d3220",
  exterior: "#5a2a12",
  floor: "#1a1610",
  inert: "#13100c",
  selectedTint: "rgba(0, 180, 200, 0.35)",
  border: "#3d3220",
  textMuted: "#9f9887",
};

const LIGHT: CanvasPalette = {
  playable: "#bcdcc0",
  wall: "#7a6a52",
  exterior: "#e8c4ac",
  floor: "#faf6f0",
  inert: "#d8cfbc",
  selectedTint: "rgba(0, 118, 138, 0.28)",
  border: "#7a6a52",
  textMuted: "#9a8870",
};

export function getCanvasPalette(theme: Theme): CanvasPalette {
  return theme === "light" ? LIGHT : DARK;
}
