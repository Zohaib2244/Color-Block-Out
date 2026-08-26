import { DIRECTIONS, DIRECTION_OFFSET } from "../types";
import type { Direction, GridCell } from "../types";

/** Identity of a gate slot: one gate per (cell, edge). */
export function gateKey(x: number, z: number, side: Direction): string {
  return `${x},${z},${side}`;
}

/**
 * The sides of `cell` that face off the playable board — exactly the sides
 * Unity's `GridBuilder.BuildWalls` puts a wall on, and therefore the only
 * places a gate can replace one.
 */
export function boundarySides(cell: GridCell, isPlayable: (cell: GridCell) => boolean): Direction[] {
  if (!isPlayable(cell)) return [];
  return DIRECTIONS.filter((side) => {
    const offset = DIRECTION_OFFSET[side];
    return !isPlayable({ x: cell.x + offset.x, z: cell.z + offset.z });
  });
}

/**
 * Which of `sides` a click at (u, v) inside the cell is nearest to, so clicking
 * near an edge picks that edge instead of making the user choose from a dropdown.
 * `u` runs 0 (west) to 1 (east); `v` runs 0 (south) to 1 (north).
 */
export function nearestBoundarySide(sides: Direction[], u: number, v: number): Direction | null {
  if (sides.length === 0) return null;
  const distance: Record<Direction, number> = { Left: u, Right: 1 - u, Down: v, Up: 1 - v };
  return sides.reduce((best, side) => (distance[side] < distance[best] ? side : best), sides[0]);
}
