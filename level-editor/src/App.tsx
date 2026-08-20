import { useState } from "react";
import { GridDesigner } from "./ui/GridDesigner";
import { LevelEditor } from "./ui/LevelEditor";
import { PresetDesigner } from "./ui/PresetDesigner";

type Tab = "grids" | "levels" | "presets";

function App() {
  const [tab, setTab] = useState<Tab>("grids");
  const [gridsVersion, setGridsVersion] = useState(0);
  const [presetsVersion, setPresetsVersion] = useState(0);

  return (
    <div className="app">
      <header className="app-header">
        <h1>Color Block Out — Level Editor</h1>
        <nav className="tabs">
          <button className={tab === "grids" ? "active" : ""} onClick={() => setTab("grids")}>
            Grid Designer
          </button>
          <button className={tab === "levels" ? "active" : ""} onClick={() => setTab("levels")}>
            Level Editor
          </button>
          <button className={tab === "presets" ? "active" : ""} onClick={() => setTab("presets")}>
            Hole Presets
          </button>
        </nav>
      </header>

      <div className="app-body" style={{ display: tab === "grids" ? "contents" : "none" }}>
        <GridDesigner onGridsChanged={() => setGridsVersion((v) => v + 1)} />
      </div>
      <div className="app-body" style={{ display: tab === "levels" ? "contents" : "none" }}>
        <LevelEditor
          gridsVersion={gridsVersion}
          presetsVersion={presetsVersion}
          onGridsChanged={() => setGridsVersion((v) => v + 1)}
        />
      </div>
      <div className="app-body" style={{ display: tab === "presets" ? "contents" : "none" }}>
        <PresetDesigner onPresetsChanged={() => setPresetsVersion((v) => v + 1)} />
      </div>
    </div>
  );
}

export default App;
