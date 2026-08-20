import { useCallback, useMemo, useState } from "react";
import { computePlayableMask, gridIndex, makeFlatGrid } from "../grid/floodFill";
import { deleteGrid, listGrids, saveGrid } from "../grid/gridLibrary";
import { newId } from "../storage";
import type { SavedGrid } from "../types";
import { GridCanvas } from "./GridCanvas";

const WALL_COLOR = "#55565c";
const PLAYABLE_COLOR = "#1f5c3a";
const EXTERIOR_COLOR = "#3a2f22";

const MAX_CANVAS_DIMENSION_PX = 640;

interface GridDesignerProps {
  onGridsChanged?: () => void;
}

export function GridDesigner({ onGridsChanged }: GridDesignerProps) {
  const [grids, setGrids] = useState<SavedGrid[]>(() => listGrids());
  const [draft, setDraft] = useState<SavedGrid>(() => makeBlankDraft());

  const refreshGrids = useCallback(() => {
    setGrids(listGrids());
    onGridsChanged?.();
  }, [onGridsChanged]);

  const playableMask = useMemo(
    () => computePlayableMask(draft.width, draft.length, draft.wallToggles),
    [draft.width, draft.length, draft.wallToggles]
  );

  const cellPx = useMemo(() => {
    const largestDimension = Math.max(draft.width, draft.length);
    return Math.max(12, Math.min(32, Math.floor(MAX_CANVAS_DIMENSION_PX / largestDimension)));
  }, [draft.width, draft.length]);

  function loadGrid(grid: SavedGrid) {
    setDraft({ ...grid, wallToggles: [...grid.wallToggles] });
  }

  function newGrid() {
    setDraft(makeBlankDraft());
  }

  function resize(width: number, length: number) {
    setDraft((prev) => {
      const clampedW = Math.max(1, Math.min(64, width));
      const clampedL = Math.max(1, Math.min(64, length));
      const next = makeFlatGrid(clampedW, clampedL, false);
      for (let x = 0; x < Math.min(prev.width, clampedW); x++) {
        for (let z = 0; z < Math.min(prev.length, clampedL); z++) {
          next[gridIndex(clampedW, x, z)] = prev.wallToggles[gridIndex(prev.width, x, z)];
        }
      }
      return { ...prev, width: clampedW, length: clampedL, wallToggles: next };
    });
  }

  function applyPreset(preset: "playable" | "walls" | "outer") {
    setDraft((prev) => {
      const next = makeFlatGrid(prev.width, prev.length, preset === "walls");
      if (preset === "outer") {
        for (let x = 0; x < prev.width; x++) {
          next[gridIndex(prev.width, x, 0)] = true;
          next[gridIndex(prev.width, x, prev.length - 1)] = true;
        }
        for (let z = 0; z < prev.length; z++) {
          next[gridIndex(prev.width, 0, z)] = true;
          next[gridIndex(prev.width, prev.width - 1, z)] = true;
        }
      }
      return { ...prev, wallToggles: next };
    });
  }

  function toggleCell(x: number, z: number) {
    setDraft((prev) => {
      const next = [...prev.wallToggles];
      const idx = gridIndex(prev.width, x, z);
      next[idx] = !next[idx];
      return { ...prev, wallToggles: next };
    });
  }

  function handleSave() {
    if (!draft.name.trim()) return;
    const saved: SavedGrid = { ...draft, name: draft.name.trim(), id: draft.id || newId() };
    saveGrid(saved);
    setDraft({ ...saved, wallToggles: [...saved.wallToggles] });
    refreshGrids();
  }

  function handleDelete(id: string) {
    deleteGrid(id);
    if (draft.id === id) newGrid();
    refreshGrids();
  }

  const playableCount = playableMask.filter(Boolean).length;

  return (
    <div className="screen">
      <aside className="sidebar">
        <div className="sidebar-header">
          <h2>Grids</h2>
          <button onClick={newGrid}>+ New</button>
        </div>
        <ul className="library-list">
          {grids.map((grid) => (
            <li key={grid.id} className={grid.id === draft.id ? "active" : ""}>
              <button className="library-item" onClick={() => loadGrid(grid)}>
                <span>{grid.name}</span>
                <span className="muted">
                  {grid.width}×{grid.length}
                </span>
              </button>
              <button className="danger-link" onClick={() => handleDelete(grid.id)} title="Delete grid">
                ×
              </button>
            </li>
          ))}
          {grids.length === 0 && <li className="muted empty">No saved grids yet.</li>}
        </ul>
      </aside>

      <main className="editor-main">
        <div className="field-row">
          <label>
            Name
            <input
              value={draft.name}
              onChange={(e) => setDraft((prev) => ({ ...prev, name: e.target.value }))}
              placeholder="New Grid"
            />
          </label>
          <label>
            Width
            <input type="number" min={1} max={64} value={draft.width} onChange={(e) => resize(Number(e.target.value), draft.length)} />
          </label>
          <label>
            Length
            <input type="number" min={1} max={64} value={draft.length} onChange={(e) => resize(draft.width, Number(e.target.value))} />
          </label>
          <label>
            Cell Size
            <input
              type="number"
              step={0.1}
              min={0.01}
              value={draft.cellSize}
              onChange={(e) => setDraft((prev) => ({ ...prev, cellSize: Number(e.target.value) }))}
            />
          </label>
        </div>

        <div className="field-row">
          <button onClick={() => applyPreset("playable")}>All Playable</button>
          <button onClick={() => applyPreset("walls")}>All Walls</button>
          <button onClick={() => applyPreset("outer")}>Outer Walls Only</button>
          <button className="primary" onClick={handleSave} disabled={!draft.name.trim()}>
            Save Grid
          </button>
        </div>

        <p className="hint">
          Click cells to toggle walls. Green = will be playable (enclosed). Amber = not a wall, but open to the
          outside, so it will NOT be generated as a playable cell — exactly like GridCreatorTool's flood fill.
          Playable cells: {playableCount} / {draft.width * draft.length}.
        </p>

        <div className="canvas-wrap">
          <GridCanvas
            width={draft.width}
            length={draft.length}
            cellPx={cellPx}
            onCellClick={({ x, z }) => toggleCell(x, z)}
            renderCell={(ctx, cell, rect) => {
              const idx = gridIndex(draft.width, cell.x, cell.z);
              const isWall = draft.wallToggles[idx];
              const isPlayable = playableMask[idx];
              ctx.fillStyle = isWall ? WALL_COLOR : isPlayable ? PLAYABLE_COLOR : EXTERIOR_COLOR;
              ctx.fillRect(rect.x, rect.y, rect.width, rect.height);
            }}
          />
        </div>
      </main>
    </div>
  );
}

function makeBlankDraft(): SavedGrid {
  const width = 10;
  const length = 10;
  return {
    id: "",
    name: "",
    width,
    length,
    cellSize: 1,
    wallToggles: makeFlatGrid(width, length, false),
    updatedAt: 0,
  };
}
