import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { getCanvasPalette } from "../canvasPalette";
import { BLOCK_COLOR_HEX } from "../colors";
import { computePlayableMask, gridIndex } from "../grid/floodFill";
import { listGrids, saveGrid } from "../grid/gridLibrary";
import { downloadLevelJson, fromLevelJson, parseLevelJson, toLevelJson } from "../io/levelJson";
import { boundarySides, nearestBoundarySide } from "../level/gates";
import { listPresets } from "../level/holePresetLibrary";
import { connectedComponents, holeCellIndex, makeHole, stampPreset } from "../level/holeShape";
import { deleteLevel, listLevels, saveLevel } from "../level/levelLibrary";
import { validateLevel } from "../level/validate";
import { newId } from "../storage";
import { useTheme } from "../theme";
import { BLOCK_COLORS, HOLE_TYPES, cellKey } from "../types";
import type {
  BlockColor,
  CatPlacement,
  GatePlacement,
  GridCell,
  HolePlacement,
  HoleShapePreset,
  HoleType,
  RotationQuarterTurns,
  SavedGrid,
  SavedLevel,
} from "../types";
import { GateInspector } from "./GateInspector";
import { GridCanvas } from "./GridCanvas";
import type { CellLocal } from "./GridCanvas";
import { HoleInspector } from "./HoleInspector";
import { PieceLegend } from "./Legend";
import { ZoomControl, zoomedCellPx } from "./ZoomControl";
import { drawCatStack, drawGate, drawHoleGroup } from "./drawShapes";

