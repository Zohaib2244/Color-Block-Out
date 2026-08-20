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
  /** The dark "void" a hole is cut into — reads as a recess in the floor. */
  holeVoid: string;
  selectedTint: string;
  border: string;
  gridLine: string;
  gridLineMajor: string;
  textMuted: string;
  textAccent: string;
}

const DARK: CanvasPalette = {
  playable: "#1c6b30",
  wall: "#3d3220",
  exterior: "#5a2a12",
  floor: "#2b2419",
  inert: "#110e0a",
  holeVoid: "#0a0806",
  selectedTint: "rgba(0, 180, 200, 0.35)",
  border: "#3d3220",
  gridLine: "rgba(232, 223, 200, 0.10)",
  gridLineMajor: "rgba(232, 223, 200, 0.26)",
  textMuted: "#9f9887",
  textAccent: "#ff6b2b",
};

const LIGHT: CanvasPalette = {
  playable: "#bcdcc0",
  wall: "#7a6a52",
  exterior: "#e8c4ac",
  floor: "#faf6f0",
  inert: "#cdc2ab",
  holeVoid: "#4a3f30",
  selectedTint: "rgba(0, 118, 138, 0.28)",
  border: "#7a6a52",
  gridLine: "rgba(28, 24, 16, 0.14)",
  gridLineMajor: "rgba(28, 24, 16, 0.34)",
  textMuted: "#9a8870",
  textAccent: "#e05a18",
};

export function getCanvasPalette(theme: Theme): CanvasPalette {
  return theme === "light" ? LIGHT : DARK;
}
