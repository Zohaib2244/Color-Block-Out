# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Unity **6000.3.14f1** mobile puzzle game (URP). Editor install is at `J:\Unity\Editor\6000.3.14f1`.
Single playable scene: `Assets/_GameData/Systems/Scenes/GameScene.unity`.

`CAT_GAME_DESIGN.md` documents the gameplay loop and the level authoring workflow — read it before
touching the puzzle systems.

## Commands

There is no test suite. `com.unity.test-framework` is installed but no test assemblies exist, so
there is nothing to run. Verification is a compile check plus opening the editor.

### Compile-checking without opening Unity

Unity is the only thing that normally compiles this project, which makes CLI iteration slow. Roslyn
can be driven directly against Unity's own reference assemblies instead:

```bash
ROOT="$(pwd -W)"   # Windows-style path; csc resolves Git Bash's /g/... against the drive root
CSC="$(ls -d "C:/Program Files/dotnet/sdk"/*/Roslyn/bincore/csc.dll | tail -1)"

# 1. Pull the assembly references out of Unity's generated csproj (paths use backslashes).
grep -o '<HintPath>[^<]*</HintPath>' Assembly-CSharp-Editor.csproj | sed 's/<[^>]*>//g' | tr '\134' '/' > /tmp/refs.txt

# 2. Build a response file: -r: per reference, then every source file.
{ while IFS= read -r r; do case "$r" in [A-Za-z]:*) p="$r";; *) p="$ROOT/$r";; esac; echo "-r:\"$p\""; done < /tmp/refs.txt
  find "$ROOT/Assets/_GameData/Systems/Scripts" \
       "$ROOT/Assets/_GameData/External Packages/Voodoo/Vibrations/Scripts" \
       "$ROOT/Assets/Plugins/Demigiant/DOTween/Modules" -name '*.cs' \
    | while IFS= read -r f; do echo "\"$f\""; done
} > /tmp/args.rsp

# 3. Compile — editor pass, then player pass.
dotnet "$CSC" -nostdlib -noconfig -nologo -target:library -langversion:9 -define:UNITY_EDITOR -out:/tmp/check.dll @/tmp/args.rsp
dotnet "$CSC" -nostdlib -noconfig -nologo -target:library -langversion:9 -out:/tmp/check2.dll @/tmp/args.rsp
```

Gotchas that make this work:

- `-nostdlib` is required. Unity ships its own `mscorlib`/`netstandard` facades; letting the SDK add
  its own produces hundreds of bogus "predefined type not defined" errors.
- Use `pwd -W`, not `$PWD`. Git Bash reports `/g/Dev/...`, which csc resolves as `G:\g\Dev\...` and
  every source file "could not be found".
- `tr '\134' '/'` rather than `tr '\\' '/'` — the latter warns on every line.
- The DOTween module files must be included or every `DOFade`/`DOAnchorPos`/`DOColor` call fails —
  they live in a separate assembly in Unity but are plain sources here.
- **The second (player) pass matters.** Editor files drop out via their own `#if UNITY_EDITOR`
  guards; anything left referencing `UnityEditor` is a real bug. A stray `using UnityEditor...` in a
  runtime script has silently broken builds here before.

`dotnet build` on the checked-in `.csproj` files does not work — they are Unity-generated, list
sources explicitly (so new files are missing), and hit the corelib conflict above.

## Assemblies

Apart from `Assets/FolderIcons`, there are no asmdefs — everything lands in `Assembly-CSharp` and
`Assembly-CSharp-Editor`. Editor-only code goes under a `Scripts/Editor/` folder **and** is wrapped
in `#if UNITY_EDITOR`. Runtime code that needs editor APIs guards them inline (see
`GridManager.SaveGridDataToAsset`).

`Assets/_GameData/` is first-party content. `Assets/_GameData/External Packages/` and
`Assets/Plugins/` are vendored third-party — leave them alone.

## Architecture

### Level data pipeline

**Content is JSON files, not assets.** Levels are authored in the web tool under `level-editor/`
and exported as JSON; Unity's own tools read and write that same format. `GridData` and
`CatLevelData` are plain `[Serializable]` classes — *not* ScriptableObjects, and there are no
`.asset` files behind them any more.

- `GridData` — board shape only: which cells are playable, width/length, cell size. Lives in a grid
  JSON file (`gridJson.ts`, formatVersion 1) so a shape can be drawn once and reused. Authored by
  `Cat Puzzle/Grid Creator`. How high the content parents sit is *not* part of a board — see the
  container heights on `CatPuzzleConfig` below.
- `CatLevelData` — cat placements, hole placements, timer, and **its own copy of the board**. A
  level JSON (`levelJson.ts`, formatVersion 4) is therefore self-contained: reshaping the grid file
  it started from does not reach back into levels already made from it. Authored by
  `Cat Puzzle/Cat Level Editor`.
- `LevelData` — still a ScriptableObject, but now only the play order: `List<TextAsset>` of level
  JSON files, assigned to `GameManager`. It parses on demand and caches; `Get` hands out a `Clone()`
  so a retry restarts from the file.

`CatLevelJson` (runtime, `Scripts/Level/Json/`) is the only thing that reads or writes that format —
in play mode as well as in the editor. Two things it translates rather than copies: colour **names**
in the file map to palette **ids** via `CatColorEntry.displayName` (an unknown name is a hard error,
never a silent default), and a JSON hole's absolute cells become a `CatHolePlacement` origin plus
offsets. Gates exist in v4 files but have no Unity counterpart, so they are dropped with a warning.
`LevelJsonFormat` holds the DTOs and must stay field-for-field in step with `level-editor/src/io/`.