type Mode = "cats" | "holes-shape" | "holes-manual" | "holes-preset" | "gates" | "edit";

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
  /** Edit mode's current subject. */
  const [selectedHoleId, setSelectedHoleId] = useState<string | null>(null);
  /** Gates mode's current subject. */
  const [selectedGateId, setSelectedGateId] = useState<string | null>(null);
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
  /** Cats sharing a cell form a stack; the LAST of a cell's entries is its top. */
  const catsByCell = useMemo(() => {
    const stacks = new Map<string, CatPlacement[]>();
    for (const cat of draft.cats) {
      const key = cellKey(cat.x, cat.z);
      const stack = stacks.get(key);
      if (stack) stack.push(cat);
      else stacks.set(key, [cat]);
    }
    return stacks;
  }, [draft.cats]);
  const gatesByCell = useMemo(() => {
    const byCell = new Map<string, GatePlacement[]>();
    for (const gate of draft.gates) {
      const key = cellKey(gate.x, gate.z);
      const list = byCell.get(key);
      if (list) list.push(gate);
      else byCell.set(key, [gate]);
    }
    return byCell;
  }, [draft.gates]);

  const cellPx = useMemo(() => (grid ? zoomedCellPx(grid.width, grid.length, zoom) : 32), [grid, zoom]);
  const issues = useMemo(() => (grid ? validateLevel(draft, grid) : []), [draft, grid]);
  const isShapeMode = mode === "holes-shape";
  const isEditMode = mode === "edit";
  const isGateMode = mode === "gates";
  const placesHoles = mode === "holes-shape" || mode === "holes-manual" || mode === "holes-preset";
  const selectedHole = useMemo(
    () => draft.holes.find((h) => h.id === selectedHoleId) ?? null,
    [draft.holes, selectedHoleId]
  );
  const selectedGate = useMemo(
    () => draft.gates.find((g) => g.id === selectedGateId) ?? null,
    [draft.gates, selectedGateId]
  );

  function refreshLevels() {
    setLevels(listLevels());
    onLevelsChanged?.();
  }

  function loadLevel(level: SavedLevel) {
    // Levels saved before gates existed have no `gates` field at all.
    setDraft({ ...level, cats: [...level.cats], holes: [...level.holes], gates: [...(level.gates ?? [])] });
    setShapeCells(new Set());
    setSelectedGateId(null);
    setSelectedHoleId(null);
  }

  function newLevel() {
    if (!grid) return;
    setDraft(makeBlankDraft(grid.id));
    setShapeCells(new Set());
    setSelectedGateId(null);
    setSelectedHoleId(null);
  }

  /**
   * Clears anything already sitting on these cells, so a hole never lands on
   * occupied floor. Gates are left alone: a gate lives on its cell's *edge*,
   * and parking a hole on that cell is exactly how a gate is meant to be used.
   */
  function clearCells(level: SavedLevel, keys: Set<string>): SavedLevel {
    return {
      ...level,
      cats: level.cats.filter((c) => !keys.has(cellKey(c.x, c.z))),
      // a hole is one unit: if a placement lands on any of its cells, the whole hole goes
      holes: level.holes.filter((h) => !h.cells.some((c) => keys.has(cellKey(c.x, c.z)))),
    };
  }

  // ── direct manipulation: click places, click again removes ──────

  /** Drops a cat on TOP of whatever is already on the cell — appending is what makes it the top. */
  function placeCat(cell: GridCell) {
    setDraft((prev) => {
      const key = cellKey(cell.x, cell.z);
      // don't let a cat land on top of an existing hole
      if (prev.holes.some((h) => h.cells.some((c) => cellKey(c.x, c.z) === key))) return prev;
      return { ...prev, cats: [...prev.cats, { color: selectedColor, x: cell.x, z: cell.z }] };
    });
  }

  /** Takes the TOP cat off the cell — the last entry for it — leaving the rest of the stack. */
  function removeCat(cell: GridCell) {
    const key = cellKey(cell.x, cell.z);
    setDraft((prev) => {
      const last = prev.cats.map((c) => cellKey(c.x, c.z)).lastIndexOf(key);
      if (last < 0) return prev;
      return { ...prev, cats: prev.cats.filter((_, i) => i !== last) };
    });
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

  // ── edit mode: retune a placed hole's color, capacity or shape ──

  function updateSelectedHole(patch: Partial<Pick<HolePlacement, "color" | "capacity">>) {
    setDraft((prev) => ({
      ...prev,
      holes: prev.holes.map((h) => (h.id === selectedHoleId ? { ...h, ...patch } : h)),
    }));
  }

  function deleteSelectedHole() {
    setDraft((prev) => ({ ...prev, holes: prev.holes.filter((h) => h.id !== selectedHoleId) }));
    setSelectedHoleId(null);
  }

  function growSelectedHole(hole: HolePlacement, cell: GridCell) {
    const touchesHole = hole.cells.some((c) => Math.abs(c.x - cell.x) + Math.abs(c.z - cell.z) === 1);
    if (!touchesHole) return; // a hole must stay one connected shape
    setDraft((prev) => {
      const cleared = clearCells(prev, new Set([cellKey(cell.x, cell.z)]));
      const target = cleared.holes.find((h) => h.id === hole.id);
      if (!target) return prev;
      const grown = makeHole(hole.id, hole.color, hole.capacity, [...target.cells, cell]);
      return { ...cleared, holes: cleared.holes.map((h) => (h.id === hole.id ? grown : h)) };
    });
  }

  function carveSelectedHole(hole: HolePlacement, cell: GridCell) {
    const key = cellKey(cell.x, cell.z);
    const remaining = hole.cells.filter((c) => cellKey(c.x, c.z) !== key);
    setDraft((prev) => {
      const others = prev.holes.filter((h) => h.id !== hole.id);
      if (remaining.length === 0) return { ...prev, holes: others };
      // carving out a middle cell can split one hole in two; keep both pieces,
      // each inheriting the original's color and capacity
      const pieces = connectedComponents(remaining).map((piece, i) =>
        makeHole(i === 0 ? hole.id : newId(), hole.color, hole.capacity, piece)
      );
      return { ...prev, holes: [...others, ...pieces] };
    });
    if (remaining.length === 0) setSelectedHoleId(null);
  }

  // ── gates: one per boundary edge, holding an ordered queue of cats ──

  function updateSelectedGate(cats: BlockColor[]) {
    setDraft((prev) => ({
      ...prev,
      gates: prev.gates.map((g) => (g.id === selectedGateId ? { ...g, cats } : g)),
    }));
  }

  function deleteSelectedGate() {
    setDraft((prev) => ({ ...prev, gates: prev.gates.filter((g) => g.id !== selectedGateId) }));
    setSelectedGateId(null);
  }

  /**
   * Click a boundary cell to put a gate on the edge nearest the click; click
   * that gate again to push the toolbar colour onto its queue. Interior cells
   * have no wall to replace, so they do nothing.
   */
  function handleGateClick(cell: GridCell, local: CellLocal) {
    const onCell = gatesByCell.get(cellKey(cell.x, cell.z)) ?? [];
    const sides = boundarySides(cell, isPlayable);

    if (onCell.length > 0) {
      // Prefer whichever of this cell's gates is on the edge nearest the click.
      const nearest = nearestBoundarySide(onCell.map((g) => g.side), local.u, local.v);
      const hit = onCell.find((g) => g.side === nearest) ?? onCell[0];
      if (hit.id === selectedGateId) {
        setDraft((prev) => ({
          ...prev,
          gates: prev.gates.map((g) => (g.id === hit.id ? { ...g, cats: [...g.cats, selectedColor] } : g)),
        }));
      } else {
        setSelectedGateId(hit.id);
      }
      return;
    }

    const side = nearestBoundarySide(sides, local.u, local.v);
    if (!side) return; // interior cell: no wall here to replace
    const gate: GatePlacement = { id: newId(), x: cell.x, z: cell.z, side, cats: [] };
    setDraft((prev) => ({ ...prev, gates: [...prev.gates, gate] }));
    setSelectedGateId(gate.id);
  }

  function handleEditClick(cell: GridCell) {
    const key = cellKey(cell.x, cell.z);
    const hitHole = holesByCell.get(key);
    if (hitHole && hitHole.id !== selectedHoleId) {
      setSelectedHoleId(hitHole.id);
      return;
    }
    if (!selectedHole) return;
    if (hitHole && hitHole.id === selectedHoleId) carveSelectedHole(selectedHole, cell);
    else if (!hitHole) growSelectedHole(selectedHole, cell);
  }

  function handlePaintStart(cell: GridCell, local: CellLocal) {
    const key = cellKey(cell.x, cell.z);
    if (isEditMode) {
      handleEditClick(cell);
      return;
    }
    if (isGateMode) {
      handleGateClick(cell, local);
      return;
    }
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
    if (isEditMode || isGateMode) return; // edits and gate placement are deliberate single clicks
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
  const gatedCats = draft.gates.reduce((sum, g) => sum + g.cats.length, 0);

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
                  {(level.gates?.length ?? 0) > 0 && ` · ${level.gates.length}g`}
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
                <button className={mode === "gates" ? "active" : ""} onClick={() => setMode("gates")}>
                  Gates
                </button>
                <button className={mode === "edit" ? "active" : ""} onClick={() => setMode("edit")}>
                  Edit
                </button>
              </div>
              <ZoomControl value={zoom} onChange={setZoom} />
            </div>

            {/* placement controls — irrelevant while editing an existing hole,
                whose own color/capacity live in the inspector instead */}
            {!isEditMode && (
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
              {placesHoles && (
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
            )}

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
                ? "Click a cell to drop a cat in the selected color; cats stack, so clicking again adds another on top and the topmost is the only one a hole can take. Click a stacked cell to lift its top cat off. Drag to place or clear a run of them."
                : mode === "holes-manual"
                  ? "Click a cell to cut a one-cell hole with the capacity above; click it again to remove it."
                  : mode === "holes-preset"
                    ? "Click a cell to stamp the selected preset with that cell as its anchor; click any cell of a placed hole to remove the whole hole."
                    : mode === "gates"
                      ? "Click a cell on the board's edge to put a gate on the wall nearest your click. Click that gate again to push the selected color onto its queue, and reorder or trim the queue on the right."
                      : mode === "edit"
                        ? "Click any hole to select it, then change its color or capacity on the right. Click a cell touching it to grow the shape, or one of its own cells to carve that cell away."
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
                        hole.capacity,
                        isEditMode && hole.id === selectedHoleId ? palette.textAccent : undefined
                      );
                    }
                    // gates sit on cell edges, under the cats so a cat on the mouth cell still reads
                    for (const gate of draft.gates) {
                      drawGate(
                        ctx,
                        cellRect(gate.x, gate.z),
                        gate.side,
                        gate.cats.map((c) => BLOCK_COLOR_HEX[c]),
                        palette.textMuted,
                        palette.holeVoid,
                        isGateMode && gate.id === selectedGateId ? palette.textAccent : undefined
                      );
                    }
                    for (const [key, stack] of catsByCell) {
                      const [x, z] = key.split(",").map(Number);
                      drawCatStack(
                        ctx,
                        cellRect(x, z),
                        stack.map((c) => BLOCK_COLOR_HEX[c.color]),
                        palette.holeVoid,
                        palette.floor
                      );
                    }
                  }}
                />
              </div>

              <div className="canvas-side">
                {isEditMode &&
                  (selectedHole ? (
                    <HoleInspector
                      hole={selectedHole}
                      onColorChange={(color) => updateSelectedHole({ color })}
                      onCapacityChange={(value) => updateSelectedHole({ capacity: value })}
                      onDelete={deleteSelectedHole}
                      onDeselect={() => setSelectedHoleId(null)}
                    />
                  ) : (
                    <p className="hint">Click a hole on the grid to select and edit it.</p>
                  ))}
                {isGateMode &&
                  (selectedGate ? (
                    <GateInspector
                      gate={selectedGate}
                      selectedColor={selectedColor}
                      onQueueChange={updateSelectedGate}
                      onDelete={deleteSelectedGate}
                      onDeselect={() => setSelectedGateId(null)}
                    />
                  ) : (
                    <p className="hint">Click a cell on the board's edge to add a gate, or an existing gate to edit it.</p>
                  ))}
                <PieceLegend accentHex={BLOCK_COLOR_HEX[selectedColor]} />
                <p className="stat-line">
                  <span className="stat-value">{draft.cats.length}</span>
                  <span className="muted"> on board · </span>
                  <span className="stat-value">{gatedCats}</span>
                  <span className="muted"> in gates · </span>
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
  return { id: "", name: "", gridId, cats: [], holes: [], gates: [], updatedAt: 0 };
}
