# Cat-and-hole direction

## The loop

The board holds one-cell coloured cats and movable holes. A hole is a connected shape of
one or more cells drawn by the designer. Drag a hole onto cats of its own colour and those
cats are collected. When no cats of a colour remain, every hole of that colour disappears.
The level is complete when all cats are collected.

A cell holding two or more cats (a "pile") shows a badge with the stack count, at the bottom-left
corner of whichever cat is currently on top; a hole shows one too, for how many more cats it can
still swallow. Both come from the single `CatPuzzleConfig.countBadgePrefab` and are tinted to the
pile/hole's colour — see `CatLevelBuilder.RefreshPileBadges` and `CatHoleCountBadge`.

## Architecture

### Data

Levels are **JSON files**, authored in the web level editor under `level-editor/` and read by the
game as they are. `GridData` and `CatLevelData` are plain serializable classes, not assets.

| What | What it holds | Made with |
| --- | --- | --- |
| Grid JSON | Board shape (which cells are playable), width/length, cell size | `Cat Puzzle/Grid Creator`, or the web tool |
| Level JSON | Cat placements, hole placements, timer, and its own copy of the board | `Cat Puzzle/Cat Level Editor`, or the web tool |
| `CatPuzzleConfig` | Project-wide prefabs, metrics (including the four container heights) and the colour palette | `Cat Puzzle/Create Default Assets` |
| `CatHoleConfiguration` | The six hole piece prefabs and the sides each one is open on | same |
| `LevelData` | Ordered list of level JSON files for play order | inspector |

A grid file is a starting point, not a live link: once a level is saved it carries its own board, so
a level is one self-contained file and reshaping the grid later leaves it alone. `CatLevelJson` is
the only code that reads or writes the format, at runtime as well as in the editor. Colours travel
as names ("Red") and are matched to palette ids by `CatColorEntry.displayName`, so the two sides have
to agree on spelling. Gates can be authored on the web but have no Unity counterpart yet, so each
one's queued cats are placed as an ordinary pile on the gate's cell instead, with the wall on that
edge left open and a warning noting the substitution (see `CatLevelJson.ApplyGates`).

### Scene

The scene owns exactly one `CatPuzzleController`. It never lives inside a level. Levels are
spawned under its `levelRoot` as a `CatLevelInstance`, which binds itself to the controller on
`Start` and unbinds on destroy. `LevelManager` listens to the controller for presentation:
the spawn tween, the timer (started on the first hole move) and the completion hand-off.

### Builders

Both the editor tooling and runtime spawning go through the same code, so what a designer
sees while authoring is what ships:

- `GridBuilder.Build(GridData, parent)` — cells, straight walls, outer corners and inner corners.
- `CatHoleBuilder.Build(placement, grid, parent)` — one piece per covered cell. Each cell counts
  its neighbours inside the shape and picks `Isolated` / `EndCap` / `Straight` / `Corner` /
  `OneSide` / `Middle`, then rotates so the prefab's open sides line up.
- `CatLevelBuilder.Build(CatLevelData, parent)` — grid plus cats plus holes.
- `CatLevelBuilder.Capture(instance, level)` — reads the scene back into the level, picking up
  anything that was dragged around, ready to be written out as JSON.

Holes carry an `originCell` plus normalised `offsets`, so moving one is a single cell change and
the piece meshes never need rebuilding.

### Coordinates

`GridManager` sits on the object placed at cell (0,0) and derives world positions from its own
transform, so the same board works anywhere in the scene. Integer cells are the source of
truth; world positions are calculated on demand.

## Authoring workflow

1. `Cat Puzzle/Create Default Assets` once, to make the config and wire the prefabs.
2. Author on the web (`level-editor/`) and export, then `Cat Puzzle/Import Level JSON` to drop the
   files into the project — or stay in Unity:
   - `Cat Puzzle/Grid Creator` — paint the board shape and save a grid JSON file.
   - `Cat Puzzle/Cat Level Editor` — pick that grid file, `New Level`, then paint cats and draw
     holes. Holes appear in the scene immediately and can be nudged with the normal move tool.
3. `Save Level` writes the scene back to the level's JSON file. `Load Into Scene` brings it back for
   editing or for pressing Play to test.
4. Add the level file to a `LevelData` collection — `Add To Play Order` in the level editor, or the
   import window does it — and assign that collection to `GameManager`.
5. `Cat Puzzle/Validate Level Files` checks every level for content on blocked cells, overlapping
   holes, and colours that can never be cleared.

Content authored before levels became files is converted by `Cat Puzzle/Migrate Level Assets To
JSON`, which also repoints the collections at the new files.
