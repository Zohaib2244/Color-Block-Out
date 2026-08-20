import { computePlayableMask, gridIndex } from "../grid/floodFill";
import { cellKey } from "../types";
import type { SavedGrid, SavedLevel } from "../types";

export interface ValidationIssue {
  severity: "error" | "warning";
  message: string;
}

export function validateLevel(level: SavedLevel, grid: SavedGrid): ValidationIssue[] {
  const issues: ValidationIssue[] = [];
  const playable = computePlayableMask(grid.width, grid.length, grid.wallToggles);
  const isPlayable = (x: number, z: number) =>
    x >= 0 && x < grid.width && z >= 0 && z < grid.length && playable[gridIndex(grid.width, x, z)];

  const occupied = new Map<string, string[]>();
  const addOccupant = (x: number, z: number, label: string) => {
    const key = cellKey(x, z);
    const list = occupied.get(key) ?? [];
    list.push(label);
    occupied.set(key, list);
  };

  for (const cat of level.cats) {
    if (!isPlayable(cat.x, cat.z)) {
      issues.push({ severity: "error", message: `${cat.color} cat at (${cat.x}, ${cat.z}) is not on a playable cell.` });
    }
    addOccupant(cat.x, cat.z, `${cat.color} cat`);
  }
  for (const hole of level.holes) {
    if (!isPlayable(hole.x, hole.z)) {
      issues.push({ severity: "error", message: `${hole.color} hole at (${hole.x}, ${hole.z}) is not on a playable cell.` });
    }
    addOccupant(hole.x, hole.z, `${hole.color} hole`);
  }
  for (const [key, labels] of occupied) {
    if (labels.length > 1) {
      issues.push({ severity: "error", message: `Multiple placements on cell (${key}): ${labels.join(", ")}.` });
    }
  }

  const catColors = new Set(level.cats.map((c) => c.color));
  const holeColors = new Set(level.holes.map((h) => h.color));
  for (const color of catColors) {
    if (!holeColors.has(color)) {
      issues.push({ severity: "warning", message: `No hole of color ${color} exists for its cats.` });
    }
  }
  for (const color of holeColors) {
    if (!catColors.has(color)) {
      issues.push({ severity: "warning", message: `No cat of color ${color} exists for its hole(s).` });
    }
  }

  return issues;
}
