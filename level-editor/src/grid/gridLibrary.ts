import { loadJson, newId, saveJson } from "../storage";
import type { SavedGrid } from "../types";
import { makeFlatGrid } from "./floodFill";

const STORAGE_KEY = "cbo-level-editor:grids";

export function listGrids(): SavedGrid[] {
  return loadJson<SavedGrid[]>(STORAGE_KEY, []).sort((a, b) => b.updatedAt - a.updatedAt);
}

export function getGrid(id: string): SavedGrid | undefined {
  return listGrids().find((g) => g.id === id);
}

export function createGrid(name: string, width: number, length: number, cellSize: number): SavedGrid {
  const grid: SavedGrid = {
    id: newId(),
    name,
    width,
    length,
    cellSize,
    wallToggles: makeFlatGrid(width, length, false),
    updatedAt: Date.now(),
  };
  saveGrid(grid);
  return grid;
}

export function saveGrid(grid: SavedGrid): void {
  const grids = loadJson<SavedGrid[]>(STORAGE_KEY, []);
  const index = grids.findIndex((g) => g.id === grid.id);
  grid.updatedAt = Date.now();
  if (index >= 0) grids[index] = grid;
  else grids.push(grid);
  saveJson(STORAGE_KEY, grids);
}

export function deleteGrid(id: string): void {
  const grids = loadJson<SavedGrid[]>(STORAGE_KEY, []).filter((g) => g.id !== id);
  saveJson(STORAGE_KEY, grids);
}

export function importGrid(grid: SavedGrid): SavedGrid {
  const copy: SavedGrid = { ...grid, id: newId(), updatedAt: Date.now() };
  saveGrid(copy);
  return copy;
}
