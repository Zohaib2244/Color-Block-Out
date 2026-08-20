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

Two ways to place holes, matching `CatLevelEditorWindow`:

- **Holes (shape)** — select a connected run of cells; each cell's
  `HoleType` (`Isolated`/`EndCap`/`Straight`/`Corner`/`OneSide`/`Middle`) is
  derived purely from how many of its 4 neighbors are also selected, and
  its `rotationQuarterTurns` is solved by rotating the hole type's default
  openings (`DEFAULT_HOLE_OPENINGS` in `src/level/holeShape.ts`) until it
  matches the actual open neighbors — the same algorithm as
  `CatLevelEditorWindow.GetHoleType`/`GetRotation`.
- **Holes (manual)** — place a single hole with an explicit type and
  rotation.
- **Holes (preset)** — pick a saved shape from the Hole Presets tab, click a
  single anchor cell, choose a placement rotation (0/90/180/270), and Stamp
  Preset places every cell of the shape relative to that anchor.

Every hole also carries a **cat capacity** — how many cats it can swallow
before it's full — set by the `Cat Capacity` field and applied to every
hole placed by that action. It's drawn as the number inside the hole.

Cats and holes are deliberately drawn as visual opposites so they can't be
confused even when they share a color: a **cat** is a solid filled creature
sitting on the floor (round head, pointed ears, two eyes), while a **hole**
is a dark socket cut *into* the floor, ringed in its color, with a tunnel
stub running to the cell edge for each open side — so a connected shape
reads as one continuous tunnel and mis-solved rotations are obvious at a
glance.

Inline validation flags overlapping placements, placements off the playable
area, invalid capacities, colors with no counterpart, and — now that
capacity exists — any color whose total hole capacity is **less** than its
cat count (an error: those cats could never be collected) or **more** than
it (a warning: unused slots).

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

### Level JSON schema (`formatVersion: 2`)

```ts
interface LevelJson {
  formatVersion: 2;
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
  holes: Array<{
    color: BlockColor;
    x: number;
    z: number;
    holeType: "Isolated" | "EndCap" | "Straight" | "Corner" | "OneSide" | "Middle";
    rotationQuarterTurns: 0 | 1 | 2 | 3;
    // how many cats this hole can swallow before it's full; >= 1
    capacity: number;
  }>;
}
```

**v2** added `capacity`. v1 files still import — their holes are migrated to
`capacity: 1`, which is what the original one-cat-per-hole behaviour
amounted to. `capacity` has no counterpart in the Unity code yet
(`CatHole`/`CatPuzzleController` still collect exactly one cat per hole);
it's authored here ahead of the Unity-side implementation.

`BlockColor` is one of `Red | Orange | Yellow | Blue | Cyan | Green |
Purple | Pink | Teal`, matching `BlockColorTypes` in
`Utilities/Enums.cs`.

## Out of scope (for now)

There is no Unity-side importer yet — no editor script converts this JSON
back into `GridData`/`CatLevelData` assets, and no runtime JSON loader
exists in-game. The schema above was designed so that step is a mechanical
field mapping later: `playableCells` inverts directly into
`GridData.wallCells`, and `cats`/`holes` map 1:1 onto `CatPlacement`/
`CatHolePlacement` — the one genuinely new piece being `capacity`, which
needs a matching field on `CatHolePlacement`/`CatHole` and a change to
`CatPuzzleController.ResolveHole` (which today collects a single cat and
completes the hole).
