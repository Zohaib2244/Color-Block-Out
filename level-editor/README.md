# Color Block Out — Level Editor (web)

A standalone, client-only web tool for authoring **Color Block Out** levels
without opening Unity. It mirrors the two in-editor tools under
`Assets/_GameData/Systems/Scripts/Editor/` and exports a level as JSON.

Nothing here touches the Unity project or its build — it's a sibling app
with its own `package.json`, deployable as a static site.

## Run it

```bash
cd level-editor
npm install
npm run dev      # http://localhost:5173
```

`npm run build` produces a static `dist/` you can host anywhere or open
directly.

## Design system

Visual style follows AVN Hub's "Chunky Blocks + Accent Border" design
system (same ember palette, sticker-card treatment, and DotGothic16/
JetBrains Mono pairing as `Zohaib2244/AVNHub`'s `styles/globals.css`), not
a design invented for this tool: rounded sticker cards with a 1.5px border
and a hard, zero-blur offset shadow; dark (default) and light themes with
a header toggle (`src/theme.ts`, persisted to `localStorage`); canvas fill
colors mirrored per-theme in `src/canvasPalette.ts` since `<canvas>` can't
read CSS custom properties directly.

## How it maps to the Unity tools

| Unity tool | Web equivalent | What it does |
| --- | --- | --- |
| `GridCreatorTool` (`Cat Puzzle/Grid Creator`) | **Grid Designer** tab | Define board width/length, toggle wall cells, save a reusable grid. |
| `CatLevelEditorWindow` (`Cat Puzzle/Cat Level Editor`) | **Level Editor** tab | Pick a saved grid, place Cats and Holes on top of it. |
| *(no Unity equivalent)* | **Hole Presets** tab | Draw a hole shape once, save it, and reuse it across any grid/level. |

### Working in the grid

Every grid canvas shares the same interactions:

- **Click or drag to paint.** A drag stroke commits to whatever the first
  cell became, so sweeping a wall (or selecting a run of cells) is one
  motion instead of one click per cell.
- **Zoom slider** — a multiplier over the auto-fit cell size, so resizing a
  grid still fits on screen while your zoom preference sticks.
- **Hover crosshair** — the hovered row and column are tinted and their
  axis labels highlight, so reading a cell's coordinates off a large grid
  doesn't need finger-tracing. The exact `x, z` is printed under the canvas.
- **Heavier gridlines every 5 cells**, and axis labels thin out to every
  5th when zoomed far out.

### Grid Designer

Same rules as `GridCreatorTool.FindInteriorCells`: a cell only becomes
**playable** if it is not a wall *and* cannot be reached from the grid
border by walking through non-wall cells. Painting a "wall" cell is just a
way to seal off a room — the canvas shows a live preview (green = will be
playable, warm red = open to the outside and will be dropped, with a
legend beside it) exactly like what
`GridManager.MarkExteriorCellsAsOccupied` produces when a `GridData` asset
is actually saved in Unity. The exported JSON stores this post-flood-fill
playable mask, not the raw wall toggles, since that's what
`GridData.wallCells` (inverted) ends up holding either way.

### Level Editor

Everything is **direct manipulation** — pick a color from the swatch row
(it drives both the cats and the holes you place next), then click the
grid. There is no select-then-commit step except when drawing a hole
outline, which genuinely needs the whole shape before it can become a hole.

- **Cats** — click a cell to drop a cat, click it again to take it away;
  drag to place or clear a run of them.
- **Holes (shape)** — drag out the outline of a tunnel, then **Create
  Hole**. Each cell's `HoleType`
  (`Isolated`/`EndCap`/`Straight`/`Corner`/`OneSide`/`Middle`) is derived
  purely from how many of its 4 neighbors are in the shape, and its
  `rotationQuarterTurns` is solved by rotating the hole type's default
  openings (`DEFAULT_HOLE_OPENINGS` in `src/level/holeShape.ts`) until it
  matches — the same algorithm as
  `CatLevelEditorWindow.GetHoleType`/`GetRotation`.
- **Holes (single)** — click to cut a one-cell hole with an explicit type
  and rotation; click it again to remove it.
- **Holes (preset)** — click to stamp a saved shape from the Hole Presets
  tab, using that cell as its anchor and the chosen placement rotation.
- **Edit** — click a placed hole to select it (it gets a halo), then retune
  it in the inspector: change its **color**, change its **capacity**, or
  reshape it by clicking an empty cell touching it to grow it or one of its
  own cells to carve that cell away. Every shape edit re-derives each
  cell's `holeType`/`rotationQuarterTurns`, so a grown corner becomes a
  `Corner` automatically. A hole must stay one connected shape, so a click
  that isn't adjacent to the selection is ignored — but carving a *middle*
  cell legitimately splits the hole, and both resulting pieces inherit the
  original's color and capacity.

Outside Edit mode, clicking any cell of a placed hole removes (or replaces)
the **whole** hole, since a hole is one unit rather than a pile of cells.

### A hole is one shape, not N cells

A connected tunnel is **one** hole: `{ color, capacity, cells[] }`. Capacity
belongs to the whole shape, so a 4-cell tunnel with capacity 5 holds five
cats in total — it is not four separate holes of five. That's why the
canvas draws one continuous outline around the entire shape with the
capacity printed once, rather than one boxed number per cell.

The outline is built by filling the union of the shape's cells in the rim
color and then filling an inset copy of the same union in the void color;
stroking would draw a line across every internal cell boundary and shatter
one hole back into N tiles. Two details make the union read as one form
rather than a chain of boxes: an edge facing a neighbour in the shape runs
all the way to the cell boundary instead of stopping at the inset, and a
corner is rounded only when *both* of its edges are on the outside of the
shape. A straight 1×3 therefore draws as a single capsule, and an L keeps a
proper sharp inner corner. Because each cell's region is still inset from
its bounds, two *separate* holes that happen to sit side by side render
with a visible seam between them and stay distinguishable.

Cats and holes are deliberately drawn as visual opposites so they can't be
confused even when they share a color: a **cat** is a solid filled creature
sitting on the floor (round head, pointed ears, two eyes), while a **hole**
is a dark recess cut *into* it, ringed in its color.

Inline validation flags overlapping placements, placements off the playable
area, invalid capacities, colors with no counterpart, and any color whose
total hole capacity is **less** than its cat count (an error: those cats
could never be collected) or **more** than it (a warning: unused slots).

`DEFAULT_HOLE_OPENINGS` is a placeholder — the real per-prefab values live
in your project's `CatHoleConfiguration` asset
(`CatHolePrefabData.defaultOpenings`), authored in Unity and not derivable
from code. If your prefabs use different default openings, edit that
constant to match so exported rotations line up with them.

### Hole Presets

Draw a connected shape on a blank 9×9 sandbox grid (same drawing
interaction as Holes (shape)) and save it under a name. A preset stores
each cell's `HoleType`/rotation plus its position relative to the shape's
own bounding-box corner — it isn't tied to any grid, level, or color, so
the same preset can be stamped anywhere. Rotating a preset at placement
time rotates both its cell positions and each cell's own hole rotation
together, so a saved corner piece still reads correctly after a 90° turn.

## Data model

Grids, levels, and hole presets are saved to `localStorage` (a small
project library — grids are reusable across many levels, same as
`GridData` assets in Unity, and presets are reusable across all of them). A
level can be exported to a standalone JSON file and re-imported later.

### Level JSON schema (`formatVersion: 3`)

```ts
interface LevelJson {
  formatVersion: 3;
  levelName: string;
  grid: {
    id: string;
    width: number;
    length: number;
    cellSize: number;
    // flat, index = z*width + x. TRUE = playable floor cell (post flood-fill).
    playableCells: boolean[];
  };
  cats: Array<{ color: BlockColor; x: number; z: number }>;
  // ONE entry per hole — a connected shape, not a single cell
  holes: Array<{
    id: string;
    color: BlockColor;
    // how many cats this whole shape can swallow before it's full; >= 1
    capacity: number;
    cells: Array<{
      x: number;
      z: number;
      holeType: "Isolated" | "EndCap" | "Straight" | "Corner" | "OneSide" | "Middle";
      rotationQuarterTurns: 0 | 1 | 2 | 3;
    }>;
  }>;
}
```

Format history:

- **v1** — holes were a flat list of single cells.
- **v2** — added `capacity` to each of those cells.
- **v3** — a hole is one shape, because capacity belongs to the whole tunnel
  rather than to each cell of it.

v1/v2 files still import: their loose cells are regrouped into
orthogonally-connected same-color shapes, and each shape takes the largest
capacity found among its cells (1 for v1, which had no capacity at all).

`capacity` has no counterpart in the Unity code yet — `CatHole` /
`CatPuzzleController.ResolveHole` still collect exactly one cat per hole —
so it's authored here ahead of the Unity-side implementation.

`BlockColor` is one of `Red | Orange | Yellow | Blue | Cyan | Green |
Purple | Pink | Teal`, matching `BlockColorTypes` in
`Utilities/Enums.cs`.

## Out of scope (for now)

There is no Unity-side importer yet — no editor script converts this JSON
back into `GridData`/`CatLevelData` assets, and no runtime JSON loader
exists in-game. The schema above was designed so that step is a mechanical
field mapping later: `playableCells` inverts directly into
`GridData.wallCells`, `cats` map 1:1 onto `CatPlacement`, and each hole's
`cells[]` map 1:1 onto `CatHolePlacement`. The genuinely new piece is
`capacity`, which is per-*hole* rather than per-cell, so the Unity side
needs some notion of a hole group (a shared id or a capacity counter on the
shape) plus a change to `CatPuzzleController.ResolveHole`, which today
collects a single cat and immediately completes the hole.
