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
- **The board is centred in its own viewport.** The page never scrolls: the
  grid sits in the middle of a scrolling stage, the side panel scrolls
  separately, and the toolbar stays put. A grid too big to fit scrolls from
  its true top-left rather than having its top and left edges clipped off.
- **Zoom dock**, floating at the stage's bottom-right: `−` / `+` step
  buttons, a slider, the current percentage, **Fit** (sizes the grid to the
  window) and **100%**. `Ctrl`/`Cmd` + scroll wheel over the grid zooms too.
  Zoom is a multiplier over the auto-fit cell size, so resizing a grid still
  fits on screen while your zoom preference sticks; picking a different grid
  re-fits it to the window rather than opening it half off-screen.
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

The tool panel asks the question in the order you actually think about it:
**what kind of thing** — a Cat, a Hole, or nothing at all (Select) — and
only then **which flavour** of it. Each family remembers the variant you
last used, so Cat → Hole → Cat comes back to the cat tool you were on.

    Add        [ Cat ]  [ Hole ]  |  [ Select ]
    Cat type   [ Normal ]  [ Stack ]  [ Gate ]
    Hole type  [ Single ]  [ Shape ]  [ Preset ]

Placement is **direct manipulation** — pick a color from the swatch row (it
drives the cats and holes you place next), then click the grid. Stacks and
gates are the exception: you pick a cell or a wall, and the sequence itself
is composed in the **Stack Visualizer** side panel, because a queue of nine
cats is not something you want to enter one grid click at a time.

**Cat tools**

- **Normal** — click a cell to drop one cat, click it again to lift the top
  one off; drag to place or clear a run of them. Cats **stack**, so clicking
  an occupied cell adds another on top rather than refusing.
- **Stack** — click any cell to open its pile in the Stack Visualizer, then
  set the colors and their order there. See *A stack of cats* below.
- **Gate** — click a cell on the board's edge to put a gate on the wall
  nearest your click, or an existing gate to reopen it; the queue is composed
  in the same Stack Visualizer. See *Gates* below.

**Hole tools**

- **Shape** — drag out the outline of a tunnel, then **Create
  Hole**. Each cell's `HoleType`
  (`Isolated`/`EndCap`/`Straight`/`Corner`/`OneSide`/`Middle`) is derived
  purely from how many of its 4 neighbors are in the shape, and its
  `rotationQuarterTurns` is solved by rotating the hole type's default
  openings (`DEFAULT_HOLE_OPENINGS` in `src/level/holeShape.ts`) until it
  matches — the same algorithm as
  `CatLevelEditorWindow.GetHoleType`/`GetRotation`.
- **Single** — click to cut a one-cell hole with an explicit type
  and rotation; click it again to remove it.
- **Preset** — click to stamp a saved shape from the Hole Presets
  tab, using that cell as its anchor and the chosen placement rotation.

Any hole you place opens in the side panel straight away, so its color and
capacity can be retuned without switching tools first.

**Select** — click a hole, gate or stack to open it on the right. A selected
hole gets a halo and can be reshaped in place: click an empty cell touching
it to grow it, or one of its own cells to carve that cell away. Every shape
edit re-derives each cell's `holeType`/`rotationQuarterTurns`, so a grown
corner becomes a `Corner` automatically. A hole must stay one connected
shape, so a click that isn't adjacent to it can't grow it — that click falls
through and selects whatever it landed on instead. Carving a *middle* cell
legitimately splits the hole, and both resulting pieces inherit the
original's color and capacity.

Outside the Select tool, clicking any cell of a placed hole removes (or
replaces) the **whole** hole, since a hole is one unit rather than a pile of
cells.

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

### A stack of cats

More than one cat may share a cell. **Order is load-bearing**: among the
entries on one cell, earlier is *lower* and the last one is on **top**. That
is exactly the contract Unity already implements — `RestackCats` lifts each
cat on a cell by `index * catStackHeight` in list order, and `TopCatAt`
exposes only the highest one — so painting order here is stack order there.

Only the top cat matters in play: it is the one a hole can take, and its
color alone decides whether a hole may enter the cell at all.

A stack is drawn as a pile of **separately outlined bands, one per cat**,
running up the cell in stack order, with the topmost band wearing the ears
and a light rim, and a badge carrying the true total. Overlapping cat heads
were tried first and answered none of the three questions a designer
actually asks of a stacked cell — which colors, in what order, how many —
so each cat gets its own band, ink-outlined and gapped so neighbouring
colors stay countable even when they're near neighbours on the palette.
Past five cats the bands stop and the badge carries the count, with a stub
peeking out below the pile so "there's more under this" stays visible.

