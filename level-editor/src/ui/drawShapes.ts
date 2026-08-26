import { cellKey } from "../types";
import type { Direction, GridCell } from "../types";

export interface CellRect {
  x: number;
  y: number;
  width: number;
  height: number;
}

export type CellRectLookup = (x: number, z: number) => CellRect;

/**
 * Outline and badge colours. Deliberately NOT theme tokens: an outline has to
 * separate nine saturated cat colours from each other and from both floor
 * colours, and a count badge has to stay readable on top of any of them. The
 * only pair that manages every combination is near-black ink with near-white
 * text, so those are fixed rather than themed.
 */
const INK = "#12100b";
const PAPER = "#f7f1e3";

/**
 * Fills `path`, having first laid a thick `INK` stroke underneath.
 *
 * Stroking *before* filling is what makes several overlapping sub-shapes (a
 * head plus two ears, a pile of bands) read as one outlined silhouette: the
 * stroke straddles each edge, and the fills that follow cover its inner half,
 * so only the outside of the union survives. Stroking afterwards would draw
 * every internal seam instead.
 */
function inkFill(ctx: CanvasRenderingContext2D, path: Path2D, fill: string, outline: number) {
  ctx.lineJoin = "round";
  ctx.lineCap = "round";
  ctx.strokeStyle = INK;
  ctx.lineWidth = outline * 2;
  ctx.stroke(path);
  ctx.fillStyle = fill;
  ctx.fill(path);
}

/** A count chip: dark disc, light number. Used for stack depth and gate queue length. */
function drawBadge(ctx: CanvasRenderingContext2D, cx: number, cy: number, r: number, text: string) {
  ctx.beginPath();
  ctx.arc(cx, cy, r, 0, Math.PI * 2);
  ctx.fillStyle = INK;
  ctx.fill();
  ctx.strokeStyle = PAPER;
  ctx.lineWidth = Math.max(1, r * 0.16);
  ctx.stroke();

  ctx.fillStyle = PAPER;
  ctx.font = `700 ${Math.round(r * 1.25)}px "JetBrains Mono", ui-monospace, monospace`;
  ctx.textAlign = "center";
  ctx.textBaseline = "middle";
  ctx.fillText(text, cx, cy + r * 0.04);
}

/** Two eyes centred on (cx, cy), spaced by `spread`. Callers skip them when too small to read. */
function drawEyes(ctx: CanvasRenderingContext2D, cx: number, cy: number, spread: number, r: number) {
  ctx.fillStyle = INK;
  ctx.beginPath();
  ctx.arc(cx - spread, cy, r, 0, Math.PI * 2);
  ctx.fill();
  ctx.beginPath();
  ctx.arc(cx + spread, cy, r, 0, Math.PI * 2);
  ctx.fill();
}

/** A pair of ear triangles rising from a shape of width `w` centred on `cx`, based at `baseY`. */
function earPaths(cx: number, baseY: number, w: number, h: number): [Path2D, Path2D] {
  const left = new Path2D();
  left.moveTo(cx - w * 0.46, baseY);
  left.lineTo(cx - w * 0.3, baseY - h);
  left.lineTo(cx - w * 0.04, baseY);
  left.closePath();

  const right = new Path2D();
  right.moveTo(cx + w * 0.46, baseY);
  right.lineTo(cx + w * 0.3, baseY - h);
  right.lineTo(cx + w * 0.04, baseY);
  right.closePath();

  return [left, right];
}

/**
 * A cat: a solid, outlined creature that sits ON the floor — round head, two
 * pointed ears, two eyes. Deliberately the visual opposite of a hole (which
 * is a dark recess cut INTO the floor), so the two can never be confused at
 * a glance even when they share a color.
 */
export function drawCat(ctx: CanvasRenderingContext2D, rect: CellRect, colorHex: string) {
  const cx = rect.x + rect.width / 2;
  const size = Math.min(rect.width, rect.height);
  const r = size * 0.3;
  const headY = rect.y + rect.height / 2 + r * 0.12;
  const outline = Math.max(1, size * 0.038);

  const [leftEar, rightEar] = earPaths(cx, headY - r * 0.45, r * 1.9, r * 0.95);
  const head = new Path2D();
  head.arc(cx, headY, r, 0, Math.PI * 2);

  inkFill(ctx, leftEar, colorHex, outline);
  inkFill(ctx, rightEar, colorHex, outline);
  inkFill(ctx, head, colorHex, outline);

  // eyes — skipped when a cell is too small for them to read as anything but noise
  if (size >= 16) drawEyes(ctx, cx, headY - r * 0.04, r * 0.37, Math.max(1, r * 0.16));
}

