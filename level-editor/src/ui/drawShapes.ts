import type { Direction } from "../types";

const DIRECTION_SCREEN_OFFSET: Record<Direction, { dx: number; dy: number }> = {
  // GridCanvas already flips rows so +Z is up on screen; Up/Down/Left/Right
  // map 1:1 onto screen directions with no further flipping needed.
  Up: { dx: 0, dy: -1 },
  Down: { dx: 0, dy: 1 },
  Left: { dx: -1, dy: 0 },
  Right: { dx: 1, dy: 0 },
};

export interface CellRect {
  x: number;
  y: number;
  width: number;
  height: number;
}

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

  // ears
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

  // head
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
 * A hole: a dark socket cut into the floor, ringed by a rim in the hole's
 * color, with a tunnel stub running to the cell edge for each open side so a
 * connected shape reads as one continuous tunnel. The rim is drawn by filling
 * the union of (socket + stubs) in the rim color and then filling an inset
 * copy of that same union in the void color — stroking would draw rim lines
 * straight across each tunnel mouth and break the continuity.
 *
 * `capacity` (how many cats the hole can swallow) is printed in the socket.
 */
export function drawHole(
  ctx: CanvasRenderingContext2D,
  rect: CellRect,
  colorHex: string,
  openings: Direction[],
  voidHex: string,
  capacity?: number
) {
  const cx = rect.x + rect.width / 2;
  const cy = rect.y + rect.height / 2;
  const size = Math.min(rect.width, rect.height);
  const socket = size * 0.64;
  const rim = Math.max(1.5, size * 0.085);
  const stub = socket * 0.5;
  const radius = socket * 0.3;

  const outer = new Path2D();
  const inner = new Path2D();

  outer.roundRect(cx - socket / 2, cy - socket / 2, socket, socket, radius);
  inner.roundRect(
    cx - socket / 2 + rim,
    cy - socket / 2 + rim,
    socket - rim * 2,
    socket - rim * 2,
    Math.max(0, radius - rim)
  );

  for (const dir of openings) {
    const { dx, dy } = DIRECTION_SCREEN_OFFSET[dir];
    if (dx !== 0) {
      // +0.5 so stubs of adjacent holes overlap and the tunnel has no seam
      const x0 = dx > 0 ? cx : rect.x - 0.5;
      const w = rect.width / 2 + 0.5;
      outer.rect(x0, cy - stub / 2, w, stub);
      inner.rect(x0, cy - stub / 2 + rim, w, stub - rim * 2);
    } else {
      const y0 = dy > 0 ? cy : rect.y - 0.5;
      const h = rect.height / 2 + 0.5;
      outer.rect(cx - stub / 2, y0, stub, h);
      inner.rect(cx - stub / 2 + rim, y0, stub - rim * 2, h);
    }
  }

  ctx.fillStyle = colorHex;
  ctx.fill(outer);
  ctx.fillStyle = voidHex;
  ctx.fill(inner);

  if (capacity !== undefined && size >= 22) {
    ctx.fillStyle = colorHex;
    ctx.font = `700 ${Math.round(size * 0.33)}px "JetBrains Mono", ui-monospace, monospace`;
    ctx.textAlign = "center";
    ctx.textBaseline = "middle";
    ctx.fillText(String(capacity), cx, cy + size * 0.015);
  }
}
