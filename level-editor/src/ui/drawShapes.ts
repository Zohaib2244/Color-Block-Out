import { cellKey } from "../types";
import type { GridCell } from "../types";

export interface CellRect {
  x: number;
  y: number;
  width: number;
  height: number;
}

export type CellRectLookup = (x: number, z: number) => CellRect;

/**
 * A cat: a solid, filled creature that sits ON the floor — round head, two
 * pointed ears, two eyes. Deliberately the visual opposite of a hole (which
 * is a dark recess cut INTO the floor), so the two can never be confused at
 * a glance even when they share a color.
 */
export function drawCat(ctx: CanvasRenderingContext2D, rect: CellRect, colorHex: string, voidHex: string) {
  const cx = rect.x + rect.width / 2;
  const size = Math.min(rect.width, rect.height);
  const r = size * 0.29;
  const headY = rect.y + rect.height / 2 + r * 0.1;

  ctx.fillStyle = colorHex;

  const earLift = r * 0.9;
  ctx.beginPath();
  ctx.moveTo(cx - r * 0.82, headY - r * 0.42);
  ctx.lineTo(cx - r * 0.5, headY - r * 0.42 - earLift);
  ctx.lineTo(cx - r * 0.08, headY - r * 0.6);
  ctx.closePath();
  ctx.fill();

  ctx.beginPath();
  ctx.moveTo(cx + r * 0.82, headY - r * 0.42);
  ctx.lineTo(cx + r * 0.5, headY - r * 0.42 - earLift);
  ctx.lineTo(cx + r * 0.08, headY - r * 0.6);
  ctx.closePath();
  ctx.fill();

  ctx.beginPath();
  ctx.arc(cx, headY, r, 0, Math.PI * 2);
  ctx.fill();

  // eyes — skipped when a cell is too small for them to read as anything but noise
  if (size >= 20) {
    const eyeR = Math.max(1, r * 0.17);
    ctx.fillStyle = voidHex;
    ctx.beginPath();
    ctx.arc(cx - r * 0.37, headY - r * 0.04, eyeR, 0, Math.PI * 2);
    ctx.fill();
    ctx.beginPath();
    ctx.arc(cx + r * 0.37, headY - r * 0.04, eyeR, 0, Math.PI * 2);
    ctx.fill();
  }
}

/** Overlap between neighbouring cell rects, so abutting edges don't hairline-seam. */
const BLEED = 0.5;

/**
 * Builds the outline of the UNION of a set of cells, inset from each cell's
 * bounds by `inset`.
 *
 * The important part is that an edge facing a neighbour in the shape runs all
 * the way to the cell boundary (and a hair past it) rather than stopping at
 * the inset, and that a corner is only rounded when BOTH of its edges are on
 * the outside of the shape. Rounding every cell's corners instead — and
 * bridging them with a narrower connector — is what makes a straight 1x3
 * tunnel read as three pinched boxes rather than one capsule.
 */
function buildUnionPath(
  cells: GridCell[],
  cellRect: CellRectLookup,
  present: Set<string>,
  inset: number,
  cornerRadius: number
): Path2D {
  const path = new Path2D();
  const has = (x: number, z: number) => present.has(cellKey(x, z));

  for (const cell of cells) {
    const r = cellRect(cell.x, cell.z);
    const hasLeft = has(cell.x - 1, cell.z);
    const hasRight = has(cell.x + 1, cell.z);
    const hasUp = has(cell.x, cell.z + 1); // +z is up on screen
    const hasDown = has(cell.x, cell.z - 1);

    const left = hasLeft ? r.x - BLEED : r.x + inset;
    const right = hasRight ? r.x + r.width + BLEED : r.x + r.width - inset;
    const top = hasUp ? r.y - BLEED : r.y + inset;
    const bottom = hasDown ? r.y + r.height + BLEED : r.y + r.height - inset;

    path.roundRect(left, top, right - left, bottom - top, [
      !hasUp && !hasLeft ? cornerRadius : 0,
      !hasUp && !hasRight ? cornerRadius : 0,
      !hasDown && !hasRight ? cornerRadius : 0,
      !hasDown && !hasLeft ? cornerRadius : 0,
    ]);
  }
  return path;
}

/**
 * ONE hole drawn as ONE shape: the union of its cells, outlined by a single
 * continuous rim in the hole's color and filled with the dark void.
 *
 * The rim comes from filling the union in the rim color and then filling an
 * inset copy of the same union in the void color — stroking would draw a line
 * across every internal cell boundary and shatter one hole into N tiles.
 * Because each cell's region is inset by `pad`, two *separate* holes that
 * happen to sit next to each other still render with a visible seam between
 * them, so adjacent holes stay distinguishable.
 *
 * `capacity` belongs to the whole shape, so it is drawn once, on the cell
 * nearest the shape's centroid — never repeated per cell.
 */
export function drawHoleGroup(
  ctx: CanvasRenderingContext2D,
  cells: GridCell[],
  cellRect: CellRectLookup,
  colorHex: string,
  voidHex: string,
  capacity?: number,
  highlightHex?: string
) {
  if (cells.length === 0) return;

  const sample = cellRect(cells[0].x, cells[0].z);
  const size = Math.min(sample.width, sample.height);
  const pad = size * 0.12;
  const rim = Math.max(1.5, size * 0.085);
  const radius = size * 0.3;
  const present = new Set(cells.map((c) => cellKey(c.x, c.z)));

  // A selection halo is a *larger* union filled underneath, not a stroke:
  // stroking the union would outline each cell's subpath, internal edges and all.
  if (highlightHex) {
    const halo = Math.max(2, size * 0.05);
    ctx.fillStyle = highlightHex;
    ctx.fill(buildUnionPath(cells, cellRect, present, Math.max(0, pad - halo), radius + halo));
  }

  ctx.fillStyle = colorHex;
  ctx.fill(buildUnionPath(cells, cellRect, present, pad, radius));
  ctx.fillStyle = voidHex;
  ctx.fill(buildUnionPath(cells, cellRect, present, pad + rim, Math.max(0, radius - rim)));

  if (capacity !== undefined && size >= 20) {
    const avgX = cells.reduce((sum, c) => sum + c.x, 0) / cells.length;
    const avgZ = cells.reduce((sum, c) => sum + c.z, 0) / cells.length;
    let best = cells[0];
    let bestDistance = Number.POSITIVE_INFINITY;
    for (const cell of cells) {
      const distance = (cell.x - avgX) ** 2 + (cell.z - avgZ) ** 2;
      if (distance < bestDistance) {
        bestDistance = distance;
        best = cell;
      }
    }
    const r = cellRect(best.x, best.z);
    ctx.fillStyle = colorHex;
    ctx.font = `700 ${Math.round(size * 0.36)}px "JetBrains Mono", ui-monospace, monospace`;
    ctx.textAlign = "center";
    ctx.textBaseline = "middle";
    ctx.fillText(String(capacity), r.x + r.width / 2, r.y + r.height / 2);
  }
}
