import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { getCanvasPalette } from "../canvasPalette";
import { BLOCK_COLOR_HEX } from "../colors";
import { computePlayableMask, gridIndex } from "../grid/floodFill";
import { listGrids, saveGrid } from "../grid/gridLibrary";
import { downloadLevelJson, fromLevelJson, parseLevelJson, toLevelJson } from "../io/levelJson";
import { classifyHoleShape, openingsForHole, stampPreset } from "../level/holeShape";
import { listPresets } from "../level/holePresetLibrary";
import { deleteLevel, listLevels, saveLevel } from "../level/levelLibrary";
import { validateLevel } from "../level/validate";
import { newId } from "../storage";
import { useTheme } from "../theme";
import { BLOCK_COLORS, HOLE_TYPES, cellKey } from "../types";
import type { BlockColor, GridCell, HoleShapePreset, HoleType, RotationQuarterTurns, SavedGrid, SavedLevel } from "../types";
import { GridCanvas } from "./GridCanvas";
import { drawCat, drawHole } from "./drawShapes";

const MAX_CANVAS_DIMENSION_PX = 640;

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
  const [selectedPresetId, setSelectedPresetId] = useState<string>("");
  const [presetRotation, setPresetRotation] = useState<RotationQuarterTurns>(0);
  const [selectedCells, setSelectedCells] = useState<Set<string>>(new Set());
  const fileInputRef = useRef<HTMLInputElement>(null);
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

  const cellPx = useMemo(() => {
    if (!grid) return 32;
    const largestDimension = Math.max(grid.width, grid.length);
    return Math.max(12, Math.min(32, Math.floor(MAX_CANVAS_DIMENSION_PX / largestDimension)));
  }, [grid]);

  const issues = useMemo(() => (grid ? validateLevel(draft, grid) : []), [draft, grid]);

  function refreshLevels() {
    setLevels(listLevels());
    onLevelsChanged?.();
  }

  function loadLevel(level: SavedLevel) {
    setDraft({ ...level, cats: [...level.cats], holes: [...level.holes] });
    setSelectedCells(new Set());
  }

  function newLevel() {
    if (!grid) return;
    setDraft(makeBlankDraft(grid.id));
    setSelectedCells(new Set());
  }

  function toggleSelected(cell: GridCell) {
    if (mode === "holes-preset") {
      // Presets stamp from a single anchor cell, so a click replaces the selection.
      setSelectedCells(new Set([cellKey(cell.x, cell.z)]));
      return;
    }
    setSelectedCells((prev) => {
      const key = cellKey(cell.x, cell.z);
      const next = new Set(prev);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  }

  function selectedCellList(): GridCell[] {
    return [...selectedCells].map((key) => {
      const [x, z] = key.split(",").map(Number);
      return { x, z };
    });
  }

  function handlePlace() {
    const cells = selectedCellList();
    if (cells.length === 0) return;

    if (mode === "holes-preset") {
      if (!selectedPreset) return;
      const stamped = stampPreset(selectedPreset, cells[0], presetRotation);
      const cellKeys = new Set(stamped.map((c) => cellKey(c.x, c.z)));
      setDraft((prev) => ({
        ...prev,
        cats: prev.cats.filter((c) => !cellKeys.has(cellKey(c.x, c.z))),
        holes: [
          ...prev.holes.filter((h) => !cellKeys.has(cellKey(h.x, h.z))),
          ...stamped.map((c) => ({ color: selectedColor, x: c.x, z: c.z, holeType: c.holeType, rotationQuarterTurns: c.rotationQuarterTurns })),
        ],
      }));
      setSelectedCells(new Set());
      return;
    }

    setDraft((prev) => {
      const cellKeys = new Set(cells.map((c) => cellKey(c.x, c.z)));
      let cats = prev.cats.filter((c) => !cellKeys.has(cellKey(c.x, c.z)));
      let holes = prev.holes.filter((h) => !cellKeys.has(cellKey(h.x, h.z)));

      if (mode === "cats") {
        cats = [...cats, ...cells.map((c) => ({ color: selectedColor, x: c.x, z: c.z }))];
      } else if (mode === "holes-shape") {
        const classified = classifyHoleShape(cells);
        holes = [
          ...holes,
          ...cells.map((c) => {
            const result = classified.get(cellKey(c.x, c.z))!;
            return { color: selectedColor, x: c.x, z: c.z, holeType: result.holeType, rotationQuarterTurns: result.rotationQuarterTurns };
          }),
        ];
      } else {
        holes = [
          ...holes,
          ...cells.map((c) => ({ color: selectedColor, x: c.x, z: c.z, holeType: selectedHoleType, rotationQuarterTurns: manualRotation })),
        ];
      }
      return { ...prev, cats, holes };
    });
    setSelectedCells(new Set());
  }

  function handleRemoveSelected() {
    const cellKeys = new Set(selectedCellList().map((c) => cellKey(c.x, c.z)));
    setDraft((prev) => ({
      ...prev,
      cats: prev.cats.filter((c) => !cellKeys.has(cellKey(c.x, c.z))),
      holes: prev.holes.filter((h) => !cellKeys.has(cellKey(h.x, h.z))),
    }));
    setSelectedCells(new Set());
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

  function handleImportClick() {
    fileInputRef.current?.click();
  }

  async function handleImportFile(file: File) {
    const text = await file.text();
    const json = parseLevelJson(text);
    const { grid: gridDraft, level: levelDraft } = fromLevelJson(json);
    const newGrid: SavedGrid = { ...gridDraft, id: newId(), updatedAt: Date.now() };
    saveGrid(newGrid);
    const newLevelEntry: SavedLevel = { ...levelDraft, id: newId(), gridId: newGrid.id, updatedAt: Date.now() };
    saveLevel(newLevelEntry);
    setGrids(listGrids());
    onGridsChanged?.();
    refreshLevels();
    loadLevel(newLevelEntry);
  }

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
          <button onClick={handleImportClick}>Import JSON</button>
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
            <select
              value={draft.gridId}
              onChange={(e) => setDraft((prev) => ({ ...prev, gridId: e.target.value }))}
            >
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
                  Holes (manual)
                </button>
                <button className={mode === "holes-preset" ? "active" : ""} onClick={() => setMode("holes-preset")}>
                  Holes (preset)
                </button>
              </div>
              <label>
                Color
                <select value={selectedColor} onChange={(e) => setSelectedColor(e.target.value as BlockColor)}>
                  {BLOCK_COLORS.map((c) => (
                    <option key={c} value={c}>
                      {c}
                    </option>
                  ))}
                </select>
              </label>
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
                  {presets.length === 0 && <p className="hint">No presets saved yet — draw one in the Hole Presets tab.</p>}
                </>
              )}
            </div>

            <div className="field-row">
              <button onClick={() => setSelectedCells(new Set())} disabled={selectedCells.size === 0}>
                Clear Selection ({selectedCells.size})
              </button>
              <button
                className="primary"
                onClick={handlePlace}
                disabled={selectedCells.size === 0 || (mode === "holes-preset" && !selectedPreset)}
              >
                {mode === "cats"
                  ? "Place Cats"
                  : mode === "holes-shape"
                    ? "Create Hole Shape"
                    : mode === "holes-preset"
                      ? "Stamp Preset"
                      : "Place Hole"}
              </button>
              <button onClick={handleRemoveSelected} disabled={selectedCells.size === 0}>
                Remove Selected
              </button>
            </div>

            <p className="hint">
              {mode === "holes-preset"
                ? "Click a single anchor cell, then Stamp Preset — the saved shape is placed relative to that cell, rotated by the amount above."
                : "Click playable cells to select them, then Place. In shape mode, select a connected run of cells and each cell's hole type/rotation is solved automatically from its neighbors, exactly like CatLevelEditorWindow."}
            </p>

            <div className="canvas-wrap">
              <GridCanvas
                width={grid.width}
                length={grid.length}
                cellPx={cellPx}
                isInteractive={isPlayable}
                onCellClick={(cell) => {
                  if (isPlayable(cell)) toggleSelected(cell);
                }}
                renderCell={(ctx, cell, rect) => {
                  const playable = isPlayable(cell);
                  ctx.fillStyle = playable ? palette.floor : palette.inert;
                  ctx.fillRect(rect.x, rect.y, rect.width, rect.height);
                  if (!playable) return;

                  if (selectedCells.has(cellKey(cell.x, cell.z))) {
                    ctx.fillStyle = palette.selectedTint;
                    ctx.fillRect(rect.x, rect.y, rect.width, rect.height);
                  }

                  const hole = draft.holes.find((h) => h.x === cell.x && h.z === cell.z);
                  if (hole) {
                    const openings = openingsForHole(hole.holeType, hole.rotationQuarterTurns);
                    drawHole(ctx, rect, BLOCK_COLOR_HEX[hole.color], openings);
                  }
                  const cat = draft.cats.find((c) => c.x === cell.x && c.z === cell.z);
                  if (cat) {
                    drawCat(ctx, rect, BLOCK_COLOR_HEX[cat.color]);
                  }
                }}
              />
            </div>

            {issues.length > 0 && (
              <ul className="issues">
                {issues.map((issue, i) => (
                  <li key={i} className={issue.severity}>
                    {issue.message}
                  </li>
                ))}
              </ul>
            )}
          </>
        )}
      </main>
    </div>
  );
}

function makeBlankDraft(gridId: string): SavedLevel {
  return { id: "", name: "", gridId, cats: [], holes: [], updatedAt: 0 };
}
