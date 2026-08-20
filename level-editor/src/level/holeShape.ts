/**
 * Mirrors CatLevelEditorWindow.GetHoleType / GetRotation
 * (Assets/_GameData/Systems/Scripts/Editor/CatLevelEditorWindow.cs).
 *
 * A hole "shape" is a connected run of selected cells drawn in one gesture.
 * Each cell's HoleType is derived purely from how many of its 4 neighbors
 * are also part of the shape; its rotation is solved by rotating the hole
 * type's configured `defaultOpenings` until it matches the actual open
 * neighbors.
 */
import { DIRECTIONS, DIRECTION_OFFSET, cellKey } from "../types";
import type {
  BlockColor,
  Direction,
  GridCell,
  HoleOpeningsConfig,
  HolePlacement,
  HoleShapePreset,
  HoleShapePresetCell,
  HoleType,
  RotationQuarterTurns,
} from "../types";

/**
 * Directions each hole type is open on at rotation 0. Mirrors
 * `CatHoleConfiguration`'s `CatHolePrefabData.defaultOpenings` in Unity —
 * authored per-prefab there, so these are placeholder values; if your
 * project's actual prefabs use different default openings, update this
 * table to match so exported rotations line up with your hole prefabs.
 */
export const DEFAULT_HOLE_OPENINGS: HoleOpeningsConfig = {
  Isolated: [],
  EndCap: ["Up"],
  Straight: ["Up", "Down"],
  Corner: ["Up", "Right"],
  OneSide: ["Up", "Right", "Down"],
  Middle: ["Up", "Right", "Down", "Left"],
};

export function classifyHoleCell(cell: GridCell, shape: Set<string>): { holeType: HoleType; openings: Direction[] } {
  const openings = DIRECTIONS.filter((dir) => {
    const offset = DIRECTION_OFFSET[dir];
    return shape.has(cellKey(cell.x + offset.x, cell.z + offset.z));
  });

  const count = openings.length;
  let holeType: HoleType;
  if (count === 0) holeType = "Isolated";
  else if (count === 1) holeType = "EndCap";
  else if (count === 3) holeType = "OneSide";
  else if (count === 4) holeType = "Middle";
  else {
    const straight =
      (openings.includes("Up") && openings.includes("Down")) ||
      (openings.includes("Left") && openings.includes("Right"));
    holeType = straight ? "Straight" : "Corner";
  }

  return { holeType, openings };
}

function rotateDirection(direction: Direction, quarterTurns: number): Direction {
  const index = DIRECTIONS.indexOf(direction);
  return DIRECTIONS[(index + quarterTurns) % 4];
}

/** Finds the 0-3 rotation of `defaultOpenings` that matches `actualOpenings`. */
export function solveRotation(
  actualOpenings: Direction[],
  defaultOpenings: Direction[]
): RotationQuarterTurns {
  const actual = new Set(actualOpenings);
  for (let quarterTurns = 0; quarterTurns < 4; quarterTurns++) {
    const rotated = new Set(defaultOpenings.map((d) => rotateDirection(d, quarterTurns)));
    if (rotated.size === actual.size && [...rotated].every((d) => actual.has(d))) {
      return quarterTurns as RotationQuarterTurns;
    }
  }
  return 0;
}

export function classifyHoleShape(
  cells: GridCell[]
): Map<string, { holeType: HoleType; rotationQuarterTurns: RotationQuarterTurns }> {
  const shape = new Set(cells.map((c) => cellKey(c.x, c.z)));
  const result = new Map<string, { holeType: HoleType; rotationQuarterTurns: RotationQuarterTurns }>();
  for (const cell of cells) {
    const { holeType, openings } = classifyHoleCell(cell, shape);
    const rotationQuarterTurns = solveRotation(openings, DEFAULT_HOLE_OPENINGS[holeType]);
    result.set(cellKey(cell.x, cell.z), { holeType, rotationQuarterTurns });
  }
  return result;
}