/** How many cats of a stack get their own band before the badge alone carries the count. */
const MAX_STACK_BANDS = 5;

/**
 * A stack of cats on one cell, drawn as a pile of separately outlined bands —
 * one band per cat, running up the cell in stack order — with the topmost band
 * wearing the ears and a bright rim, and a badge carrying the true total.
 *
 * Bands rather than overlapping cat heads: the whole question a designer asks
 * of a stacked cell is "which colours, in what order, how many", and heads
 * drawn on top of one another answer none of the three. Each band is separated
 * by an ink outline and a gap, so adjacent colours stay countable even when
 * they are near neighbours on the palette.
 *
 * `colors` runs bottom (index 0) to top, matching the level's cat order.
 */
export function drawCatStack(ctx: CanvasRenderingContext2D, rect: CellRect, colors: string[]) {
  if (colors.length === 0) return;
  if (colors.length === 1) {
    drawCat(ctx, rect, colors[0]);
    return;
  }

  const size = Math.min(rect.width, rect.height);
  // Only the top few get a band; a deeper pile leans on the badge for the count.
  const shown = colors.slice(-MAX_STACK_BANDS);
  const n = shown.length;
  const hidden = colors.length - n;

  const outline = Math.max(0.9, size * 0.032);
  const gap = Math.max(1.2, size * 0.05);
  const bandH = Math.min(size * 0.2, (size * 0.76 - gap * (n - 1)) / n);
  const pileH = bandH * n + gap * (n - 1);
  const bandW = size * 0.62;
  const earH = bandH * 0.9;

  const cx = rect.x + rect.width / 2;
  const x = cx - bandW / 2;
  // The ears are part of the silhouette, so centre pile+ears rather than the pile alone.
  const top = rect.y + (rect.height - (pileH + earH)) / 2 + earH;
  const radius = Math.min(bandH * 0.46, bandW * 0.2);
  /** i counts from the bottom of the shown run, so index n-1 is the topmost cat. */
  const bandY = (i: number) => top + (n - 1 - i) * (bandH + gap);

  // A pile deeper than the bands can show gets a stub peeking out below the
  // bottom band, so "there is more under this" is visible and not only counted.
  if (hidden > 0) {
    const stub = new Path2D();
    stub.roundRect(x + bandW * 0.14, bandY(0) + bandH * 0.55, bandW * 0.72, bandH * 0.72, radius * 0.7);
    inkFill(ctx, stub, INK, outline);
  }

  for (let i = 0; i < n; i++) {
    const isTop = i === n - 1;
    const band = new Path2D();
    band.roundRect(x, bandY(i), bandW, bandH, radius);

    if (isTop) {
      const [leftEar, rightEar] = earPaths(cx, bandY(i) + bandH * 0.5, bandW, earH);
      inkFill(ctx, leftEar, shown[i], outline);
      inkFill(ctx, rightEar, shown[i], outline);
    }

    inkFill(ctx, band, shown[i], outline);

    if (isTop) {
      // A light rim on the top band: the top cat is the only one a hole can
      // take, so it has to be identifiable without counting from the bottom.
      ctx.strokeStyle = PAPER;
      ctx.lineWidth = Math.max(1, size * 0.022);
      ctx.stroke(band);
      if (bandH >= 9) drawEyes(ctx, cx, bandY(i) + bandH * 0.5, bandW * 0.19, Math.max(1, bandH * 0.15));
    }
  }

  if (size >= 13) {
    const r = size * 0.19;
    drawBadge(
      ctx,
      rect.x + rect.width - r - size * 0.05,
      rect.y + rect.height - r - size * 0.05,
      r,
      String(colors.length)
    );
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

  if (capacity !== undefined && size >= 14) {
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
    // Outlined rather than plain: a dim colour (Purple, Blue) set straight onto
    // the near-black void is otherwise close to unreadable.
    ctx.font = `700 ${Math.round(size * 0.4)}px "JetBrains Mono", ui-monospace, monospace`;
    ctx.textAlign = "center";
    ctx.textBaseline = "middle";
    ctx.lineJoin = "round";
    ctx.strokeStyle = INK;
    ctx.lineWidth = Math.max(2, size * 0.09);
    ctx.strokeText(String(capacity), r.x + r.width / 2, r.y + r.height / 2);
    ctx.fillStyle = colorHex;
    ctx.fillText(String(capacity), r.x + r.width / 2, r.y + r.height / 2);
  }
}

/** Most queued cats shown as pips on the gate before the badge alone carries the count. */
const MAX_GATE_PIPS = 4;

/**
 * A gate: a bar laid along ONE boundary edge of a cell, standing in for the
 * wall Unity would otherwise build there.
 *
 * It is drawn just inside its own cell rather than out in the neighbouring
 * blocked cell, so a gate on the outermost row stays on the canvas instead of
 * colliding with the axis labels — and so it reads as belonging to the cell a
 * hole gets parked on, which is the cell that actually matters in play.
 *
 * The bar only ever shows the head of the queue — the first few pips, ringed
 * on the one that leaves next, plus a badge with the true length. A bar one
 * cell long cannot legibly hold a queue of nine, so the full sequence is the
 * Stack Visualizer's job and this is the at-a-glance summary.
 */
export function drawGate(
  ctx: CanvasRenderingContext2D,
  rect: CellRect,
  side: Direction,
  catColorHexes: string[],
  bodyHex: string,
  highlightHex?: string
) {
  const size = Math.min(rect.width, rect.height);
  const thickness = size * 0.3;
  const span = size * 0.9;
  const radius = thickness * 0.42;
  const vertical = side === "Left" || side === "Right";

  // The bar hugs its edge; `inner` corners (facing into the cell) are the rounded ones.
  const along = rect.x + rect.width / 2 - span / 2;
  const alongV = rect.y + rect.height / 2 - span / 2;
  let bar: CellRect;
  let corners: [number, number, number, number];
  switch (side) {
    case "Up": // +z is up on screen
      bar = { x: along, y: rect.y, width: span, height: thickness };
      corners = [0, 0, radius, radius];
      break;
    case "Down":
      bar = { x: along, y: rect.y + rect.height - thickness, width: span, height: thickness };
      corners = [radius, radius, 0, 0];
      break;
    case "Left":
      bar = { x: rect.x, y: alongV, width: thickness, height: span };
      corners = [0, radius, radius, 0];
      break;
    default: // Right
      bar = { x: rect.x + rect.width - thickness, y: alongV, width: thickness, height: span };
      corners = [radius, 0, 0, radius];
      break;
  }

  if (highlightHex) {
    const halo = Math.max(2, size * 0.05);
    ctx.fillStyle = highlightHex;
    ctx.beginPath();
    ctx.roundRect(bar.x - halo, bar.y - halo, bar.width + halo * 2, bar.height + halo * 2, radius + halo);
    ctx.fill();
  }

  ctx.fillStyle = bodyHex;
  ctx.beginPath();
  ctx.roundRect(bar.x, bar.y, bar.width, bar.height, corners);
  ctx.fill();
  ctx.strokeStyle = INK;
  ctx.lineWidth = Math.max(1, size * 0.03);
  ctx.stroke();

  if (catColorHexes.length === 0 || size < 14) return;

  // Pips run along the bar's long axis: left-to-right, or bottom-to-top.
  const shown = catColorHexes.slice(0, MAX_GATE_PIPS);
  const length = vertical ? bar.height : bar.width;
  const step = length / MAX_GATE_PIPS;
  const pipR = Math.min(thickness * 0.32, step * 0.36);
  const cross = vertical ? bar.x + bar.width / 2 : bar.y + bar.height / 2;
  const outline = Math.max(0.9, size * 0.026);

  shown.forEach((hex, i) => {
    // Vertical bars count from the bottom so "first" is the low-z end.
    const offset = vertical ? bar.y + bar.height - (i + 0.5) * step : bar.x + (i + 0.5) * step;
    const cx = vertical ? cross : offset;
    const cy = vertical ? offset : cross;

    const pip = new Path2D();
    pip.arc(cx, cy, i === 0 ? pipR * 1.3 : pipR, 0, Math.PI * 2);
    inkFill(ctx, pip, hex, outline);

    if (i === 0) {
      // The head of the queue decides the next move on its own, so it is ringed.
      ctx.strokeStyle = PAPER;
      ctx.lineWidth = Math.max(1, size * 0.025);
      ctx.stroke(pip);
    }
  });

  if (size >= 18) {
    const r = size * 0.16;
    // Off the bar, and never in the bottom-right corner — that one belongs to
    // the cat-stack badge, and a gate's cell can legally carry cats too.
    const bx = side === "Left" ? rect.x + rect.width - r - size * 0.05 : rect.x + r + size * 0.05;
    const by = side === "Up" ? rect.y + rect.height - r - size * 0.05 : rect.y + r + size * 0.05;
    drawBadge(ctx, bx, by, r, String(catColorHexes.length));
  }
}
