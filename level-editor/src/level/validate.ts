import { computePlayableMask, gridIndex } from "../grid/floodFill";
import { DIRECTION_OFFSET, cellKey } from "../types";
import type { BlockColor, SavedGrid, SavedLevel } from "../types";
import { gateKey } from "./gates";

export interface ValidationIssue {
  severity: "error" | "warning";
  message: string;
}

export function validateLevel(level: SavedLevel, grid: SavedGrid): ValidationIssue[] {
  const issues: ValidationIssue[] = [];
  const playable = computePlayableMask(grid.width, grid.length, grid.wallToggles);
  const isPlayable = (x: number, z: number) =>
    x >= 0 && x < grid.width && z >= 0 && z < grid.length && playable[gridIndex(grid.width, x, z)];

  const gates = level.gates ?? [];

  // Cats may share a cell (a stack) by design, so only a hole makes a cell contested.
  const holeCells = new Map<string, string>();

  for (const cat of level.cats) {
    if (!isPlayable(cat.x, cat.z)) {
      issues.push({ severity: "error", message: `${cat.color} cat at (${cat.x}, ${cat.z}) is not on a playable cell.` });
    }
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
      const key = cellKey(cell.x, cell.z);
      const other = holeCells.get(key);
      if (other) {
        issues.push({ severity: "error", message: `Two holes (${other}, ${hole.color}) both cover cell (${key}); neither could move.` });
      } else {
        holeCells.set(key, hole.color);
      }
    }
  }

  // A cat standing where a hole already sits has nowhere to be.
  for (const cat of level.cats) {
    const key = cellKey(cat.x, cat.z);
    if (holeCells.has(key)) {
      issues.push({ severity: "error", message: `${cat.color} cat at (${key}) stands on a ${holeCells.get(key)} hole.` });
    }
  }

  // ── gates ───────────────────────────────────────────────────────
  const gateSlots = new Set<string>();
  for (const gate of gates) {
    const at = `(${gate.x}, ${gate.z})`;
    if (!isPlayable(gate.x, gate.z)) {
      issues.push({ severity: "error", message: `Gate at ${at} is not attached to a playable cell.` });
      continue;
    }

    const offset = DIRECTION_OFFSET[gate.side];
    if (isPlayable(gate.x + offset.x, gate.z + offset.z)) {
      issues.push({
        severity: "error",
        message: `Gate at ${at} faces ${gate.side}, which is open board rather than a wall — a gate has to replace a wall.`,
      });
    }

    const slot = gateKey(gate.x, gate.z, gate.side);
    if (gateSlots.has(slot)) {
      issues.push({ severity: "error", message: `Two gates share the ${gate.side} edge of ${at}.` });
    }
    gateSlots.add(slot);

    if (gate.cats.length === 0) {
      issues.push({ severity: "warning", message: `Gate at ${at} is empty, so it will never release a cat.` });
    }
  }

  // ── colour budget: every cat, on the board or queued in a gate, needs hole capacity ──
  const catCounts = new Map<BlockColor, number>();
  const bump = (color: BlockColor) => catCounts.set(color, (catCounts.get(color) ?? 0) + 1);
  for (const cat of level.cats) bump(cat.color);
  for (const gate of gates) for (const color of gate.cats) bump(color);

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
