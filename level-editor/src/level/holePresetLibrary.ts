import { loadJson, newId, saveJson } from "../storage";
import type { HoleShapePreset, HoleShapePresetCell } from "../types";

const STORAGE_KEY = "cbo-level-editor:hole-presets";

export function listPresets(): HoleShapePreset[] {
  return loadJson<HoleShapePreset[]>(STORAGE_KEY, []).sort((a, b) => b.updatedAt - a.updatedAt);
}

export function createPreset(name: string, cells: HoleShapePresetCell[]): HoleShapePreset {
  const preset: HoleShapePreset = { id: newId(), name, cells, updatedAt: Date.now() };
  const presets = loadJson<HoleShapePreset[]>(STORAGE_KEY, []);
  presets.push(preset);
  saveJson(STORAGE_KEY, presets);
  return preset;
}

export function deletePreset(id: string): void {
  const presets = loadJson<HoleShapePreset[]>(STORAGE_KEY, []).filter((p) => p.id !== id);
  saveJson(STORAGE_KEY, presets);
}
