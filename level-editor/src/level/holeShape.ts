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
  Direction,
  GridCell,
  HoleOpeningsConfig,
  HoleType,
  RotationQuarterTurns,
} from "../types";

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

/** Rotates a hole type's configured default openings by a given rotation, for rendering/inspection. */
export function openingsForHole(
  holeType: HoleType,
  rotationQuarterTurns: RotationQuarterTurns,
  config: HoleOpeningsConfig
): Direction[] {
  return (config[holeType] ?? []).map((d) => rotateDirection(d, rotationQuarterTurns));
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
  cells: GridCell[],
  config: HoleOpeningsConfig
): Map<string, { holeType: HoleType; rotationQuarterTurns: RotationQuarterTurns }> {
  const shape = new Set(cells.map((c) => cellKey(c.x, c.z)));
  const result = new Map<string, { holeType: HoleType; rotationQuarterTurns: RotationQuarterTurns }>();
  for (const cell of cells) {
    const { holeType, openings } = classifyHoleCell(cell, shape);
    const rotationQuarterTurns = solveRotation(openings, config[holeType] ?? []);
    result.set(cellKey(cell.x, cell.z), { holeType, rotationQuarterTurns });
  }
  return result;
}
