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
  capacity?: number
) {
  if (cells.length === 0) return;

  const sample = cellRect(cells[0].x, cells[0].z);
  const size = Math.min(sample.width, sample.height);
  const pad = size * 0.13;
  const rim = Math.max(1.5, size * 0.085);
  const radius = size * 0.26;
  const present = new Set(cells.map((c) => cellKey(c.x, c.z)));

  const outer = new Path2D();
  const inner = new Path2D();

  for (const cell of cells) {
    const r = cellRect(cell.x, cell.z);
    outer.roundRect(r.x + pad, r.y + pad, r.width - pad * 2, r.height - pad * 2, radius);
    inner.roundRect(
      r.x + pad + rim,
      r.y + pad + rim,
      r.width - (pad + rim) * 2,
      r.height - (pad + rim) * 2,
      Math.max(0, radius - rim)
    );

    // Bridge to the +x / +z neighbours only, so each internal edge is built once.
    // The inner bridge starts at the neighbouring cells' *inner* edges (inset by
    // rim), not the outer ones — starting at the outer edge would leave a
    // rim-wide band of rim color across the tunnel and visibly seam the shape.
    if (present.has(cellKey(cell.x + 1, cell.z))) {
      const n = cellRect(cell.x + 1, cell.z);
      const x0 = r.x + r.width - pad;
      const x1 = n.x + pad;
      outer.rect(x0, r.y + pad, x1 - x0, r.height - pad * 2);
      inner.rect(x0 - rim, r.y + pad + rim, x1 - x0 + rim * 2, r.height - (pad + rim) * 2);
    }
    if (present.has(cellKey(cell.x, cell.z + 1))) {
      // +z is up on screen, so the neighbour's rect sits above this one
      const n = cellRect(cell.x, cell.z + 1);
      const y0 = n.y + n.height - pad;
      const y1 = r.y + pad;
      outer.rect(r.x + pad, y0, r.width - pad * 2, y1 - y0);
      inner.rect(r.x + pad + rim, y0 - rim, r.width - (pad + rim) * 2, y1 - y0 + rim * 2);
    }
  }

  ctx.fillStyle = colorHex;
  ctx.fill(outer);
  ctx.fillStyle = voidHex;
  ctx.fill(inner);

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