/** Builds one hole (a connected shape sharing a single capacity) from raw cells. */
export function makeHole(id: string, color: BlockColor, capacity: number, cells: GridCell[]): HolePlacement {
  const classified = classifyHoleShape(cells);
  return {
    id,
    color,
    capacity,
    cells: cells.map((c) => {
      const result = classified.get(cellKey(c.x, c.z))!;
      return { x: c.x, z: c.z, holeType: result.holeType, rotationQuarterTurns: result.rotationQuarterTurns };
    }),
  };
}

/** cellKey -> the hole occupying it, for hit-testing a click against placed holes. */
export function holeCellIndex(holes: HolePlacement[]): Map<string, HolePlacement> {
  const index = new Map<string, HolePlacement>();
  for (const hole of holes) {
    for (const cell of hole.cells) index.set(cellKey(cell.x, cell.z), hole);
  }
  return index;
}

/**
 * Splits loose cells into orthogonally-connected components. Used to migrate
 * pre-v3 level files, where every cell was its own hole record, back into the
 * shapes they were originally drawn as.
 */
export function connectedComponents(cells: GridCell[]): GridCell[][] {
  const remaining = new Map(cells.map((c) => [cellKey(c.x, c.z), c]));
  const components: GridCell[][] = [];
  while (remaining.size > 0) {
    const [firstKey, firstCell] = remaining.entries().next().value as [string, GridCell];
    remaining.delete(firstKey);
    const component: GridCell[] = [firstCell];
    const queue: GridCell[] = [firstCell];
    while (queue.length > 0) {
      const current = queue.shift()!;
      for (const dir of DIRECTIONS) {
        const offset = DIRECTION_OFFSET[dir];
        const key = cellKey(current.x + offset.x, current.z + offset.z);
        const neighbor = remaining.get(key);
        if (!neighbor) continue;
        remaining.delete(key);
        component.push(neighbor);
        queue.push(neighbor);
      }
    }
    components.push(component);
  }
  return components;
}

/** Normalizes a drawn shape into preset cells relative to its bounding-box min corner (0,0). */
export function buildPresetCells(cells: GridCell[]): HoleShapePresetCell[] {
  if (cells.length === 0) return [];
  const classified = classifyHoleShape(cells);
  const minX = Math.min(...cells.map((c) => c.x));
  const minZ = Math.min(...cells.map((c) => c.z));
  return cells.map((c) => {
    const result = classified.get(cellKey(c.x, c.z))!;
    return { dx: c.x - minX, dz: c.z - minZ, holeType: result.holeType, rotationQuarterTurns: result.rotationQuarterTurns };
  });
}

/**
 * Rotates a (dx, dz) offset by 90deg steps using the same rotation direction
 * as Direction (Up->Right->Down->Left): (x, z) -> (z, -x) per quarter turn.
 */
export function rotateOffset(dx: number, dz: number, quarterTurns: number): { dx: number; dz: number } {
  let x = dx;
  let z = dz;
  for (let i = 0; i < ((quarterTurns % 4) + 4) % 4; i++) {
    [x, z] = [z, -x];
  }
  return { dx: x, dz: z };
}

/** Rotates every cell of a preset (both its position and its own hole rotation) by a placement rotation. */
export function rotatePresetCells(cells: HoleShapePresetCell[], quarterTurns: RotationQuarterTurns): HoleShapePresetCell[] {
  return cells.map((c) => {
    const { dx, dz } = rotateOffset(c.dx, c.dz, quarterTurns);
    return { dx, dz, holeType: c.holeType, rotationQuarterTurns: ((c.rotationQuarterTurns + quarterTurns) % 4) as RotationQuarterTurns };
  });
}

/** Stamps a preset onto the grid at an anchor cell, with an optional placement rotation. */
export function stampPreset(
  preset: HoleShapePreset,
  anchor: GridCell,
  placementRotation: RotationQuarterTurns
): Array<{ x: number; z: number; holeType: HoleType; rotationQuarterTurns: RotationQuarterTurns }> {
  const rotated = rotatePresetCells(preset.cells, placementRotation);
  return rotated.map((c) => ({
    x: anchor.x + c.dx,
    z: anchor.z + c.dz,
    holeType: c.holeType,
    rotationQuarterTurns: c.rotationQuarterTurns,
  }));
}
