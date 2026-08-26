import { useCallback, useMemo, useRef, useState } from "react";
import { getCanvasPalette } from "../canvasPalette";
import { computePlayableMask, gridIndex, makeFlatGrid } from "../grid/floodFill";
import { deleteGrid, listGrids, saveGrid } from "../grid/gridLibrary";
import { downloadGridJson, fromGridJson, parseGridJson, toGridJson } from "../io/gridJson";
import { toFileName } from "../io/folder";
import { useFolder } from "../io/useFolder";
import type { FolderFile } from "../io/useFolder";
import { newId } from "../storage";
import { useTheme } from "../theme";
import type { SavedGrid } from "../types";
import { FolderBar } from "./FolderBar";
import { GridCanvas } from "./GridCanvas";
import { SwatchLegend } from "./Legend";
import { ZoomControl } from "./ZoomControl";
import { fitZoom, stepZoom, zoomedCellPx } from "./zoom";

/** Viewport padding + canvas card padding/border + the canvas' axis-label margin. */
const CANVAS_CHROME_PX = 92;

interface GridDesignerProps {
  onGridsChanged?: () => void;
}

export function GridDesigner({ onGridsChanged }: GridDesignerProps) {
  const [grids, setGrids] = useState<SavedGrid[]>(() => listGrids());
  const [draft, setDraft] = useState<SavedGrid>(() => makeBlankDraft());
  const [zoom, setZoom] = useState(1);
  const palette = getCanvasPalette(useTheme());
  /** Value the current drag gesture is painting, so dragging never flip-flops cells. */
  const paintValueRef = useRef(true);
  const viewportRef = useRef<HTMLDivElement>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const folder = useFolder("grids");

  const fitToViewport = useCallback(() => {
    const el = viewportRef.current;
    if (!el) return;
    setZoom(fitZoom(draft.width, draft.length, el.clientWidth, el.clientHeight, CANVAS_CHROME_PX));
  }, [draft.width, draft.length]);

  const onZoomStep = useCallback((direction: number) => setZoom((z) => stepZoom(z, direction)), []);

  const refreshGrids = useCallback(() => {
    setGrids(listGrids());
    onGridsChanged?.();
  }, [onGridsChanged]);

  const playableMask = useMemo(
    () => computePlayableMask(draft.width, draft.length, draft.wallToggles),
    [draft.width, draft.length, draft.wallToggles]
  );

  const cellPx = useMemo(() => zoomedCellPx(draft.width, draft.length, zoom), [draft.width, draft.length, zoom]);

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

  function paintCell(x: number, z: number, value: boolean) {
    setDraft((prev) => {
      const idx = gridIndex(prev.width, x, z);
      if (prev.wallToggles[idx] === value) return prev;
      const next = [...prev.wallToggles];
      next[idx] = value;
      return { ...prev, wallToggles: next };
    });
  }

  function handleSave() {
    if (!draft.name.trim()) return;
    const saved: SavedGrid = { ...draft, name: draft.name.trim(), id: draft.id || newId() };
    saveGrid(saved);
    setDraft({ ...saved, wallToggles: [...saved.wallToggles] });
    refreshGrids();
    // A grid saved while a folder is attached lands in it as JSON too, so the
    // folder stays the shareable copy of the library rather than a stale one.
    void folder.write(toFileName(saved.name, "grid"), JSON.stringify(toGridJson(saved), null, 2));
  }

  function handleExport() {
    if (!draft.name.trim()) return;
    downloadGridJson(toGridJson(draft));
  }

  /**
   * Imports grid JSON. A file whose name matches an existing grid replaces it
   * in place, keeping its id, so re-reading a folder updates the library
   * instead of piling up copies — and every level pointing at that grid id
   * keeps working.
   */
  function importGridFiles(files: FolderFile[]): string {
    if (files.length === 0) return "No .json files in that folder.";
    const byName = new Map(listGrids().map((grid) => [grid.name, grid]));
    let added = 0;
    let updated = 0;
    const failed: string[] = [];

    for (const file of files) {
      try {
        const parsed = fromGridJson(parseGridJson(file.text));
        const match = byName.get(parsed.name);
        const saved = { ...parsed, id: match?.id ?? newId(), updatedAt: Date.now() };
        saveGrid(saved);
        byName.set(saved.name, saved);
        if (match) updated++;
        else added++;
      } catch {
        failed.push(file.name);
      }
    }

    refreshGrids();
    const parts = [];
    if (added) parts.push(`${added} added`);
    if (updated) parts.push(`${updated} updated`);
    if (failed.length) parts.push(`${failed.length} skipped (${failed.slice(0, 3).join(", ")})`);
    return parts.join(" · ") || "Nothing to import.";
  }

  async function handleImportFiles(list: FileList | null) {
    if (!list) return;
    const files: FolderFile[] = [];
    for (const file of Array.from(list)) files.push({ name: file.name, text: await file.text() });
    importGridFiles(files);
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
        <div className="sidebar-footer">
          <input
            ref={fileInputRef}
            type="file"
            accept="application/json"
            multiple
            style={{ display: "none" }}
            onChange={(e) => {
              void handleImportFiles(e.target.files);
              e.target.value = "";
            }}
          />
          <button onClick={() => fileInputRef.current?.click()}>Import Grid</button>
          <FolderBar folder={folder} noun="grid" onFiles={importGridFiles} />
        </div>
      </aside>

      <main className="editor-main">
        <div className="editor-toolbar">
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
          <button onClick={handleExport} disabled={!draft.name.trim()}>
            Export JSON
          </button>
        </div>

        <p className="hint">
          Click or <strong>drag</strong> across cells to paint walls — the whole stroke follows whatever the first
          cell became, so you can sweep a wall in one motion. A cell is only playable if it's sealed off from the
          grid border, exactly like GridCreatorTool's flood fill.
        </p>
        </div>

        <div className="canvas-row">
          <div className="canvas-stage">
            <div className="canvas-viewport" ref={viewportRef}>
              <div className="canvas-wrap">
                <GridCanvas
                  width={draft.width}
                  length={draft.length}
                  cellPx={cellPx}
                  onPaintStart={({ x, z }) => {
                    paintValueRef.current = !draft.wallToggles[gridIndex(draft.width, x, z)];
                    paintCell(x, z, paintValueRef.current);
                  }}
                  onPaintDrag={({ x, z }) => paintCell(x, z, paintValueRef.current)}
                  onZoomStep={onZoomStep}
                  renderCell={(ctx, cell, rect) => {
                    const idx = gridIndex(draft.width, cell.x, cell.z);
                    const isWall = draft.wallToggles[idx];
                    const isPlayable = playableMask[idx];
                    ctx.fillStyle = isWall ? palette.wall : isPlayable ? palette.playable : palette.exterior;
                    ctx.fillRect(rect.x, rect.y, rect.width, rect.height);
                  }}
                />
              </div>
            </div>
            <div className="zoom-dock">
              <ZoomControl value={zoom} onChange={setZoom} onFit={fitToViewport} />
            </div>
          </div>

          <div className="canvas-side">
            <SwatchLegend
              items={[
                { color: palette.playable, label: "Playable — sealed from the outside" },
                { color: palette.wall, label: "Wall — you painted it" },
                { color: palette.exterior, label: "Open to the outside — dropped" },
              ]}
            />
            <p className="stat-line">
              <span className="stat-value">{playableCount}</span>
              <span className="muted"> / {draft.width * draft.length} playable</span>
            </p>
          </div>
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
