import { computePlayableMask } from "../grid/floodFill";
import type { BlockColor, HoleType, RotationQuarterTurns, SavedGrid, SavedLevel } from "../types";

export const LEVEL_JSON_FORMAT_VERSION = 1 as const;

export interface LevelJsonGrid {
  id: string;
  width: number;
  length: number;
  cellSize: number;
  /** Flat, index = z*width + x. TRUE = playable floor cell (post flood-fill). */
  playableCells: boolean[];
}

export interface LevelJsonCat {
  color: BlockColor;
  x: number;
  z: number;
}

export interface LevelJsonHole {
  color: BlockColor;
  x: number;
  z: number;
  holeType: HoleType;
  rotationQuarterTurns: RotationQuarterTurns;
}

export interface LevelJson {
  formatVersion: typeof LEVEL_JSON_FORMAT_VERSION;
  levelName: string;
  grid: LevelJsonGrid;
  cats: LevelJsonCat[];
  holes: LevelJsonHole[];
}

export function toLevelJson(level: SavedLevel, grid: SavedGrid): LevelJson {
  return {
    formatVersion: LEVEL_JSON_FORMAT_VERSION,
    levelName: level.name,
    grid: {
      id: grid.id,
      width: grid.width,
      length: grid.length,
      cellSize: grid.cellSize,
      playableCells: computePlayableMask(grid.width, grid.length, grid.wallToggles),
    },
    cats: level.cats.map((c) => ({ color: c.color, x: c.x, z: c.z })),
    holes: level.holes.map((h) => ({
      color: h.color,
      x: h.x,
      z: h.z,
      holeType: h.holeType,
      rotationQuarterTurns: h.rotationQuarterTurns,
    })),
  };
}

export function downloadLevelJson(json: LevelJson): void {
  const blob = new Blob([JSON.stringify(json, null, 2)], { type: "application/json" });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = `${json.levelName || "level"}.json`;
  anchor.click();
  URL.revokeObjectURL(url);
}

export function parseLevelJson(raw: string): LevelJson {
  const parsed = JSON.parse(raw) as Partial<LevelJson>;
  if (parsed.formatVersion !== LEVEL_JSON_FORMAT_VERSION) {
    throw new Error(`Unsupported level JSON formatVersion: ${String(parsed.formatVersion)}`);
  }
  if (!parsed.grid || !Array.isArray(parsed.cats) || !Array.isArray(parsed.holes)) {
    throw new Error("Level JSON is missing grid/cats/holes.");
  }
  return parsed as LevelJson;
}

/**
 * Converts an imported LevelJson back into editable SavedGrid + SavedLevel
 * shapes. Since the JSON only stores the post-flood-fill playable mask (not
 * the original wall toggles), the playable cells are imported directly as
 * "wall toggles" (inverted) â€” re-running flood fill over a fully-enclosed
 * mask is a no-op, so editing continues to work exactly as if the user had
 * drawn that same boundary by hand.
 */
export function fromLevelJson(json: LevelJson): { grid: Omit<import("../types").SavedGrid, "id" | "updatedAt">; level: Omit<SavedLevel, "id" | "gridId" | "updatedAt"> } {
  const wallToggles = json.grid.playableCells.map((playable) => !playable);
  return {
    grid: {
      name: `${json.levelName} (imported grid)`,
      width: json.grid.width,
      length: json.grid.length,
      cellSize: json.grid.cellSize,
      wallToggles,
    },
    level: {
      name: json.levelName,
      cats: json.cats.map((c) => ({ color: c.color, x: c.x, z: c.z })),
      holes: json.holes.map((h) => ({
        color: h.color,
        x: h.x,
        z: h.z,
        holeType: h.holeType,
        rotationQuarterTurns: h.rotationQuarterTurns,
      })),
    },
  };
}
