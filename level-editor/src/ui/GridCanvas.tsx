import { useCallback, useEffect, useRef, useState } from "react";
import { getCanvasPalette } from "../canvasPalette";
import { useTheme } from "../theme";
import type { GridCell } from "../types";

const LABEL_MARGIN = 24;
/** Every Nth line is drawn heavier, so counting cells on a big grid isn't a chore. */
const MAJOR_EVERY = 5;

/**
 * Where inside a cell a pointer landed. `u` runs 0 (west edge) to 1 (east),
 * `v` runs 0 (south edge) to 1 (north) — so it reads in grid space, not screen
 * space. Only edge-anchored content (gates) needs it; cell-anchored modes ignore it.
 */
export interface CellLocal {
  u: number;
  v: number;
}

export interface GridCanvasProps {
  width: number;
  length: number;
  cellPx?: number;
  showLabels?: boolean;
  /** Start of a paint gesture (pointer down on a cell). */
  onPaintStart?: (cell: GridCell, local: CellLocal) => void;
  /** Each new cell the pointer enters while the button is held. */
  onPaintDrag?: (cell: GridCell, local: CellLocal) => void;
  /** Draw a single cell's fill/content. Gridlines, crosshair and hover are drawn by GridCanvas. */
  renderCell: (ctx: CanvasRenderingContext2D, cell: GridCell, rect: { x: number; y: number; width: number; height: number }) => void;
  /**
   * Drawn on top of the cells and gridlines, with a lookup for any cell's
   * rect. Needed for shapes that span several cells and must be one
   * continuous form (a multi-cell hole), which per-cell drawing can't express.
   */
  renderOverlay?: (
    ctx: CanvasRenderingContext2D,
    cellRect: (x: number, z: number) => { x: number; y: number; width: number; height: number }
  ) => void;
  /** Cells the pointer may interact with; others still render but ignore paint/hover. Defaults to all cells. */
  isInteractive?: (cell: GridCell) => boolean;
}

/**
 * Renders a canvas grid using the same top-down, Z-flipped convention as
 * GridCreatorTool/CatLevelEditorWindow's editor GUIs: row 0 on screen is the
 * highest Z value, and Z increases going up (toward the viewer's "north").
 */
