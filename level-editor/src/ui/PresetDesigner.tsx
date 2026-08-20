import { useRef, useState } from "react";
import { getCanvasPalette } from "../canvasPalette";
import { createPreset, deletePreset, listPresets } from "../level/holePresetLibrary";
import { buildPresetCells } from "../level/holeShape";
import { useTheme } from "../theme";
import { cellKey } from "../types";
import type { GridCell, HoleShapePreset } from "../types";
import { GridCanvas } from "./GridCanvas";
import { drawHoleGroup } from "./drawShapes";

const SANDBOX_SIZE = 9;
const SANDBOX_CELL_PX = 44;
const PREVIEW_CELL_PX = 22;
const PREVIEW_COLOR_DARK = "#00b4c8";
const PREVIEW_COLOR_LIGHT = "#00768a";

interface PresetDesignerProps {
  onPresetsChanged?: () => void;
}

export function PresetDesigner({ onPresetsChanged }: PresetDesignerProps) {
  const [presets, setPresets] = useState<HoleShapePreset[]>(() => listPresets());
  const [selectedCells, setSelectedCells] = useState<Set<string>>(new Set());
  const [name, setName] = useState("");
  const theme = useTheme();
  const palette = getCanvasPalette(theme);
  const previewColor = theme === "light" ? PREVIEW_COLOR_LIGHT : PREVIEW_COLOR_DARK;
  /** Whether the current drag gesture is adding or removing cells. */
  const paintModeRef = useRef(true);

  function refresh() {
    setPresets(listPresets());
    onPresetsChanged?.();
  }

  function beginPaint(cell: GridCell) {
    paintModeRef.current = !selectedCells.has(cellKey(cell.x, cell.z));
    applyPaint(cell);
  }

  function applyPaint(cell: GridCell) {
    setSelectedCells((prev) => {
      const key = cellKey(cell.x, cell.z);
      if (prev.has(key) === paintModeRef.current) return prev;
      const next = new Set(prev);
      if (paintModeRef.current) next.add(key);
      else next.delete(key);
      return next;
    });
  }

  function handleSave() {
    if (!name.trim() || selectedCells.size === 0) return;
    const cells = [...selectedCells].map((key) => {
      const [x, z] = key.split(",").map(Number);
      return { x, z };
    });
    createPreset(name.trim(), buildPresetCells(cells));
    setName("");
    setSelectedCells(new Set());
    refresh();
  }

  function handleDelete(id: string) {
    deletePreset(id);
    refresh();
  }

  return (
    <div className="screen">
      <aside className="sidebar">
        <div className="sidebar-header">
          <h2>Saved Presets</h2>
        </div>
        <ul className="preset-list">
          {presets.map((preset) => (
            <li key={preset.id}>
              <PresetPreview preset={preset} color={previewColor} voidHex={palette.holeVoid} floorHex={palette.floor} inertHex={palette.inert} />
              <div className="preset-list-item-body">
                <span>{preset.name}</span>
                <span className="muted">{preset.cells.length} cells</span>
              </div>
              <button className="danger-link" onClick={() => handleDelete(preset.id)} title="Delete preset">
                ×
              </button>
            </li>
          ))}
          {presets.length === 0 && <li className="muted empty">No saved presets yet.</li>}
        </ul>
      </aside>

      <main className="editor-main">
        <p className="hint">
          Click or <strong>drag</strong> to draw a connected hole shape, same as Level Editor's Holes (shape) mode,
          then save it as a reusable preset. A preset stores the shape only — grid, color and cat capacity are all
          chosen when you stamp it into a level.
        </p>

        <div className="field-row">
          <label>
            Preset Name
            <input value={name} onChange={(e) => setName(e.target.value)} placeholder="e.g. Zigzag" />
          </label>
          <button onClick={() => setSelectedCells(new Set())} disabled={selectedCells.size === 0}>
            Clear ({selectedCells.size})
          </button>
          <button className="primary" onClick={handleSave} disabled={!name.trim() || selectedCells.size === 0}>
            Save as Preset
          </button>
        </div>

        <div className="canvas-wrap">
          <GridCanvas
            width={SANDBOX_SIZE}
            length={SANDBOX_SIZE}
            cellPx={SANDBOX_CELL_PX}
            onPaintStart={beginPaint}
            onPaintDrag={applyPaint}
            renderCell={(ctx, cell, rect) => {
              ctx.fillStyle = palette.floor;
              ctx.fillRect(rect.x, rect.y, rect.width, rect.height);
              if (selectedCells.has(cellKey(cell.x, cell.z))) {
                ctx.fillStyle = palette.selectedTint;
                ctx.fillRect(rect.x, rect.y, rect.width, rect.height);
              }
            }}
          />
        </div>
      </main>
    </div>
  );
}

function PresetPreview({
  preset,
  color,
  voidHex,
  floorHex,
  inertHex,
}: {
  preset: HoleShapePreset;
  color: string;
  voidHex: string;
  floorHex: string;
  inertHex: string;
}) {
  const width = Math.max(1, ...preset.cells.map((c) => c.dx + 1));
  const length = Math.max(1, ...preset.cells.map((c) => c.dz + 1));
  const occupied = new Set(preset.cells.map((c) => cellKey(c.dx, c.dz)));
  const shape = preset.cells.map((c) => ({ x: c.dx, z: c.dz }));

  return (
    <GridCanvas
      width={width}
      length={length}
      cellPx={PREVIEW_CELL_PX}
      showLabels={false}
      renderCell={(ctx, cell, rect) => {
        ctx.fillStyle = occupied.has(cellKey(cell.x, cell.z)) ? floorHex : inertHex;
        ctx.fillRect(rect.x, rect.y, rect.width, rect.height);
      }}
      renderOverlay={(ctx, cellRect) => drawHoleGroup(ctx, shape, cellRect, color, voidHex)}
    />
  );
}
