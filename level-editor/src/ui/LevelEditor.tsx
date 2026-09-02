import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { getCanvasPalette } from "../canvasPalette";
import { BLOCK_COLOR_HEX } from "../colors";
import { computePlayableMask, gridIndex } from "../grid/floodFill";
import { listGrids, saveGrid } from "../grid/gridLibrary";
import { toFileName } from "../io/folder";
import { sameShape } from "../io/gridJson";
import { downloadLevelJson, fromLevelJson, parseLevelJson, toLevelJson } from "../io/levelJson";
import { useFolder } from "../io/useFolder";
import type { FolderFile } from "../io/useFolder";
import { boundarySides, nearestBoundarySide } from "../level/gates";
import { listPresets } from "../level/holePresetLibrary";
import { connectedComponents, holeCellIndex, makeHole, stampPreset } from "../level/holeShape";
import { deleteLevel, listLevels, saveLevel } from "../level/levelLibrary";
import { validateLevel } from "../level/validate";
import type { ValidationIssue } from "../level/validate";
import { newId } from "../storage";
import { useTheme } from "../theme";
import { BLOCK_COLORS, cellKey } from "../types";
import type {
  BlockColor,
  CatPlacement,
  GatePlacement,
  GridCell,
  HolePlacement,
  HoleShapePreset,
  RotationQuarterTurns,
  SavedGrid,
  SavedLevel,
} from "../types";
import { FolderBar } from "./FolderBar";
import { GridCanvas } from "./GridCanvas";
import type { CellLocal } from "./GridCanvas";
import { HoleInspector } from "./HoleInspector";
import { StackVisualizer } from "./StackVisualizer";
import { ToolPanel } from "./ToolPanel";
import { ZoomControl } from "./ZoomControl";
import { toolFor } from "./tools";
import type { CatTool, HoleTool, ToolFamily } from "./tools";
import { fitZoom, stepZoom, zoomedCellPx } from "./zoom";
import { drawCatStack, drawGate, drawHoleGroup } from "./drawShapes";

/**
 * Everything between the viewport's inner edge and the grid itself: the
 * viewport's padding, the canvas card's padding and border, and the canvas'
 * own axis-label margin. Subtracted before working out what zoom makes a grid
 * fill the window.
 */
const CANVAS_CHROME_PX = 92;

/**
 * What the side panel is currently about. A stack has no id of its own — it is
 * just "every cat on this cell" — so it is addressed by cell instead.
 */