export function GridCanvas({
  width,
  length,
  cellPx = 32,
  showLabels = true,
  onPaintStart,
  onPaintDrag,
  renderCell,
  renderOverlay,
  isInteractive,
}: GridCanvasProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const [hovered, setHovered] = useState<GridCell | null>(null);
  const paintingRef = useRef(false);
  const lastPaintedRef = useRef<string | null>(null);
  const theme = useTheme();
  const palette = getCanvasPalette(theme);
  const margin = showLabels ? LABEL_MARGIN : 0;
  const canvasWidth = width * cellPx + margin;
  const canvasHeight = length * cellPx + margin;
  const interactive = !!onPaintStart;

  const cellFromEvent = useCallback(
    (evt: { clientX: number; clientY: number }): { cell: GridCell; local: CellLocal } | null => {
      const canvas = canvasRef.current;
      if (!canvas) return null;
      const bounds = canvas.getBoundingClientRect();
      const px = evt.clientX - bounds.left - margin;
      const py = evt.clientY - bounds.top - margin;
      if (px < 0 || py < 0) return null;
      const cx = px / cellPx;
      const cy = py / cellPx;
      const x = Math.floor(cx);
      const z = length - 1 - Math.floor(cy);
      if (x < 0 || x >= width || z < 0 || z >= length) return null;
      // Screen y grows downward while z grows upward, so v is the flipped remainder.
      return { cell: { x, z }, local: { u: cx - x, v: 1 - (cy - Math.floor(cy)) } };
    },
    [width, length, cellPx, margin]
  );

  useEffect(() => {
    const stopPainting = () => {
      paintingRef.current = false;
      lastPaintedRef.current = null;
    };
    window.addEventListener("pointerup", stopPainting);
    window.addEventListener("pointercancel", stopPainting);
    return () => {
      window.removeEventListener("pointerup", stopPainting);
      window.removeEventListener("pointercancel", stopPainting);
    };
  }, []);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const dpr = window.devicePixelRatio || 1;
    canvas.width = canvasWidth * dpr;
    canvas.height = canvasHeight * dpr;
    canvas.style.width = `${canvasWidth}px`;
    canvas.style.height = `${canvasHeight}px`;
    const ctx = canvas.getContext("2d");
    if (!ctx) return;
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.clearRect(0, 0, canvasWidth, canvasHeight);

    const gridX = margin;
    const gridY = margin;
    const gridW = width * cellPx;
    const gridH = length * cellPx;
    const cellRect = (x: number, z: number) => ({
      x: gridX + x * cellPx,
      y: gridY + (length - 1 - z) * cellPx,
      width: cellPx,
      height: cellPx,
    });

    // ── cell fills ────────────────────────────────────────────────
    for (let x = 0; x < width; x++) {
      for (let z = 0; z < length; z++) {
        renderCell(ctx, { x, z }, cellRect(x, z));
      }
    }

    // ── hover crosshair: tint the whole row + column so reading a cell's
    //    coordinates off a large grid doesn't need finger-tracing ─────
    if (hovered && (!isInteractive || isInteractive(hovered))) {
      const hoverRow = length - 1 - hovered.z;
      ctx.fillStyle = palette.selectedTint;
      ctx.globalAlpha = 0.28;
      ctx.fillRect(gridX + hovered.x * cellPx, gridY, cellPx, gridH);
      ctx.fillRect(gridX, gridY + hoverRow * cellPx, gridW, cellPx);
      ctx.globalAlpha = 1;
      ctx.fillRect(gridX + hovered.x * cellPx, gridY + hoverRow * cellPx, cellPx, cellPx);
    }

    // ── gridlines ─────────────────────────────────────────────────
    for (let x = 0; x <= width; x++) {
      const major = x % MAJOR_EVERY === 0 || x === width;
      ctx.strokeStyle = major ? palette.gridLineMajor : palette.gridLine;
      ctx.lineWidth = major ? 1.5 : 1;
      ctx.beginPath();
      ctx.moveTo(Math.round(gridX + x * cellPx) + 0.5, gridY);
      ctx.lineTo(Math.round(gridX + x * cellPx) + 0.5, gridY + gridH);
      ctx.stroke();
    }
    for (let row = 0; row <= length; row++) {
      const z = length - row;
      const major = z % MAJOR_EVERY === 0 || row === 0 || row === length;
      ctx.strokeStyle = major ? palette.gridLineMajor : palette.gridLine;
      ctx.lineWidth = major ? 1.5 : 1;
      ctx.beginPath();
      ctx.moveTo(gridX, Math.round(gridY + row * cellPx) + 0.5);
      ctx.lineTo(gridX + gridW, Math.round(gridY + row * cellPx) + 0.5);
      ctx.stroke();
    }

    // ── pieces, on top of the gridlines so nothing cuts through them ──
    renderOverlay?.(ctx, cellRect);

    // ── axis labels ───────────────────────────────────────────────
    if (showLabels) {
      const labelStep = cellPx < 18 ? MAJOR_EVERY : 1;
      ctx.font = `10px "JetBrains Mono", ui-monospace, monospace`;
      ctx.textBaseline = "middle";

      ctx.textAlign = "center";
      for (let x = 0; x < width; x++) {
        const isHover = hovered?.x === x;
        if (!isHover && x % labelStep !== 0) continue;
        ctx.fillStyle = isHover ? palette.textAccent : palette.textMuted;
        ctx.fillText(String(x), gridX + x * cellPx + cellPx / 2, margin / 2);
      }

      ctx.textAlign = "right";
      for (let z = 0; z < length; z++) {
        const isHover = hovered?.z === z;
        if (!isHover && z % labelStep !== 0) continue;
        const row = length - 1 - z;
        ctx.fillStyle = isHover ? palette.textAccent : palette.textMuted;
        ctx.fillText(String(z), margin - 7, gridY + row * cellPx + cellPx / 2);
      }
    }
  }, [
    width,
    length,
    cellPx,
    margin,
    canvasWidth,
    canvasHeight,
    showLabels,
    renderCell,
    renderOverlay,
    hovered,
    isInteractive,
    palette,
  ]);

  return (
    <div className="grid-canvas-shell">
      <canvas
        ref={canvasRef}
        className="grid-canvas"
        style={{ cursor: interactive ? "crosshair" : "default" }}
        onPointerDown={(evt) => {
          if (!onPaintStart) return;
          const hit = cellFromEvent(evt);
          if (!hit || (isInteractive && !isInteractive(hit.cell))) return;
          paintingRef.current = true;
          lastPaintedRef.current = `${hit.cell.x},${hit.cell.z}`;
          onPaintStart(hit.cell, hit.local);
        }}
        onPointerMove={(evt) => {
          const hit = cellFromEvent(evt);
          setHovered(hit?.cell ?? null);
          if (!paintingRef.current || !hit) return;
          const key = `${hit.cell.x},${hit.cell.z}`;
          if (key === lastPaintedRef.current) return;
          if (isInteractive && !isInteractive(hit.cell)) return;
          lastPaintedRef.current = key;
          (onPaintDrag ?? onPaintStart)?.(hit.cell, hit.local);
        }}
        onPointerLeave={() => setHovered(null)}
      />
      {showLabels && (
        <div className="grid-coord">
          {hovered ? (
            <>
              <span>x</span>
              {hovered.x}
              <span>z</span>
              {hovered.z}
            </>
          ) : (
            <span className="grid-coord-idle">hover the grid</span>
          )}
        </div>
      )}
    </div>
  );
}
