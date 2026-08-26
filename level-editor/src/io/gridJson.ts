import { computePlayableMask } from "../grid/floodFill";
import type { SavedGrid } from "../types";

/**
 * A grid on its own, so a board shape can live in source control and be shared
 * between levels without being trapped in one level's export.
 *
 * The grid section here is deliberately field-for-field the same as
 * `LevelJson.grid` (minus the id, plus a name), so the two never drift and a
 * level's inline grid can be recognised as one of these.
 */
export const GRID_JSON_FORMAT_VERSION = 1 as const;

export interface GridJson {
  formatVersion: typeof GRID_JSON_FORMAT_VERSION;
  gridName: string;
  width: number;
  length: number;
  cellSize: number;
  /** Flat, index = z*width + x. TRUE = playable floor cell (post flood-fill). */
  playableCells: boolean[];
}

export function toGridJson(grid: SavedGrid): GridJson {
  return {
    formatVersion: GRID_JSON_FORMAT_VERSION,
    gridName: grid.name,
    width: grid.width,
    length: grid.length,
    cellSize: grid.cellSize,
    playableCells: computePlayableMask(grid.width, grid.length, grid.wallToggles),
  };
}

export function parseGridJson(raw: string): GridJson {
  const parsed = JSON.parse(raw) as Partial<GridJson>;
  if (parsed.formatVersion !== GRID_JSON_FORMAT_VERSION) {
    throw new Error(`Unsupported grid JSON formatVersion: ${String(parsed.formatVersion)}`);
  }
  if (
    !Number.isInteger(parsed.width) ||
    !Number.isInteger(parsed.length) ||
    (parsed.width ?? 0) < 1 ||
    (parsed.width ?? 0) > 64 ||
    (parsed.length ?? 0) < 1 ||
    (parsed.length ?? 0) > 64 ||
    !Array.isArray(parsed.playableCells)
  ) {
    throw new Error("Grid JSON is missing width/length/playableCells.");
  }
  if (parsed.playableCells.length !== parsed.width! * parsed.length!) {
    throw new Error("Grid JSON playableCells does not match width x length.");
  }
  if (!parsed.playableCells.every((playable) => typeof playable === "boolean")) {
    throw new Error("Grid JSON playableCells must contain only booleans.");
  }
  if (typeof parsed.cellSize !== "number" || !Number.isFinite(parsed.cellSize) || parsed.cellSize <= 0) {
    throw new Error("Grid JSON cellSize must be a positive number.");
  }
  return {
    formatVersion: GRID_JSON_FORMAT_VERSION,
    gridName: typeof parsed.gridName === "string" && parsed.gridName.trim() ? parsed.gridName : "Imported Grid",
    width: parsed.width!,
    length: parsed.length!,
    cellSize: parsed.cellSize,
    playableCells: parsed.playableCells,
  };
}

/**
 * Since the JSON stores the post-flood-fill playable mask rather than the raw
 * wall toggles, the mask is imported inverted as the toggles — re-running the
 * flood fill over an already-enclosed mask is a no-op, so editing continues
 * exactly as if the boundary had been drawn by hand. Same reasoning as
 * `fromLevelJson`.
 */
export function fromGridJson(json: GridJson): Omit<SavedGrid, "id" | "updatedAt"> {
  return {
    name: json.gridName,
    width: json.width,
    length: json.length,
    cellSize: json.cellSize,
    wallToggles: json.playableCells.map((playable) => !playable),
  };
}

/**
 * True when both describe the same board, so importing doesn't pile up
 * duplicates.
 *
 * Compares the flood-filled *playable mask*, not the raw wall toggles: a grid
 * that came back through JSON has its toggles rebuilt from that mask, so two
 * boards that play identically can still disagree on which cells were painted
 * walls versus merely open to the outside. The mask is what actually defines
 * the board.
 */
export function sameShape(a: Omit<SavedGrid, "id" | "updatedAt">, b: SavedGrid): boolean {
  if (a.width !== b.width || a.length !== b.length || a.cellSize !== b.cellSize) return false;
  const maskA = computePlayableMask(a.width, a.length, a.wallToggles);
  const maskB = computePlayableMask(b.width, b.length, b.wallToggles);
  return maskA.length === maskB.length && maskA.every((playable, i) => playable === maskB[i]);
}

export function downloadGridJson(json: GridJson): void {
  const blob = new Blob([JSON.stringify(json, null, 2)], { type: "application/json" });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = `${json.gridName || "grid"}.json`;
  anchor.click();
  URL.revokeObjectURL(url);
}
