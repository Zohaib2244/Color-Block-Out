/**
 * Mirrors GridCreatorTool.FindInteriorCells + GridManager.MarkExteriorCellsAsOccupied
 * (Assets/_GameData/Systems/Scripts/Editor/GridCreatorTool.cs).
 *
 * A cell is only "playable" if it is NOT a user-toggled wall AND it cannot be
 * reached from the grid border by walking through non-wall cells. Anything
 * reachable from the outside (including the border itself, if left open) is
 * exterior/unplayable, even though the user never marked it a wall. This is
 * exactly what GridData ends up storing: `wallCells[]` in the saved asset is
 * the inverse of this playable mask, not the raw user toggles.
 */
export function computePlayableMask(
  width: number,
  length: number,
  wallToggles: boolean[]
): boolean[] {
  const idx = (x: number, z: number) => z * width + x;
  const playable = new Array<boolean>(width * length);
  for (let x = 0; x < width; x++) {
    for (let z = 0; z < length; z++) {
      playable[idx(x, z)] = !wallToggles[idx(x, z)];
    }
  }

  const queue: [number, number][] = [];
  const tryEnqueueEdge = (x: number, z: number) => {
    if (!wallToggles[idx(x, z)]) {
      playable[idx(x, z)] = false;
      queue.push([x, z]);
    }
  };
  for (let x = 0; x < width; x++) {
    tryEnqueueEdge(x, 0);
    tryEnqueueEdge(x, length - 1);
  }
  for (let z = 0; z < length; z++) {
    tryEnqueueEdge(0, z);
    tryEnqueueEdge(width - 1, z);
  }

  const tryMarkExterior = (x: number, z: number) => {
    if (x < 0 || x >= width || z < 0 || z >= length) return;
    if (!playable[idx(x, z)] || wallToggles[idx(x, z)]) return;
    playable[idx(x, z)] = false;
    queue.push([x, z]);
  };

  while (queue.length > 0) {
    const [x, z] = queue.shift()!;
    tryMarkExterior(x + 1, z);
    tryMarkExterior(x - 1, z);
    tryMarkExterior(x, z + 1);
    tryMarkExterior(x, z - 1);
  }

  return playable;
}

export function gridIndex(width: number, x: number, z: number): number {
  return z * width + x;
}

export function makeFlatGrid(width: number, length: number, value: boolean): boolean[] {
  return new Array(width * length).fill(value);
}
