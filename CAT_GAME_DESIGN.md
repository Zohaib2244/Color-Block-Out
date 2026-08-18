# Cat-and-hole direction

## Understood loop

The board contains one-cell colored cats and movable one-cell holes. Move a hole onto a cat of the same color; that cat is collected. When no cats of that color remain, every active hole of that color disappears. The level is complete when all cats are collected.

## Code review

- `GridManager` is the strongest reusable piece: grid coordinates, cell/wall registries, serialized data, and world/grid conversion are valuable.
- The current gameplay contract is too coupled to blocks. `BlockInputManager`, `BlockMeshHandler`, `BlockShape`, and `LevelManager` use block terminology and multi-cell assumptions everywhere.
- `GridManager` mixes editor persistence, scene discovery, occupancy, movement, and gameplay events. That will make cat rules difficult to test and increases regression risk.
- `Vector3` keys in `_worldToGridMap` are fragile when transforms or floating-point spacing change; use integer grid coordinates as the source of truth and calculate world positions on demand.
- `GameConstants` loads materials by enum index. Renaming or reordering `BlockColorTypes` can silently change saved content; use explicit color IDs or a serialized color palette for production.
- Existing `HoleConfiguration` describes connected hole shapes, which is useful for the old prototype but not for the new one-cell destination rule.

`CatPuzzleController`, `CatPiece`, and `CatHole` provide a clean rules layer without deleting the prototype. The next migration should connect their grid positions to `GridManager` world-position helpers, then replace block-specific touch handling with a hole drag handler.
