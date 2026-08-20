import { useState } from "react";
import { DEFAULT_HOLE_OPENINGS, loadHoleConfig, saveHoleConfig } from "../holeConfig/holeConfigStore";
import { DIRECTIONS, HOLE_TYPES } from "../types";
import type { Direction, HoleOpeningsConfig } from "../types";

export function HoleConfigPanel() {
  const [config, setConfig] = useState<HoleOpeningsConfig>(() => loadHoleConfig());
  const [savedFlash, setSavedFlash] = useState(false);

  function toggleDirection(holeType: (typeof HOLE_TYPES)[number], direction: Direction) {
    setConfig((prev) => {
      const current = prev[holeType];
      const next = current.includes(direction) ? current.filter((d) => d !== direction) : [...current, direction];
      return { ...prev, [holeType]: next };
    });
  }

  function handleSave() {
    saveHoleConfig(config);
    setSavedFlash(true);
    setTimeout(() => setSavedFlash(false), 1500);
  }

  function resetToDefaults() {
    setConfig(DEFAULT_HOLE_OPENINGS);
  }

  return (
    <div className="screen">
      <main className="editor-main">
        <h2>Hole Openings Config</h2>
        <p className="hint">
          Mirrors <code>CatHoleConfiguration</code>'s <code>CatHolePrefabData.defaultOpenings</code> per hole type —
          the directions each hole prefab is open on at rotation 0 in your Unity project. This is authored data, not
          something derivable from code, so enter it here to match your actual <code>CatHoleConfiguration</code>{" "}
          asset. The Level Editor's auto-shape tool solves each cell's <code>rotationQuarterTurns</code> by rotating
          these until they match the drawn shape's actual open neighbors — get this wrong and exported rotations
          won't line up with your prefabs in-game.
        </p>

        <table className="hole-config-table">
          <thead>
            <tr>
              <th>Hole Type</th>
              {DIRECTIONS.map((dir) => (
                <th key={dir}>{dir}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {HOLE_TYPES.map((holeType) => (
              <tr key={holeType}>
                <td>{holeType}</td>
                {DIRECTIONS.map((dir) => (
                  <td key={dir}>
                    <input
                      type="checkbox"
                      checked={config[holeType].includes(dir)}
                      onChange={() => toggleDirection(holeType, dir)}
                    />
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>

        <div className="field-row">
          <button className="primary" onClick={handleSave}>
            Save Config
          </button>
          <button onClick={resetToDefaults}>Reset to Placeholder Defaults</button>
          {savedFlash && <span className="muted">Saved.</span>}
        </div>
      </main>
    </div>
  );
}
