import { useState } from "react";
import { buildPresetCells, openingsForHole } from "../level/holeShape";
import { createPreset, deletePreset, listPresets } from "../level/holePresetLibrary";
import { cellKey } from "../types";
import type { GridCell, HoleShapePreset } from "../types";
import { GridCanvas } from "./GridCanvas";
import { drawHole } from "./drawShapes";

const SANDBOX_SIZE = 9;
const SANDBOX_CELL_PX = 40;
const PREVIEW_CELL_PX = 24;
const PREVIEW_COLOR = "#8fd3ff";
const FLOOR_COLOR = "#20242c";
const EMPTY_COLOR = "#15171c";

interface PresetDesignerProps {
  onPresetsChanged?: () => void;
}

export function PresetDesigner({ onPresetsChanged }: PresetDesignerProps) {
  const [presets, setPresets] = useState<HoleShapePreset[]>(() => listPresets());
  const [selectedCells, setSelectedCells] = useState<Set<string>>(new Set());
  const [name, setName] = useState("");

  function refresh() {
    setPresets(listPresets());
    onPresetsChanged?.();
  }

  function toggleCell(cell: GridCell) {
    setSelectedCells((prev) => {
      const key = cellKey(cell.x, cell.z);
      const next = new Set(prev);
      if (next.has(key)) next.delete(key);
      else next.add(key);
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
              <PresetPreview preset={preset} />
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
          Draw a connected hole shape below, same as Level Editor's Holes (shape) mode, then save it as a reusable
          preset. Presets aren't tied to any grid or color — you'll pick both when stamping one into a level.
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
            onCellClick={toggleCell}
            renderCell={(ctx, cell, rect) => {
              ctx.fillStyle = FLOOR_COLOR;
              ctx.fillRect(rect.x, rect.y, rect.width, rect.height);
              if (selectedCells.has(cellKey(cell.x, cell.z))) {
                ctx.fillStyle = "rgba(0, 180, 200, 0.35)";
                ctx.fillRect(rect.x, rect.y, rect.width, rect.height);
              }
            }}
          />
        </div>
      </main>
    </div>
  );
}

function PresetPreview({ preset }: { preset: HoleShapePreset }) {
  const width = Math.max(1, ...preset.cells.map((c) => c.dx + 1));
  const length = Math.max(1, ...preset.cells.map((c) => c.dz + 1));
  const byCell = new Map(preset.cells.map((c) => [cellKey(c.dx, c.dz), c]));

  return (
    <GridCanvas
      width={width}
      length={length}
      cellPx={PREVIEW_CELL_PX}
      showLabels={false}
      renderCell={(ctx, cell, rect) => {
        const preview = byCell.get(cellKey(cell.x, cell.z));
        ctx.fillStyle = preview ? FLOOR_COLOR : EMPTY_COLOR;
        ctx.fillRect(rect.x, rect.y, rect.width, rect.height);
        if (preview) {
          drawHole(ctx, rect, PREVIEW_COLOR, openingsForHole(preview.holeType, preview.rotationQuarterTurns));
        }
      }}
    />
  );
}
