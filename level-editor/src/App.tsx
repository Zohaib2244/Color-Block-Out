import { useState } from "react";
import { GridDesigner } from "./ui/GridDesigner";
import { LevelEditor } from "./ui/LevelEditor";
import { HoleConfigPanel } from "./ui/HoleConfigPanel";

type Tab = "grids" | "levels" | "hole-config";

function App() {
  const [tab, setTab] = useState<Tab>("grids");
  const [gridsVersion, setGridsVersion] = useState(0);

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
          <button className={tab === "hole-config" ? "active" : ""} onClick={() => setTab("hole-config")}>
            Hole Config
          </button>
        </nav>
      </header>

      <div className="app-body" style={{ display: tab === "grids" ? "contents" : "none" }}>
        <GridDesigner onGridsChanged={() => setGridsVersion((v) => v + 1)} />
      </div>
      <div className="app-body" style={{ display: tab === "levels" ? "contents" : "none" }}>
        <LevelEditor
          gridsVersion={gridsVersion}
          onGridsChanged={() => setGridsVersion((v) => v + 1)}
        />
      </div>
      <div className="app-body" style={{ display: tab === "hole-config" ? "contents" : "none" }}>
        <HoleConfigPanel />
      </div>
    </div>
  );
}

export default App;