type Selection =
  | { kind: "hole"; id: string }
  | { kind: "gate"; id: string }
  | { kind: "stack"; x: number; z: number }
  | null;

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

  // Tool is split three ways so that switching Cat -> Hole -> Cat comes back to
  // the cat tool you were actually using rather than resetting to the default.
  const [family, setFamily] = useState<ToolFamily>("cat");
  const [catTool, setCatTool] = useState<CatTool>("cat");
  const [holeTool, setHoleTool] = useState<HoleTool>("hole-shape");
  const tool = toolFor(family, catTool, holeTool);

  const [selectedColor, setSelectedColor] = useState<BlockColor>("Red");
  const [capacity, setCapacity] = useState(1);
  const [selectedPresetId, setSelectedPresetId] = useState<string>("");
  const [presetRotation, setPresetRotation] = useState<RotationQuarterTurns>(0);
  /** Only used by shape mode, which needs the whole outline before it can become a hole. */
  const [shapeCells, setShapeCells] = useState<Set<string>>(new Set());
  const [selection, setSelection] = useState<Selection>(null);
  const [zoom, setZoom] = useState(1);

  const fileInputRef = useRef<HTMLInputElement>(null);
  const viewportRef = useRef<HTMLDivElement>(null);
  const folder = useFolder("levels");
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

  const selectedHole = useMemo(
    () => (selection?.kind === "hole" ? (draft.holes.find((h) => h.id === selection.id) ?? null) : null),
    [draft.holes, selection]
  );
  const selectedGate = useMemo(
    () => (selection?.kind === "gate" ? (draft.gates.find((g) => g.id === selection.id) ?? null) : null),
    [draft.gates, selection]
  );
  const selectedStack = useMemo(
    () =>
      selection?.kind === "stack"
        ? (catsByCell.get(cellKey(selection.x, selection.z)) ?? []).map((c) => c.color)
        : [],
    [catsByCell, selection]
  );

  // ── viewport: keep the board centred and sized to the window ────

  const fitToViewport = useCallback(() => {
    const el = viewportRef.current;
    if (!el || !grid) return;
    setZoom(fitZoom(grid.width, grid.length, el.clientWidth, el.clientHeight, CANVAS_CHROME_PX));
  }, [grid]);

  // A newly picked grid starts sized to the window rather than at whatever zoom
  // the previous one was left on, which for a big board meant opening off-screen.
  useEffect(() => {
    fitToViewport();
  }, [fitToViewport]);

  const onZoomStep = useCallback((direction: number) => setZoom((z) => stepZoom(z, direction)), []);

  function refreshLevels() {
    setLevels(listLevels());
    onLevelsChanged?.();
  }

  function loadLevel(level: SavedLevel) {
    // Levels saved before gates existed have no `gates` field at all.
    setDraft({ ...level, cats: [...level.cats], holes: [...level.holes], gates: [...(level.gates ?? [])] });
    setShapeCells(new Set());
    setSelection(null);
  }

  function newLevel() {
    if (!grid) return;
    setDraft(makeBlankDraft(grid.id));
    setShapeCells(new Set());
    setSelection(null);
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

  /**
   * Replaces every cat on one cell with `colors`, bottom-first. The cell's own
   * order is all that matters to Unity, so dropping the old entries and
   * appending the new run is enough — where they land in the level-wide list
   * carries no meaning.
   */
  function setStackCats(x: number, z: number, colors: BlockColor[]) {
    const key = cellKey(x, z);
    setDraft((prev) => ({
      ...prev,
      cats: [...prev.cats.filter((c) => cellKey(c.x, c.z) !== key), ...colors.map((color) => ({ color, x, z }))],
    }));
  }

  function removeHoleAt(cell: GridCell) {
    const key = cellKey(cell.x, cell.z);
    setDraft((prev) => ({
      ...prev,
      holes: prev.holes.filter((h) => !h.cells.some((c) => cellKey(c.x, c.z) === key)),
    }));
    setSelection(null);
  }

  function stampPresetAt(cell: GridCell) {
    if (!selectedPreset) return;
    const stamped = stampPreset(selectedPreset, cell, presetRotation);
    const id = newId();
    setDraft((prev) => {
      const keys = new Set(stamped.map((c) => cellKey(c.x, c.z)));
      const cleared = clearCells(prev, keys);
      const hole: HolePlacement = {
        id,
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
    setSelection({ kind: "hole", id });
  }

  // ── retune a placed hole's color, capacity or shape ─────────────

  function updateSelectedHole(patch: Partial<Pick<HolePlacement, "color" | "capacity">>) {
    setDraft((prev) => ({
      ...prev,
      holes: prev.holes.map((h) => (h.id === selectedHole?.id ? { ...h, ...patch } : h)),
    }));
  }

  function deleteSelectedHole() {
    setDraft((prev) => ({ ...prev, holes: prev.holes.filter((h) => h.id !== selectedHole?.id) }));
    setSelection(null);
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
    if (remaining.length === 0) setSelection(null);
  }

  // ── gates: one per boundary edge, holding an ordered queue of cats ──

  function updateSelectedGate(cats: BlockColor[]) {
    setDraft((prev) => ({
      ...prev,
      gates: prev.gates.map((g) => (g.id === selectedGate?.id ? { ...g, cats } : g)),
    }));
  }

  function deleteSelectedGate() {
    setDraft((prev) => ({ ...prev, gates: prev.gates.filter((g) => g.id !== selectedGate?.id) }));
    setSelection(null);
  }

  /** Whichever gate on this cell sits nearest the click, if any. */
  function gateAt(cell: GridCell, local: CellLocal): GatePlacement | null {
    const onCell = gatesByCell.get(cellKey(cell.x, cell.z)) ?? [];
    if (onCell.length === 0) return null;
    const nearest = nearestBoundarySide(
      onCell.map((g) => g.side),
      local.u,
      local.v
    );
    return onCell.find((g) => g.side === nearest) ?? onCell[0];
  }

  /**
   * Click a boundary cell to put a gate on the edge nearest the click, or an
   * existing one to open it. Either way the queue is composed in the panel, not
   * by clicking the board — a sequence of nine cats is not something you want
   * to enter one grid click at a time. Interior cells have no wall to replace.
   */
  function handleGateClick(cell: GridCell, local: CellLocal) {
    const hit = gateAt(cell, local);
    if (hit) {
      setSelection({ kind: "gate", id: hit.id });
      return;
    }
    const side = nearestBoundarySide(boundarySides(cell, isPlayable), local.u, local.v);
    if (!side) return; // interior cell: no wall here to replace
    const gate: GatePlacement = { id: newId(), x: cell.x, z: cell.z, side, cats: [] };
    setDraft((prev) => ({ ...prev, gates: [...prev.gates, gate] }));
    setSelection({ kind: "gate", id: gate.id });
  }

  function handleStackClick(cell: GridCell) {
    const hole = holesByCell.get(cellKey(cell.x, cell.z));
    // A cat can't stand on a hole. Showing what IS there beats a click that
    // silently does nothing.
    if (hole) setSelection({ kind: "hole", id: hole.id });
    else setSelection({ kind: "stack", x: cell.x, z: cell.z });
  }

  /**
   * Select whatever the click landed on. A selected hole keeps first claim on
   * the click so its grow/carve editing still works; everything else is picked
   * up in the order it sits on the cell.
   */
  function handleSelectClick(cell: GridCell, local: CellLocal) {
    const key = cellKey(cell.x, cell.z);
    const hitHole = holesByCell.get(key);

    // A selected hole keeps first claim, but only over the cells it can
    // actually edit — its own, and the empty ones touching it. Anywhere else
    // falls through to a normal selection rather than swallowing the click.
    if (selectedHole) {
      if (hitHole?.id === selectedHole.id) {
        carveSelectedHole(selectedHole, cell);
        return;
      }
      const touches = selectedHole.cells.some((c) => Math.abs(c.x - cell.x) + Math.abs(c.z - cell.z) === 1);
      if (!hitHole && touches) {
        growSelectedHole(selectedHole, cell);
        return;
      }
    }
    if (hitHole) {
      setSelection({ kind: "hole", id: hitHole.id });
      return;
    }
    const hitGate = gateAt(cell, local);
    if (hitGate) {
      setSelection({ kind: "gate", id: hitGate.id });
      return;
    }
    setSelection(catsByCell.has(key) ? { kind: "stack", x: cell.x, z: cell.z } : null);
  }

  function handlePaintStart(cell: GridCell, local: CellLocal) {
    const key = cellKey(cell.x, cell.z);
    switch (tool) {
      case "select":
        return handleSelectClick(cell, local);
      case "gate":
        return handleGateClick(cell, local);
      case "stack":
        return handleStackClick(cell);
      case "cat":
        dragActionRef.current = catsByCell.has(key) ? "remove" : "add";
        if (dragActionRef.current === "remove") removeCat(cell);
        else placeCat(cell);
        return;
      case "hole-preset":
        if (holesByCell.has(key)) removeHoleAt(cell);
        else stampPresetAt(cell);
        return;
      case "hole-shape":
        // shape mode: accumulate an outline first
        dragActionRef.current = shapeCells.has(key) ? "remove" : "add";
        applyShapeSelection(cell);
        return;
    }
  }

  function handlePaintDrag(cell: GridCell) {
    // Selections, gates and preset placements are deliberate single clicks.
    if (tool === "cat") {
      if (dragActionRef.current === "add") placeCat(cell);
      else removeCat(cell);
      return;
    }
    if (tool === "hole-shape") applyShapeSelection(cell);
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
    const id = newId();
    setDraft((prev) => {
      const cleared = clearCells(prev, shapeCells);
      return { ...cleared, holes: [...cleared.holes, makeHole(id, selectedColor, capacity, cells)] };
    });
    setShapeCells(new Set());
    setSelection({ kind: "hole", id });
  }

  function handleSaveLevel() {
    if (!draft.name.trim() || !grid) return;
    const saved: SavedLevel = { ...draft, name: draft.name.trim(), id: draft.id || newId() };
    saveLevel(saved);
    setDraft({ ...saved });
    refreshLevels();
    // A level saved while a folder is attached lands in it as JSON too, so the
    // folder stays the shareable copy of the library rather than a stale one.
    void folder.write(toFileName(saved.name, "level"), JSON.stringify(toLevelJson(saved, grid), null, 2));
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

  /**
   * Imports level JSON, one file or a whole folder.
   *
   * Every level file carries its own inline copy of the grid it was authored
   * on, so importing 40 levels that share a board would otherwise leave 40
   * identical grids behind. A grid whose shape already matches is reused
   * instead, and a level whose name already exists is replaced in place
   * (keeping its id), so re-reading a folder updates the library rather than
   * doubling it.
   */
  function importLevelFiles(files: FolderFile[]): string {
    if (files.length === 0) return "No .json files there.";
    let pool = listGrids();
    const byName = new Map(listLevels().map((level) => [level.name, level]));
    const failed: string[] = [];
    let added = 0;
    let updated = 0;
    let last: SavedLevel | null = null;

    for (const file of files) {
      try {
        const { grid: gridDraft, level: levelDraft } = fromLevelJson(parseLevelJson(file.text));
        let target = pool.find((g) => sameShape(gridDraft, g));
        if (!target) {
          target = { ...gridDraft, id: newId(), updatedAt: Date.now() };
          saveGrid(target);
          pool = [...pool, target];
        }
        const match = byName.get(levelDraft.name);
        const level: SavedLevel = {
          ...levelDraft,
          id: match?.id ?? newId(),
          gridId: target.id,
          updatedAt: Date.now(),
        };
        saveLevel(level);
        byName.set(level.name, level);
        last = level;
        if (match) updated++;
        else added++;
      } catch {
        failed.push(file.name);
      }
    }

    setGrids(listGrids());
    onGridsChanged?.();
    refreshLevels();
    if (last) loadLevel(last);

    const parts: string[] = [];
    if (added) parts.push(`${added} added`);
    if (updated) parts.push(`${updated} updated`);
    if (failed.length) parts.push(`${failed.length} skipped (${failed.slice(0, 3).join(", ")})`);
    return parts.join(" · ") || "Nothing to import.";
  }

  async function handleImportFiles(list: FileList | null) {
    if (!list) return;
    const files: FolderFile[] = [];
    for (const file of Array.from(list)) files.push({ name: file.name, text: await file.text() });
    importLevelFiles(files);
  }

  const totalCapacity = draft.holes.reduce((sum, h) => sum + h.capacity, 0);
  const gatedCats = draft.gates.reduce((sum, g) => sum + g.cats.length, 0);
  const placesHoles = family === "hole";
  const usesToolbarColor = tool === "cat" || placesHoles;

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
            multiple
            style={{ display: "none" }}
            multiple
            onChange={(e) => {
              void handleImportFiles(e.target.files);
              e.target.value = "";
            }}
          />
          <button onClick={() => fileInputRef.current?.click()}>Import JSON</button>
          <FolderBar folder={folder} noun="level" onFiles={importLevelFiles} />
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
                placeholder="New Level"
              />
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
              <ToolPanel
                family={family}
                catTool={catTool}
                holeTool={holeTool}
                onFamily={setFamily}
                onCatTool={setCatTool}
                onHoleTool={setHoleTool}
              />

              {/* Placement options. Stacks and gates take their colours from the
                  Stack Visualizer instead, and selecting takes none at all. */}
              {usesToolbarColor && (
                <div className="field-row">
                  {usesToolbarColor && (
                    <label className="color-field">
                      {family === "cat" ? "Cat colour" : "Hole colour"}
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
                  )}
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
                  {tool === "hole-preset" && (
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
                        <select
                          value={presetRotation}
                          onChange={(e) => setPresetRotation(Number(e.target.value) as RotationQuarterTurns)}
                        >
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

              {tool === "hole-shape" && (
                <div className="field-row">
                  <button onClick={() => setShapeCells(new Set())} disabled={shapeCells.size === 0}>
                    Clear Outline ({shapeCells.size})
                  </button>
                  <button className="primary" onClick={commitShape} disabled={shapeCells.size === 0}>
                    Create Hole
                  </button>
                </div>
              )}

              <p className="hint">{TOOL_HINTS[tool]}</p>
            </>
          )}
        </div>

        {grid && (
          <div className="canvas-row">
            <div className="canvas-stage">
              <div className="canvas-viewport" ref={viewportRef}>
                <div className="canvas-wrap">
                  <GridCanvas
                    width={grid.width}
                    length={grid.length}
                    cellPx={cellPx}
                    isInteractive={isPlayable}
                    onPaintStart={handlePaintStart}
                    onPaintDrag={handlePaintDrag}
                    onZoomStep={onZoomStep}
                    renderCell={(ctx, cell, rect) => {
                      const playable = isPlayable(cell);
                      ctx.fillStyle = playable ? palette.floor : palette.inert;
                      ctx.fillRect(rect.x, rect.y, rect.width, rect.height);
                      if (playable && tool === "hole-shape" && shapeCells.has(cellKey(cell.x, cell.z))) {
                        ctx.fillStyle = palette.selectedTint;
                        ctx.fillRect(rect.x, rect.y, rect.width, rect.height);
                      }
                    }}
                    renderOverlay={(ctx, cellRect) => {
                      // The cell a stack is being edited on gets a halo, since a
                      // stack has no outline of its own to highlight.
                      if (selection?.kind === "stack") {
                        const r = cellRect(selection.x, selection.z);
                        ctx.strokeStyle = palette.textAccent;
                        ctx.lineWidth = Math.max(2, cellPx * 0.07);
                        ctx.strokeRect(r.x + 1, r.y + 1, r.width - 2, r.height - 2);
                      }
                      // holes first: each is drawn as ONE shape across all its cells
                      for (const hole of draft.holes) {
                        drawHoleGroup(
                          ctx,
                          hole.cells,
                          cellRect,
                          BLOCK_COLOR_HEX[hole.color],
                          palette.holeVoid,
                          hole.capacity,
                          hole.id === selectedHole?.id ? palette.textAccent : undefined
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
                          gate.id === selectedGate?.id ? palette.textAccent : undefined
                        );
                      }
                      for (const [key, stack] of catsByCell) {
                        const [x, z] = key.split(",").map(Number);
                        drawCatStack(
                          ctx,
                          cellRect(x, z),
                          stack.map((c) => BLOCK_COLOR_HEX[c.color])
                        );
                      }
                    }}
                  />
                </div>
              </div>
              <div className="zoom-dock">
                <ZoomControl value={zoom} onChange={setZoom} onFit={fitToViewport} />
              </div>
            </div>

            <div className="canvas-side">
              {selectedHole ? (
                <HoleInspector
                  hole={selectedHole}
                  onColorChange={(color) => updateSelectedHole({ color })}
                  onCapacityChange={(value) => updateSelectedHole({ capacity: value })}
                  onDelete={deleteSelectedHole}
                  onDeselect={() => setSelection(null)}
                />
              ) : selectedGate ? (
                <StackVisualizer
                  kind="gate"
                  side={selectedGate.side}
                  location={`(${selectedGate.x}, ${selectedGate.z}) · ${selectedGate.side} edge`}
                  cats={selectedGate.cats}
                  onChange={updateSelectedGate}
                  onDelete={deleteSelectedGate}
                  onDeselect={() => setSelection(null)}
                />
              ) : selection?.kind === "stack" ? (
                <StackVisualizer
                  kind="stack"
                  location={`(${selection.x}, ${selection.z})`}
                  cats={selectedStack}
                  onChange={(cats) => setStackCats(selection.x, selection.z, cats)}
                  onDelete={() => setStackCats(selection.x, selection.z, [])}
                  onDeselect={() => setSelection(null)}
                />
              ) : (
                <p className="hint">{IDLE_PANEL_HINTS[tool]}</p>
              )}

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
        )}
      </main>
    </div>
  );
}

const TOOL_HINTS: Record<string, string> = {
  cat: "Click a cell to drop one cat in the selected colour, and drag to lay a run. Clicking a cell that already has cats lifts the top one off — to build a pile deliberately, switch to Stack.",
  stack: "Click any cell to open its pile in the Stack Visualizer, then set the colours and their order there. The top of the list is the top of the pile, and the only cat a hole can take.",
  gate: "Click a cell on the board's edge to put a gate on the wall nearest your click, or click an existing gate to reopen it. The queue itself is composed in the Stack Visualizer.",
  "hole-shape": "Drag out the outline of a tunnel, then Create Hole — the whole connected shape becomes ONE hole with a single shared capacity.",
  "hole-preset": "Click a cell to stamp the selected preset with that cell as its anchor; click any cell of a placed hole to remove the whole hole.",
  select: "Click a hole, gate or stack to open it on the right. With a hole selected, click a cell touching it to grow the shape, or one of its own cells to carve that cell away.",
};

const IDLE_PANEL_HINTS: Record<string, string> = {
  cat: "Cats go straight onto the grid in the colour picked above. Switch to Stack to compose a pile instead.",
  stack: "Click any cell on the grid to open its stack here.",
  gate: "Click a cell on the board's edge to add a gate, or an existing gate to edit its queue.",
  "hole-shape": "Draw an outline and commit it; the finished hole opens here.",
  "hole-preset": "Stamp a preset and the finished hole opens here.",
  select: "Click a hole, gate or stack on the grid to select it.",
};

function makeBlankDraft(gridId: string): SavedLevel {
  return { id: "", name: "", gridId, cats: [], holes: [], gates: [], updatedAt: 0 };
}
