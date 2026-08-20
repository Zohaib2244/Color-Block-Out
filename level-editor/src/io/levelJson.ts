import { computePlayableMask } from "../grid/floodFill";
import { connectedComponents, makeHole } from "../level/holeShape";
import { newId } from "../storage";
import type { BlockColor, GridCell, HoleType, RotationQuarterTurns, SavedGrid, SavedLevel } from "../types";

/**
 * Format history:
 *  - v1: holes were a flat list of single cells.
 *  - v2: added `capacity` to each of those cells.
 *  - v3: a hole is one shape — `{ color, capacity, cells[] }` — because
 *    capacity belongs to the whole tunnel, not to each cell of it.
 *
 * v1/v2 files still import: their loose cells are regrouped into
 * orthogonally-connected same-color shapes, and each shape takes the largest
 * capacity found among its cells (1 for v1, which had no capacity at all).
 */
export const LEVEL_JSON_FORMAT_VERSION = 3 as const;
const SUPPORTED_FORMAT_VERSIONS = [1, 2, 3];

export const DEFAULT_HOLE_CAPACITY = 1;

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

export interface LevelJsonHoleCell {
  x: number;
  z: number;
  holeType: HoleType;
  rotationQuarterTurns: RotationQuarterTurns;
}

/** ONE hole: a connected shape of cells sharing a single color and capacity. */
export interface LevelJsonHole {
  id: string;
  color: BlockColor;
  /** How many cats this whole shape can swallow before it's full. Minimum 1. */
  capacity: number;
  cells: LevelJsonHoleCell[];
}

/** The pre-v3 shape of a hole entry: one loose cell, optionally with a capacity. */
interface LegacyFlatHole extends LevelJsonHoleCell {
  color: BlockColor;
  capacity?: number;
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
      id: h.id,
      color: h.color,
      capacity: normalizeCapacity(h.capacity),
      cells: h.cells.map((c) => ({
        x: c.x,
        z: c.z,
        holeType: c.holeType,
        rotationQuarterTurns: c.rotationQuarterTurns,
      })),
    })),
  };
}

export function normalizeCapacity(value: unknown): number {
  const parsed = typeof value === "number" ? Math.floor(value) : Number.NaN;
  return Number.isFinite(parsed) && parsed >= 1 ? parsed : DEFAULT_HOLE_CAPACITY;
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
  const parsed = JSON.parse(raw) as Partial<LevelJson> & { holes?: unknown[] };
  if (typeof parsed.formatVersion !== "number" || !SUPPORTED_FORMAT_VERSIONS.includes(parsed.formatVersion)) {
    throw new Error(`Unsupported level JSON formatVersion: ${String(parsed.formatVersion)}`);
  }
  if (!parsed.grid || !Array.isArray(parsed.cats) || !Array.isArray(parsed.holes)) {
    throw new Error("Level JSON is missing grid/cats/holes.");
  }

  const holes =
    parsed.formatVersion >= 3
      ? (parsed.holes as LevelJsonHole[]).map((h) => ({
          id: h.id || newId(),
          color: h.color,
          capacity: normalizeCapacity(h.capacity),
          cells: h.cells ?? [],
        }))
      : regroupLegacyHoles(parsed.holes as LegacyFlatHole[]);

  return { ...(parsed as LevelJson), formatVersion: LEVEL_JSON_FORMAT_VERSION, holes };
}

/**
 * Rebuilds pre-v3 loose hole cells into the shapes they were drawn as:
 * connected components, split by color so two different-colored tunnels that
 * happen to touch don't fuse into one hole.
 */
function regroupLegacyHoles(flat: LegacyFlatHole[]): LevelJsonHole[] {
  const byColor = new Map<BlockColor, LegacyFlatHole[]>();
  for (const cell of flat) {
    const list = byColor.get(cell.color) ?? [];
    list.push(cell);
    byColor.set(cell.color, list);
  }

  const holes: LevelJsonHole[] = [];
  for (const [color, cells] of byColor) {
    const capacityAt = new Map(cells.map((c) => [`${c.x},${c.z}`, normalizeCapacity(c.capacity)]));
    for (const component of connectedComponents(cells as GridCell[])) {
      const capacity = Math.max(...component.map((c) => capacityAt.get(`${c.x},${c.z}`) ?? DEFAULT_HOLE_CAPACITY));
      holes.push(makeHole(newId(), color, capacity, component));
    }
  }
  return holes;
}

/**
 * Converts an imported LevelJson back into editable SavedGrid + SavedLevel
 * shapes. Since the JSON only stores the post-flood-fill playable mask (not
 * the original wall toggles), the playable cells are imported directly as
 * "wall toggles" (inverted) — re-running flood fill over a fully-enclosed
 * mask is a no-op, so editing continues to work exactly as if the user had
 * drawn that same boundary by hand.
 */
export function fromLevelJson(json: LevelJson): {
  grid: Omit<SavedGrid, "id" | "updatedAt">;
  level: Omit<SavedLevel, "id" | "gridId" | "updatedAt">;
} {
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
        id: h.id || newId(),
        color: h.color,
        capacity: normalizeCapacity(h.capacity),
        cells: h.cells.map((c) => ({
          x: c.x,
          z: c.z,
          holeType: c.holeType,
          rotationQuarterTurns: c.rotationQuarterTurns,
        })),
      })),
    },
  };
}
