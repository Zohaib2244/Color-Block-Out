import type { Direction } from "../types";

const DIRECTION_SCREEN_OFFSET: Record<Direction, { dx: number; dy: number }> = {
  // GridCanvas already flips rows so +Z is up on screen; Up/Down/Left/Right
  // map 1:1 onto screen directions with no further flipping needed.
  Up: { dx: 0, dy: -1 },
  Down: { dx: 0, dy: 1 },
  Left: { dx: -1, dy: 0 },
  Right: { dx: 1, dy: 0 },
};

/** Draws a cat as a filled circle in its color. */
export function drawCat(ctx: CanvasRenderingContext2D, rect: { x: number; y: number; width: number; height: number }, colorHex: string) {
  const cx = rect.x + rect.width / 2;
  const cy = rect.y + rect.height / 2;
  const radius = Math.min(rect.width, rect.height) * 0.32;
  ctx.beginPath();
  ctx.arc(cx, cy, radius, 0, Math.PI * 2);
  ctx.fillStyle = colorHex;
  ctx.fill();
  ctx.lineWidth = 1.5;
  ctx.strokeStyle = "rgba(0,0,0,0.35)";
  ctx.stroke();
  // small ear triangles for a "cat" read
  ctx.fillStyle = colorHex;
  const earSize = radius * 0.55;
  ctx.beginPath();
  ctx.moveTo(cx - radius * 0.7, cy - radius * 0.55);
  ctx.lineTo(cx - radius * 0.15, cy - radius * 0.95);
  ctx.lineTo(cx - radius * 0.15, cy - radius * 0.3);
  ctx.closePath();
  ctx.fill();
  ctx.beginPath();
  ctx.moveTo(cx + radius * 0.7, cy - radius * 0.55);
  ctx.lineTo(cx + radius * 0.15, cy - radius * 0.95);
  ctx.lineTo(cx + radius * 0.15, cy - radius * 0.3);
  ctx.closePath();
  ctx.fill();
  void earSize;
}

/**
 * Draws a hole as a pipe-like piece: a core circle with a stub protruding
 * toward each "open" neighbor direction, so a connected chain of holes reads
 * as a continuous tunnel and the rendering doubles as a sanity-check for
 * whether holeType/rotation were solved correctly.
 */
export function drawHole(
  ctx: CanvasRenderingContext2D,
  rect: { x: number; y: number; width: number; height: number },
  colorHex: string,
  openings: Direction[]
) {
  const cx = rect.x + rect.width / 2;
  const cy = rect.y + rect.height / 2;
  const coreRadius = Math.min(rect.width, rect.height) * 0.24;
  const stubWidth = coreRadius * 1.5;

  ctx.fillStyle = colorHex;
  for (const dir of openings) {
    const { dx, dy } = DIRECTION_SCREEN_OFFSET[dir];
    if (dx !== 0) {
      const x0 = dx > 0 ? cx : rect.x;
      const w = rect.width / 2 + coreRadius * 0.1;
      ctx.fillRect(x0, cy - stubWidth / 2, w, stubWidth);
    } else {
      const y0 = dy > 0 ? cy : rect.y;
      const h = rect.height / 2 + coreRadius * 0.1;
      ctx.fillRect(cx - stubWidth / 2, y0, stubWidth, h);
    }
  }

  ctx.beginPath();
  ctx.arc(cx, cy, coreRadius, 0, Math.PI * 2);
  ctx.fill();
  ctx.lineWidth = 1.5;
  ctx.strokeStyle = "rgba(0,0,0,0.4)";
  ctx.stroke();
}
