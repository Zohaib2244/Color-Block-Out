import { useCallback, useEffect, useRef, useState } from "react";
import type { GridCell } from "../types";

const LABEL_MARGIN = 22;
const BORDER_COLOR = "#3d3220";

export interface GridCanvasProps {
  width: number;
  length: number;
  cellPx?: number;
  showLabels?: boolean;
  /** Called on click of a cell; omit to make the canvas display-only. */
  onCellClick?: (cell: GridCell) => void;
  /** Draw a single cell's fill/content. Border + hover overlay are handled by GridCanvas itself. */
  renderCell: (ctx: CanvasRenderingContext2D, cell: GridCell, rect: DOMRect | { x: number; y: number; width: number; height: number }) => void;
  /** Cells the pointer may interact with; others still render but ignore clicks/hover. Defaults to all cells. */
  isInteractive?: (cell: GridCell) => boolean;
}

/**
 * Renders a canvas grid using the same top-down, Z-flipped convention as
 * GridCreatorTool/CatLevelEditorWindow's editor GUIs: row 0 on screen is the
 * highest Z value, and Z increases going up (toward the viewer's "north").
 */
export function GridCanvas({ width, length, cellPx = 32, showLabels = true, onCellClick, renderCell, isInteractive }: GridCanvasProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const [hovered, setHovered] = useState<GridCell | null>(null);
  const margin = showLabels ? LABEL_MARGIN : 0;
  const canvasWidth = width * cellPx + margin;
  const canvasHeight = length * cellPx + margin;

  const cellFromEvent = useCallback(
    (evt: { clientX: number; clientY: number }): GridCell | null => {
      const canvas = canvasRef.current;
      if (!canvas) return null;
      const rect = canvas.getBoundingClientRect();
      const px = evt.clientX - rect.left - margin;
      const py = evt.clientY - rect.top - margin;
      if (px < 0 || py < 0) return null;
      const x = Math.floor(px / cellPx);
      const row = Math.floor(py / cellPx);
      const z = length - 1 - row;
      if (x < 0 || x >= width || z < 0 || z >= length) return null;
      return { x, z };
    },
    [width, length, cellPx, margin]
  );

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

    if (showLabels) {
      ctx.fillStyle = "#9f9887";
      ctx.font = "10px 'JetBrains Mono', monospace";
      ctx.textAlign = "center";
      ctx.textBaseline = "middle";
      for (let x = 0; x < width; x++) {
        ctx.fillText(String(x), margin + x * cellPx + cellPx / 2, margin / 2);
      }
      ctx.textAlign = "right";
      for (let z = 0; z < length; z++) {
        const row = length - 1 - z;
        ctx.fillText(String(z), margin - 6, margin + row * cellPx + cellPx / 2);
      }
    }

    for (let x = 0; x < width; x++) {
      for (let z = 0; z < length; z++) {
        const row = length - 1 - z;
        const rect = { x: margin + x * cellPx, y: margin + row * cellPx, width: cellPx, height: cellPx };
        renderCell(ctx, { x, z }, rect);

        if (hovered && hovered.x === x && hovered.z === z && (!isInteractive || isInteractive({ x, z }))) {
          ctx.fillStyle = "rgba(0, 180, 200, 0.25)";
          ctx.fillRect(rect.x, rect.y, rect.width, rect.height);
        }

        ctx.strokeStyle = BORDER_COLOR;
        ctx.lineWidth = 1;
        ctx.strokeRect(rect.x + 0.5, rect.y + 0.5, rect.width - 1, rect.height - 1);
      }
    }
  }, [width, length, cellPx, margin, canvasWidth, canvasHeight, showLabels, renderCell, hovered, isInteractive]);

  return (
    <canvas
      ref={canvasRef}
      className="grid-canvas"
      onMouseMove={(evt) => setHovered(cellFromEvent(evt))}
      onMouseLeave={() => setHovered(null)}
      onClick={(evt) => {
        const cell = cellFromEvent(evt);
        if (!cell || !onCellClick) return;
        if (isInteractive && !isInteractive(cell)) return;
        onCellClick(cell);
      }}
    />
  );
}
