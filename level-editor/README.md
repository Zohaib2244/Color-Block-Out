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

## How it maps to the Unity tools

| Unity tool | Web equivalent | What it does |
| --- | --- | --- |
| `GridCreatorTool` (`Cat Puzzle/Grid Creator`) | **Grid Designer** tab | Define board width/length, toggle wall cells, save a reusable grid. |
| `CatLevelEditorWindow` (`Cat Puzzle/Cat Level Editor`) | **Level Editor** tab | Pick a saved grid, place Cats and Holes on top of it. |

### Grid Designer

Same rules as `GridCreatorTool.FindInteriorCells`: a cell only becomes
**playable** if it is not a wall *and* cannot be reached from the grid
border by walking through non-wall cells. Toggling a "wall" cell is just a
way to seal off a room — the canvas shows a live preview (green = will be
playable, amber = open to the outside and will be dropped) exactly like
what `GridManager.MarkExteriorCellsAsOccupied` produces when a
`GridData` asset is actually saved in Unity. The exported JSON stores this
post-flood-fill playable mask, not the raw wall toggles, since that's what
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

Holes render as pipe-like pieces (a core with a stub toward each open
side) so a connected shape visually reads as a tunnel, and incorrect
rotation solving is easy to spot at a glance. Inline validation flags
overlapping placements, placements off the playable area, and cat/hole
colors that don't have a match.

`DEFAULT_HOLE_OPENINGS` is a placeholder — the real per-prefab values live
in your project's `CatHoleConfiguration` asset
(`CatHolePrefabData.defaultOpenings`), authored in Unity and not derivable
from code. If your prefabs use different default openings, edit that
constant to match so exported rotations line up with them.

## Data model

Grids and levels are saved to `localStorage` (a small project library —
grids are reusable across many levels, same as `GridData` assets in
Unity). A level can be exported to a standalone JSON file and re-imported
later.

### Level JSON schema (`formatVersion: 1`)

```ts
interface LevelJson {
  formatVersion: 1;
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
  }>;
}
```

`BlockColor` is one of `Red | Orange | Yellow | Blue | Cyan | Green |
Purple | Pink | Teal`, matching `BlockColorTypes` in
`Utilities/Enums.cs`.

## Out of scope (for now)

There is no Unity-side importer yet — no editor script converts this JSON
back into `GridData`/`CatLevelData` assets, and no runtime JSON loader
exists in-game. The schema above was designed so that step is a mechanical
field mapping later: `playableCells` inverts directly into
`GridData.wallCells`, and `cats`/`holes` map 1:1 onto `CatPlacement`/
`CatHolePlacement`.
