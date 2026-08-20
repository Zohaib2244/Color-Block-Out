# Cat-and-hole direction

## The loop

The board holds one-cell coloured cats and movable holes. A hole is a connected shape of
one or more cells drawn by the designer. Drag a hole onto cats of its own colour and those
cats are collected. When no cats of a colour remain, every hole of that colour disappears.
The level is complete when all cats are collected.

## Architecture

### Assets

| Asset | What it holds | Made with |
| --- | --- | --- |
| `GridData` | Board shape (which cells are playable), width/length, cell size | `Cat Puzzle/Grid Creator` |
| `CatLevelData` | A `GridData` reference, cat placements, hole placements, camera and timer | `Cat Puzzle/Cat Level Editor` |
| `CatPuzzleConfig` | Project-wide prefabs and metrics, lives in `Assets/Resources` | `Cat Puzzle/Create Default Assets` |
| `CatHoleConfiguration` | The six hole piece prefabs and the sides each one is open on | same |
| `LevelData` | Ordered list of `CatLevelData` for play order | inspector |

One `GridData` can back any number of levels, so a board shape is drawn once and reused.

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
- `CatLevelBuilder.Capture(instance, asset)` — reads the scene back into the asset, picking up
  anything that was dragged around.

Holes carry an `originCell` plus normalised `offsets`, so moving one is a single cell change and
the piece meshes never need rebuilding.

### Coordinates

`GridManager` sits on the object placed at cell (0,0) and derives world positions from its own
transform, so the same grid asset works anywhere in the scene. Integer cells are the source of
truth; world positions are calculated on demand.

## Authoring workflow

1. `Cat Puzzle/Create Default Assets` once, to make the config and wire the prefabs.
2. `Cat Puzzle/Grid Creator` — paint the board shape and save a `GridData` asset.
3. `Cat Puzzle/Cat Level Editor` — pick that grid, `New Level`, then paint cats and draw holes.
   Holes appear in the scene immediately and can be nudged with the normal move tool.
4. `Save Level` writes the scene back to the asset. `Load Into Scene` brings it back for editing
   or for pressing Play to test.
5. Add finished levels to the `LevelData` collection on `GameManager`.
