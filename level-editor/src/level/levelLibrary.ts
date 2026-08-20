import { loadJson, newId, saveJson } from "../storage";
import type { SavedLevel } from "../types";

const STORAGE_KEY = "cbo-level-editor:levels";

export function listLevels(): SavedLevel[] {
  return loadJson<SavedLevel[]>(STORAGE_KEY, []).sort((a, b) => b.updatedAt - a.updatedAt);
}

export function getLevel(id: string): SavedLevel | undefined {
  return listLevels().find((l) => l.id === id);
}

export function createLevel(name: string, gridId: string): SavedLevel {
  const level: SavedLevel = {
    id: newId(),
    name,
    gridId,
    cats: [],
    holes: [],
    updatedAt: Date.now(),
  };
  saveLevel(level);
  return level;
}

export function saveLevel(level: SavedLevel): void {
  const levels = loadJson<SavedLevel[]>(STORAGE_KEY, []);
  const index = levels.findIndex((l) => l.id === level.id);
  level.updatedAt = Date.now();
  if (index >= 0) levels[index] = level;
  else levels.push(level);
  saveJson(STORAGE_KEY, levels);
}

export function deleteLevel(id: string): void {
  const levels = loadJson<SavedLevel[]>(STORAGE_KEY, []).filter((l) => l.id !== id);
  saveJson(STORAGE_KEY, levels);
}
