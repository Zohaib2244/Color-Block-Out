import { loadJson, saveJson } from "../storage";
import type { HoleOpeningsConfig } from "../types";

const STORAGE_KEY = "cbo-level-editor:hole-openings-config";

/**
 * Placeholder defaults, NOT authoritative. The real values live in each
 * project's `CatHoleConfiguration` asset (`CatHolePrefabData.defaultOpenings`)
 * authored in Unity. Edit these in the Hole Config panel to match your
 * actual asset so exported rotations line up with your hole prefabs.
 */
export const DEFAULT_HOLE_OPENINGS: HoleOpeningsConfig = {
  Isolated: [],
  EndCap: ["Up"],
  Straight: ["Up", "Down"],
  Corner: ["Up", "Right"],
  OneSide: ["Up", "Right", "Down"],
  Middle: ["Up", "Right", "Down", "Left"],
};

export function loadHoleConfig(): HoleOpeningsConfig {
  return loadJson<HoleOpeningsConfig>(STORAGE_KEY, DEFAULT_HOLE_OPENINGS);
}

export function saveHoleConfig(config: HoleOpeningsConfig): void {
  saveJson(STORAGE_KEY, config);
}