The **Stack Visualizer** is the authoritative view: pick the Stack tool,
click a cell, and it opens with a full-size preview drawn through the very
same canvas routines the grid uses (so the panel can never disagree with the
board) above a numbered list, top first. Add colors from the palette, drag
the order around with `↑`/`↓`, remove with `×`, or reverse the whole run.

### Gates

A **gate** sits on one boundary edge of a playable cell, standing in for the
wall Unity's `GridBuilder.BuildWalls` would otherwise generate there, and
holds an ordered queue of cats. In play the hole is moved onto the gate's own
cell — the cell in front of the mouth — and if the gate's **topmost** cat
matches that hole's color, the cat hops out into it.

Queue index 0 is the topmost, the one that leaves next. Gates share the
**Stack Visualizer** with cat stacks — they are the same thing to a designer,
an ordered run of colored cats where only one end is live — so it shows the
queue as a numbered list (head labelled `next`) rather than a color count,
because the order *is* the content. The two differ only in which end of the
stored array is the live one, and the panel hides that split by always
listing top-first and converting on the way in and out.

On the canvas a gate is a bar laid along its edge showing only the **head**
of the queue: the first few pips, ringed on the one that leaves next, plus a
badge with the true length. A one-cell bar cannot legibly hold a queue of
nine, so the full sequence is the panel's job and the bar is the at-a-glance
summary.

A gate is drawn just inside its own cell rather than out in the neighbouring
blocked cell, so a gate on the outermost row stays on canvas instead of
colliding with the axis labels — and so it reads as belonging to the cell
that actually matters in play. Placing a hole never clears a gate: parking a
hole on the gate's cell is the whole point of one.

Gate cats count toward the color budget below, since they still have to be
collected before the level can finish.

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

### Level JSON schema (`formatVersion: 4`)

```ts
interface LevelJson {
  formatVersion: 4;
  levelName: string;
  grid: {
    id: string;
    width: number;
    length: number;
    cellSize: number;
    // flat, index = z*width + x. TRUE = playable floor cell (post flood-fill).
    playableCells: boolean[];
  };
  // Several entries MAY share a cell — a stack. Within one cell, earlier
  // entries sit LOWER and the last one is on top (see "A stack of cats").
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
  gates: Array<{
    id: string;
    // the playable cell the gate feeds — where a hole gets parked
    x: number;
    z: number;
    // edge of (x, z) the gate replaces the wall on. Up=+z, Right=+x, Down=-z, Left=-x
    side: "Up" | "Right" | "Down" | "Left";
    // queued cats; index 0 is the topmost — the next one out
    cats: BlockColor[];
  }>;
}
```

Format history:

- **v1** — holes were a flat list of single cells.
- **v2** — added `capacity` to each of those cells.
- **v3** — a hole is one shape, because capacity belongs to the whole tunnel
  rather than to each cell of it.
- **v4** — added `gates`, and cats may now share a cell as a stack.

v1/v2 files still import: their loose cells are regrouped into
orthogonally-connected same-color shapes, and each shape takes the largest
capacity found among its cells (1 for v1, which had no capacity at all).
v1–v3 files import with an empty `gates` list and no stacks, since neither
could be expressed before v4. Malformed `gates` entries (bad coordinates, an
unrecognised `side`, unknown colors in a queue) are dropped on import rather
than failing the file.

`BlockColor` is one of `Red | Orange | Yellow | Blue | Cyan | Green |
Purple | Pink | Teal`, matching the `displayName`s in the project's
`CatColorPalette` asset, which is what the Unity importer resolves against.

## What Unity does with this

`CatLevelJsonImporter` (`Cat Puzzle/Import Level JSON`) converts an export
into `GridData` + `CatLevelData` assets. It accepts **v3 and v4**, since v4
only added things v3 could not express.

- **Cat stacks import as-is.** `BuildCats` maps entries 1:1 onto
  `CatPlacement` in file order and never dedupes by cell, and the runtime
  already stacks them — `CatLevelBuilder.RestackCats` lifts each cat on a
  cell by `index * catStackHeight`, and `CatPuzzleController.TopCatAt`
  exposes only the top one to a hole. So authoring order here is stack order
  in game, end to end, with no Unity change needed.
- **Gates do not import yet.** There is no gate on the Unity side — no data
  class, prefab, builder or rule. The importer parses the `gates` array only
  so it can *warn* how many gates and how many queued cats were dropped,
  rather than losing authored content silently. A level exported with gates
  will therefore be missing those cats in Unity until the runtime lands.

Implementing gates in Unity means: a `CatGatePlacement` on `CatLevelData`, a
gate prefab that replaces the wall segment `GridBuilder.BuildWalls` emits for
that (cell, side), and a rule in `CatPuzzleController` that pops the head of
the queue when a hole whose colour matches is parked on the gate's cell.
