// Mirrors Assets/_GameData/Systems/Scripts/Utilities/Enums.cs (BlockColorTypes, Direction)
// and the Cat puzzle data classes (CatPlacement, CatHolePlacement, CatHoleType).

export const BLOCK_COLORS = [
  "Red",
  "Orange",
  "Yellow",
  "Blue",
  "Cyan",
  "Green",
  "Purple",
  "Pink",
  "Teal",
] as const;
export type BlockColor = (typeof BLOCK_COLORS)[number];

export const HOLE_TYPES = [
  "Isolated",
  "EndCap",
  "Straight",
  "Corner",
  "OneSide",
  "Middle",
] as const;
export type HoleType = (typeof HOLE_TYPES)[number];

// Order matters: matches C# `enum Direction { Up, Right, Down, Left }` (0..3),
// since rotation math is `((int)direction + quarterTurns) % 4`.
export const DIRECTIONS = ["Up", "Right", "Down", "Left"] as const;
export type Direction = (typeof DIRECTIONS)[number];

export type RotationQuarterTurns = 0 | 1 | 2 | 3;

export interface GridCell {
  x: number;
  z: number;
}

export function cellKey(x: number, z: number): string {
  return `${x},${z}`;
}

/** Direction -> (dx, dz) neighbor offset. Matches GridCreatorTool's North=+z/East=+x/South=-z/West=-x. */
export const DIRECTION_OFFSET: Record<Direction, GridCell> = {
  Up: { x: 0, z: 1 },
  Right: { x: 1, z: 0 },
  Down: { x: 0, z: -1 },
  Left: { x: -1, z: 0 },
};

export interface SavedGrid {
  id: string;
  name: string;
  width: number;
  length: number;
  cellSize: number;
  /** Raw user-toggled wall cells, flat array, index = z*width+x. Pre flood-fill. */
  wallToggles: boolean[];
  updatedAt: number;
}

export interface CatPlacement {
  color: BlockColor;
  x: number;
  z: number;
}

/** One grid cell of a hole. `holeType`/`rotation` pick the Unity prefab for that cell. */
export interface HoleCell {
  x: number;
  z: number;
  holeType: HoleType;
  rotationQuarterTurns: RotationQuarterTurns;
}

/**
 * ONE hole: a connected run of cells sharing a single color and a single
 * capacity. The whole shape is the hole — not each cell — so capacity is
 * stored once here rather than repeated per cell.
 */
export interface HolePlacement {
  id: string;
  color: BlockColor;
  /** How many cats this hole can swallow before it's full. Minimum 1. */
  capacity: number;
  cells: HoleCell[];
}

export interface SavedLevel {
  id: string;
  name: string;
  gridId: string;
  cats: CatPlacement[];
  holes: HolePlacement[];
  updatedAt: number;
}

export type HoleOpeningsConfig = Record<HoleType, Direction[]>;

/** A single cell within a saved hole shape, relative to the shape's bounding-box min corner. */
export interface HoleShapePresetCell {
  dx: number;
  dz: number;
  holeType: HoleType;
  rotationQuarterTurns: RotationQuarterTurns;
}

/** A reusable hole shape (drawn once in the Hole Presets tab, stamped onto any level's grid). */
export interface HoleShapePreset {
  id: string;
  name: string;
  cells: HoleShapePresetCell[];
  updatedAt: number;
}