Old ScriptableObject content is recovered by `Cat Puzzle/Migrate Level Assets To JSON`, which reads
the orphaned `.asset` YAML directly (nothing can load it as an object once the class stops being a
ScriptableObject), writes the JSON beside it, and repoints each `LevelData` collection.

`CatPuzzleConfig` lives at `Assets/_GameData/Systems/Scriptable Objects/MISC/CatPuzzleConfig.asset`
and holds every shared prefab and metric (cell/wall prefabs, cat prefab, `CatHoleConfiguration`,
heights and offsets) plus the `CatColorPalette`. Nothing loads it implicitly — it is wired onto the
scene's `LevelSpawner` and onto each authoring window — so what ships is always what someone
assigned. Parsing a level needs its palette to turn colour names into ids, and `GridBuilder` reads
the four **container heights** (`cellParentHeight`, `wallParentHeight`, `catParentHeight`,
`holeParentHeight`) from it for every grid it builds: those are project wide, so changing one moves
that container in every level rather than in one board. They land when a grid is next built, so a
grid already in the scene needs a reload to pick them up. `Cat Puzzle/Create Default Assets` creates
and repairs it.

### Scene ownership

The scene owns exactly **one** `CatPuzzleController`. It is never part of a level. Levels are spawned
under its `levelRoot` as a `CatLevelInstance`, which binds itself to the controller on `Start` and
unbinds on destroy — so the controller outlives every level and no level asset points back into the
scene. `LevelManager` is scene-level presentation only (spawn tween, timer on first move, completion
hand-off) and listens to controller events.

### Shared builders

The editor tooling and runtime spawning go through the same code, so what a designer sees while
authoring is exactly what ships. Never duplicate build logic into an editor script:

- `GridBuilder.Build(GridData, parent)` — cells, straight walls, outer and inner corners.
- `CatHoleBuilder.Build(placement, grid, parent)` — a hole is a *connected set of cells*, not a single
  prefab. Each cell counts its neighbours inside the shape, picks `Isolated`/`EndCap`/`Straight`/
  `Corner`/`OneSide`/`Middle`, and rotates so the prefab's open sides line up.
- `CatLevelBuilder.Build` / `.Capture` — spawn a level, and read the scene back into the asset.

These run in both edit and play mode. Use `GridBuilder.InstantiatePrefab` / `RegisterCreated` /
`DestroyChildren` rather than `Object.Instantiate`/`Destroy` directly — they keep prefab links and
undo working in the editor and fall back to plain runtime calls in play mode.

### Coordinates

Integer `Vector2Int` cells are the source of truth; world positions are derived on demand from
`GridManager`, which sits on the object placed at cell (0,0) and uses its own transform. Never store
world positions as identity.

**Editor grid views draw z=0 at the bottom row** — every window flips the Y axis when hit-testing
(`z = height - 1 - floor(...)`). Match that when adding a new grid view.

### Colours

`BlockColorTypes` maps by **enum index** to materials loaded by name from
`Resources/Materials/BlockColors/<Name>.mat` (cats) and `Resources/Materials/GateColors/<Name>.mat`
(holes), via `GameConstants`. Reordering or renaming the enum silently repoints already-saved level
content. Add new colours at the end.

### Singletons and UI

`GameManager`, `GameUIManager`, `AudioManager` and `CatPuzzleController` are all scene singletons with
an `Instance` set in `Awake`, no `DontDestroyOnLoad` (there is only one gameplay scene). UI is driven
by `GameUIManager.ShowScreen(ScreenType, bgIndex, onComplete)` against a serialized list of screens
rather than by activating objects directly.

## Editor tooling conventions

All menu items live under `Cat Puzzle/`: `Grid Creator`, `Cat Level Editor`, `Import Level JSON`
(files dropped in from the web tool), `Validate Level Files`, `Migrate Level Assets To JSON` and
`Create Default Assets`.

In the level editor, **the scene is the working document**: painting creates real GameObjects, holes
can be nudged with the normal move tool, and `Save Level` snaps everything to cells, captures the
scene, and writes the level JSON. `Load Into Scene` rebuilds from that file. The file is the export —
there is no separate export step, and no asset in between.

Both windows take `TextAsset` object fields rather than typed asset fields, since a level and a grid
are both just `.json` now. A picked file is only adopted once it parses, so choosing the wrong file
does not lose the one already open or repoint where `Save Level` writes.

`CatLevelInstance` and `GridManager` each keep what they were built from as a serialized JSON
*string* rather than a plain serialized field. Unity rebuilds a serializable class field as a default
instance instead of leaving it null, so "no level" and "an empty level" would be indistinguishable
across a domain reload. Do the same for any new field of this kind.

`CatLevelEditorWindow` routes every scene mutation through `Defer(...)`, which runs the action on the
next `EventType.Layout` pass. Creating or destroying objects mid-event changes the control count
between Layout and Repaint and throws GUILayout errors — keep new mutating actions deferred.

## Unity asset handling

Every asset needs its `.meta` file committed; references are GUID + fileID, so moving or renaming
through the OS instead of the editor breaks them. Renaming a serialized field drops the saved data
for that field. Hand-editing `.asset` YAML works (a script GUID was repointed this way to revive an
orphaned asset) but is easy to get wrong — prefer an editor script.
