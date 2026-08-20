import { computePlayableMask, gridIndex } from "../grid/floodFill";
import { cellKey } from "../types";
import type { BlockColor, SavedGrid, SavedLevel } from "../types";

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
    if (!Number.isInteger(hole.capacity) || hole.capacity < 1) {
      issues.push({
        severity: "error",
        message: `A ${hole.color} hole has an invalid capacity (${hole.capacity}); must be a whole number of 1 or more.`,
      });
    }
    for (const cell of hole.cells) {
      if (!isPlayable(cell.x, cell.z)) {
        issues.push({ severity: "error", message: `${hole.color} hole covers (${cell.x}, ${cell.z}), which is not a playable cell.` });
      }
      addOccupant(cell.x, cell.z, `${hole.color} hole`);
    }
  }

  for (const [key, labels] of occupied) {
    if (labels.length > 1) {
      issues.push({ severity: "error", message: `Multiple placements on cell (${key}): ${labels.join(", ")}.` });
    }
  }

  const catCounts = new Map<BlockColor, number>();
  for (const cat of level.cats) catCounts.set(cat.color, (catCounts.get(cat.color) ?? 0) + 1);

  const holeCapacity = new Map<BlockColor, number>();
  for (const hole of level.holes) {
    const capacity = Number.isInteger(hole.capacity) && hole.capacity >= 1 ? hole.capacity : 0;
    holeCapacity.set(hole.color, (holeCapacity.get(hole.color) ?? 0) + capacity);
  }

  for (const [color, cats] of catCounts) {
    const capacity = holeCapacity.get(color) ?? 0;
    if (capacity === 0) {
      issues.push({ severity: "warning", message: `No hole of color ${color} exists for its ${cats} cat(s).` });
    } else if (capacity < cats) {
      issues.push({
        severity: "error",
        message: `${color}: ${cats} cats but only ${capacity} hole capacity — ${cats - capacity} cat(s) could never be collected.`,
      });
    } else if (capacity > cats) {
      issues.push({
        severity: "warning",
        message: `${color}: ${capacity} hole capacity for only ${cats} cat(s) — ${capacity - cats} slot(s) go unused.`,
      });
    }
  }

  for (const color of holeCapacity.keys()) {
    if (!catCounts.has(color)) {
      issues.push({ severity: "warning", message: `No cat of color ${color} exists for its hole(s).` });
    }
  }

  return issues;
}
