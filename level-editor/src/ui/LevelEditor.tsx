import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { getCanvasPalette } from "../canvasPalette";
import { BLOCK_COLOR_HEX } from "../colors";
import { computePlayableMask, gridIndex } from "../grid/floodFill";
import { listGrids, saveGrid } from "../grid/gridLibrary";
import { downloadLevelJson, fromLevelJson, parseLevelJson, toLevelJson } from "../io/levelJson";
import { listPresets } from "../level/holePresetLibrary";
import { holeCellIndex, makeHole, stampPreset } from "../level/holeShape";
import { deleteLevel, listLevels, saveLevel } from "../level/levelLibrary";
import { validateLevel } from "../level/validate";
import { newId } from "../storage";
import { useTheme } from "../theme";
import { BLOCK_COLORS, HOLE_TYPES, cellKey } from "../types";
import type {
  BlockColor,
  GridCell,
  HolePlacement,
  HoleShapePreset,
  HoleType,
  RotationQuarterTurns,
  SavedGrid,
  SavedLevel,
} from "../types";
import { GridCanvas } from "./GridCanvas";
import { PieceLegend } from "./Legend";
import { ZoomControl, zoomedCellPx } from "./ZoomControl";
import { drawCat, drawHoleGroup } from "./drawShapes";

type Mode = "cats" | "holes-shape" | "holes-manual" | "holes-preset";

interface LevelEditorProps {
  gridsVersion: number;
  presetsVersion: number;
  onLevelsChanged?: () => void;
  onGridsChanged?: () => void;
}

export function LevelEditor({ gridsVersion, presetsVersion, onLevelsChanged, onGridsChanged }: LevelEditorProps) {
  const [grids, setGrids] = useState<SavedGrid[]>(() => listGrids());
  const [levels, setLevels] = useState<SavedLevel[]>(() => listLevels());
  const [presets, setPresets] = useState<HoleShapePreset[]>(() => listPresets());
  const [draft, setDraft] = useState<SavedLevel>(() => makeBlankDraft(grids[0]?.id ?? ""));
  const [mode, setMode] = useState<Mode>("cats");
  const [selectedColor, setSelectedColor] = useState<BlockColor>("Red");
  const [selectedHoleType, setSelectedHoleType] = useState<HoleType>("Isolated");
  const [manualRotation, setManualRotation] = useState<RotationQuarterTurns>(0);
  const [capacity, setCapacity] = useState(1);
  const [selectedPresetId, setSelectedPresetId] = useState<string>("");
  const [presetRotation, setPresetRotation] = useState<RotationQuarterTurns>(0);
  /** Only used by shape mode, which needs the whole outline before it can become a hole. */
  const [shapeCells, setShapeCells] = useState<Set<string>>(new Set());
  const [zoom, setZoom] = useState(1);
  const fileInputRef = useRef<HTMLInputElement>(null);
  /** What the in-progress drag is doing, so a stroke never flip-flops a cell. */
  const dragActionRef = useRef<"add" | "remove">("add");
  const palette = getCanvasPalette(useTheme());

  useEffect(() => setGrids(listGrids()), [gridsVersion]);
  useEffect(() => setPresets(listPresets()), [presetsVersion]);

  const selectedPreset = useMemo(() => presets.find((p) => p.id === selectedPresetId), [presets, selectedPresetId]);
  const grid = useMemo(() => grids.find((g) => g.id === draft.gridId), [grids, draft.gridId]);
  const playableMask = useMemo(
    () => (grid ? computePlayableMask(grid.width, grid.length, grid.wallToggles) : []),
    [grid]
  );
  const isPlayable = useCallback(
    (cell: GridCell) => !!grid && !!playableMask[gridIndex(grid.width, cell.x, cell.z)],
    [grid, playableMask]
  );
  const holesByCell = useMemo(() => holeCellIndex(draft.holes), [draft.holes]);
  const catsByCell = useMemo(
    () => new Map(draft.cats.map((c) => [cellKey(c.x, c.z), c])),
    [draft.cats]
  );

  const cellPx = useMemo(() => (grid ? zoomedCellPx(grid.width, grid.length, zoom) : 32), [grid, zoom]);
  const issues = useMemo(() => (grid ? validateLevel(draft, grid) : []), [draft, grid]);
  const isShapeMode = mode === "holes-shape";
  const isHoleMode = mode !== "cats";

  function refreshLevels() {
    setLevels(listLevels());
    onLevelsChanged?.();
  }

  function loadLevel(level: SavedLevel) {
    setDraft({ ...level, cats: [...level.cats], holes: [...level.holes] });
    setShapeCells(new Set());
  }

  function newLevel() {
    if (!grid) return;
    setDraft(makeBlankDraft(grid.id));
    setShapeCells(new Set());
  }

  /** Clears anything already sitting on these cells, so a placement never stacks. */
  function clearCells(level: SavedLevel, keys: Set<string>): SavedLevel {
    return {
      ...level,
      cats: level.cats.filter((c) => !keys.has(cellKey(c.x, c.z))),
      // a hole is one unit: if a placement lands on any of its cells, the whole hole goes
      holes: level.holes.filter((h) => !h.cells.some((c) => keys.has(cellKey(c.x, c.z)))),
    };
  }

  // ── direct manipulation: click places, click again removes ──────

  function placeCat(cell: GridCell) {
    setDraft((prev) => {
      const key = cellKey(cell.x, cell.z);
      if (prev.cats.some((c) => cellKey(c.x, c.z) === key)) return prev;
      // don't let a cat land on top of an existing hole
      if (prev.holes.some((h) => h.cells.some((c) => cellKey(c.x, c.z) === key))) return prev;
      return { ...prev, cats: [...prev.cats, { color: selectedColor, x: cell.x, z: cell.z }] };
    });
  }

  function removeCat(cell: GridCell) {
    const key = cellKey(cell.x, cell.z);
    setDraft((prev) => ({ ...prev, cats: prev.cats.filter((c) => cellKey(c.x, c.z) !== key) }));
  }

  function placeSingleHole(cell: GridCell) {
    setDraft((prev) => {
      const keys = new Set([cellKey(cell.x, cell.z)]);
      const cleared = clearCells(prev, keys);
      const hole: HolePlacement = {
        id: newId(),
        color: selectedColor,
        capacity,
        cells: [{ x: cell.x, z: cell.z, holeType: selectedHoleType, rotationQuarterTurns: manualRotation }],
      };
      return { ...cleared, holes: [...cleared.holes, hole] };
    });
  }

  function removeHoleAt(cell: GridCell) {
    const key = cellKey(cell.x, cell.z);
    setDraft((prev) => ({
      ...prev,
      holes: prev.holes.filter((h) => !h.cells.some((c) => cellKey(c.x, c.z) === key)),
    }));
  }

  function stampPresetAt(cell: GridCell) {
    if (!selectedPreset) return;
    const stamped = stampPreset(selectedPreset, cell, presetRotation);
    setDraft((prev) => {
      const keys = new Set(stamped.map((c) => cellKey(c.x, c.z)));
      const cleared = clearCells(prev, keys);
      const hole: HolePlacement = {
        id: newId(),
        color: selectedColor,
        capacity,
        cells: stamped.map((c) => ({
          x: c.x,
          z: c.z,
          holeType: c.holeType,
          rotationQuarterTurns: c.rotationQuarterTurns,
        })),
      };
      return { ...cleared, holes: [...cleared.holes, hole] };
    });
  }

  function handlePaintStart(cell: GridCell) {
    const key = cellKey(cell.x, cell.z);
    if (mode === "cats") {
      dragActionRef.current = catsByCell.has(key) ? "remove" : "add";
      if (dragActionRef.current === "remove") removeCat(cell);
      else placeCat(cell);
      return;
    }
    if (mode === "holes-manual") {
      if (holesByCell.has(key)) removeHoleAt(cell);
      else placeSingleHole(cell);
      return;
    }
    if (mode === "holes-preset") {
      if (holesByCell.has(key)) removeHoleAt(cell);
      else stampPresetAt(cell);
      return;
    }
    // shape mode: accumulate an outline first
    dragActionRef.current = shapeCells.has(key) ? "remove" : "add";
    applyShapeSelection(cell);
  }

  function handlePaintDrag(cell: GridCell) {
    if (mode === "cats") {
      if (dragActionRef.current === "add") placeCat(cell);
      else removeCat(cell);
      return;
    }
    if (isShapeMode) applyShapeSelection(cell);
    // manual/preset holes are single deliberate placements — no drag repeat
  }

  function applyShapeSelection(cell: GridCell) {
    setShapeCells((prev) => {
      const key = cellKey(cell.x, cell.z);
      const shouldHave = dragActionRef.current === "add";
      if (prev.has(key) === shouldHave) return prev;
      const next = new Set(prev);
      if (shouldHave) next.add(key);
      else next.delete(key);
      return next;
    });
  }

  function commitShape() {
    const cells: GridCell[] = [...shapeCells].map((key) => {
      const [x, z] = key.split(",").map(Number);
      return { x, z };
    });
    if (cells.length === 0) return;
    setDraft((prev) => {
      const cleared = clearCells(prev, shapeCells);
      return { ...cleared, holes: [...cleared.holes, makeHole(newId(), selectedColor, capacity, cells)] };
    });
    setShapeCells(new Set());
  }

  function handleSaveLevel() {
    if (!draft.name.trim() || !grid) return;
    const saved: SavedLevel = { ...draft, name: draft.name.trim(), id: draft.id || newId() };
    saveLevel(saved);
    setDraft({ ...saved });
    refreshLevels();
  }

  function handleDeleteLevel(id: string) {
    deleteLevel(id);
    if (draft.id === id) newLevel();
    refreshLevels();
  }

  function handleExport() {
    if (!grid) return;
    downloadLevelJson(toLevelJson(draft, grid));
  }

  async function handleImportFile(file: File) {
    const text = await file.text();
    const json = parseLevelJson(text);
    const { grid: gridDraft, level: levelDraft } = fromLevelJson(json);
    const importedGrid: SavedGrid = { ...gridDraft, id: newId(), updatedAt: Date.now() };
    saveGrid(importedGrid);
    const importedLevel: SavedLevel = { ...levelDraft, id: newId(), gridId: importedGrid.id, updatedAt: Date.now() };
    saveLevel(importedLevel);
    setGrids(listGrids());
    onGridsChanged?.();
    refreshLevels();
    loadLevel(importedLevel);
  }

  const totalCapacity = draft.holes.reduce((sum, h) => sum + h.capacity, 0);

  return (
    <div className="screen">
      <aside className="sidebar">
        <div className="sidebar-header">
          <h2>Levels</h2>
        </div>
        <ul className="library-list">
          {levels.map((level) => (
            <li key={level.id} className={level.id === draft.id ? "active" : ""}>
              <button className="library-item" onClick={() => loadLevel(level)}>
                <span>{level.name}</span>
                <span className="muted">
                  {level.cats.length}c · {level.holes.length}h
                </span>
              </button>
              <button className="danger-link" onClick={() => handleDeleteLevel(level.id)} title="Delete level">
                ×
              </button>
            </li>
          ))}
          {levels.length === 0 && <li className="muted empty">No saved levels yet.</li>}
        </ul>
        <div className="sidebar-footer">
          <input
            ref={fileInputRef}
            type="file"
            accept="application/json"
            style={{ display: "none" }}
            onChange={(e) => {
              const file = e.target.files?.[0];
              if (file) void handleImportFile(file);
              e.target.value = "";
            }}
          />
          <button onClick={() => fileInputRef.current?.click()}>Import JSON</button>
        </div>
      </aside>

      <main className="editor-main">
        <div className="field-row">
          <label>
            Name
            <input value={draft.name} onChange={(e) => setDraft((prev) => ({ ...prev, name: e.target.value }))} placeholder="New Level" />
          </label>
          <label>
            Grid
            <select value={draft.gridId} onChange={(e) => setDraft((prev) => ({ ...prev, gridId: e.target.value }))}>
              <option value="" disabled>
                Select a grid…
              </option>
              {grids.map((g) => (
                <option key={g.id} value={g.id}>
                  {g.name} ({g.width}×{g.length})
                </option>
              ))}
            </select>
          </label>
          <button onClick={newLevel} disabled={!grid}>
            New Level
          </button>
          <button className="primary" onClick={handleSaveLevel} disabled={!draft.name.trim() || !grid}>
            Save Level
          </button>
          <button onClick={handleExport} disabled={!grid}>
            Export JSON
          </button>
        </div>

        {!grid && <p className="hint">Create a grid in the Grid Designer tab first, then pick it here.</p>}

        {grid && (
          <>
            <div className="field-row">
              <div className="mode-toggle">
                <button className={mode === "cats" ? "active" : ""} onClick={() => setMode("cats")}>
                  Cats
                </button>
                <button className={mode === "holes-shape" ? "active" : ""} onClick={() => setMode("holes-shape")}>
                  Holes (shape)
                </button>
                <button className={mode === "holes-manual" ? "active" : ""} onClick={() => setMode("holes-manual")}>
                  Holes (single)
                </button>
                <button className={mode === "holes-preset" ? "active" : ""} onClick={() => setMode("holes-preset")}>
                  Holes (preset)
                </button>
              </div>
              <ZoomControl value={zoom} onChange={setZoom} />
            </div>

            <div className="field-row">
              <label className="color-field">
                Color — cats &amp; holes
                <div className="color-picker">
                  {BLOCK_COLORS.map((c) => (
                    <button
                      key={c}
                      type="button"
                      title={c}
                      aria-label={c}
                      aria-pressed={selectedColor === c}
                      className={selectedColor === c ? "color-chip active" : "color-chip"}
                      style={{ background: BLOCK_COLOR_HEX[c] }}
                      onClick={() => setSelectedColor(c)}
                    />
                  ))}
                </div>
              </label>
              {isHoleMode && (
                <label title="How many cats this hole can swallow before it's full">
                  Cat Capacity
                  <input
                    type="number"
                    min={1}
                    max={99}
                    value={capacity}
                    onChange={(e) => setCapacity(Math.max(1, Math.floor(Number(e.target.value)) || 1))}
                  />
                </label>
              )}
              {mode === "holes-manual" && (
                <>
                  <label>
                    Hole Type
                    <select value={selectedHoleType} onChange={(e) => setSelectedHoleType(e.target.value as HoleType)}>
                      {HOLE_TYPES.map((t) => (
                        <option key={t} value={t}>
                          {t}
                        </option>
                      ))}
                    </select>
                  </label>
                  <label>
                    Rotation
                    <select value={manualRotation} onChange={(e) => setManualRotation(Number(e.target.value) as RotationQuarterTurns)}>
                      {[0, 1, 2, 3].map((r) => (
                        <option key={r} value={r}>
                          {r * 90}°
                        </option>
                      ))}
                    </select>
                  </label>
                </>
              )}
              {mode === "holes-preset" && (
                <>
                  <label>
                    Preset
                    <select value={selectedPresetId} onChange={(e) => setSelectedPresetId(e.target.value)}>
                      <option value="" disabled>
                        Select a preset…
                      </option>
                      {presets.map((p) => (
                        <option key={p.id} value={p.id}>
                          {p.name} ({p.cells.length} cells)
                        </option>
                      ))}
                    </select>
                  </label>
                  <label>
                    Rotation
                    <select value={presetRotation} onChange={(e) => setPresetRotation(Number(e.target.value) as RotationQuarterTurns)}>
                      {[0, 1, 2, 3].map((r) => (
                        <option key={r} value={r}>
                          {r * 90}°
                        </option>
                      ))}
                    </select>
                  </label>
                </>
              )}
            </div>

            {isShapeMode && (
              <div className="field-row">
                <button onClick={() => setShapeCells(new Set())} disabled={shapeCells.size === 0}>
                  Clear Outline ({shapeCells.size})
                </button>
                <button className="primary" onClick={commitShape} disabled={shapeCells.size === 0}>
                  Create Hole
                </button>
              </div>
            )}

            <p className="hint">
              {mode === "cats"
                ? "Click a cell to drop a cat in the selected color; click it again to take it away. Drag to place or clear a run of them."
                : mode === "holes-manual"
                  ? "Click a cell to cut a one-cell hole with the capacity above; click it again to remove it."
                  : mode === "holes-preset"
                    ? "Click a cell to stamp the selected preset with that cell as its anchor; click any cell of a placed hole to remove the whole hole."
                    : "Drag out the outline of a tunnel, then Create Hole — the whole connected shape becomes ONE hole with a single shared capacity. Click any cell of a placed hole to replace it."}
            </p>

            <div className="canvas-row">
              <div className="canvas-wrap">
                <GridCanvas
                  width={grid.width}
                  length={grid.length}
                  cellPx={cellPx}
                  isInteractive={isPlayable}
                  onPaintStart={handlePaintStart}
                  onPaintDrag={handlePaintDrag}
                  renderCell={(ctx, cell, rect) => {
                    const playable = isPlayable(cell);
                    ctx.fillStyle = playable ? palette.floor : palette.inert;
                    ctx.fillRect(rect.x, rect.y, rect.width, rect.height);
                    if (playable && isShapeMode && shapeCells.has(cellKey(cell.x, cell.z))) {
                      ctx.fillStyle = palette.selectedTint;
                      ctx.fillRect(rect.x, rect.y, rect.width, rect.height);
                    }
                  }}
                  renderOverlay={(ctx, cellRect) => {
                    // holes first: each is drawn as ONE shape across all its cells
                    for (const hole of draft.holes) {
                      drawHoleGroup(
                        ctx,
                        hole.cells,
                        cellRect,
                        BLOCK_COLOR_HEX[hole.color],
                        palette.holeVoid,
                        hole.capacity
                      );
                    }
                    for (const cat of draft.cats) {
                      drawCat(ctx, cellRect(cat.x, cat.z), BLOCK_COLOR_HEX[cat.color], palette.holeVoid);
                    }
                  }}
                />
              </div>

              <div className="canvas-side">
                <PieceLegend accentHex={BLOCK_COLOR_HEX[selectedColor]} />
                <p className="stat-line">
                  <span className="stat-value">{draft.cats.length}</span>
                  <span className="muted"> cats · </span>
                  <span className="stat-value">{draft.holes.length}</span>
                  <span className="muted"> holes · </span>
                  <span className="stat-value">{totalCapacity}</span>
                  <span className="muted"> capacity</span>
                </p>
                {issues.length > 0 && (
                  <ul className="issues">
                    {issues.map((issue, i) => (
                      <li key={i} className={issue.severity}>
                        {issue.message}
                      </li>
                    ))}
                  </ul>
                )}
              </div>
            </div>
          </>
        )}
      </main>
    </div>
  );
}

function makeBlankDraft(gridId: string): SavedLevel {
  return { id: "", name: "", gridId, cats: [], holes: [], updatedAt: 0 };
}
