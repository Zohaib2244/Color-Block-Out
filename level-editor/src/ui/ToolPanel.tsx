import { CatIcon, CursorIcon, GateIcon, HoleIcon, StackIcon } from "./icons";
import type { CatTool, HoleTool, ToolFamily } from "./tools";

const CAT_TOOLS: { id: CatTool; label: string; note: string }[] = [
  { id: "cat", label: "Normal", note: "one cat per click, drag to lay a run" },
  { id: "stack", label: "Stack", note: "pick a cell, then compose its pile" },
  { id: "gate", label: "Gate", note: "pick a wall, then compose its queue" },
];

const HOLE_TOOLS: { id: HoleTool; label: string; note: string }[] = [
  { id: "hole-shape", label: "Shape", note: "drag an outline, then commit it" },
  { id: "hole-preset", label: "Preset", note: "stamp a saved hole shape" },
];

interface ToolPanelProps {
  family: ToolFamily;
  catTool: CatTool;
  holeTool: HoleTool;
  onFamily: (family: ToolFamily) => void;
  onCatTool: (tool: CatTool) => void;
  onHoleTool: (tool: HoleTool) => void;
}

/**
 * Picks what the next click on the grid will do, in the order the question is
 * actually asked: what kind of thing (a cat, a hole, or nothing — just select),
 * and only then which flavour of it. Flattening all seven into one row of
 * buttons, as this used to be, made "Cats" and "Holes (preset)" look like peers
 * when one is a category and the other is a variant.
 */
export function ToolPanel({ family, catTool, holeTool, onFamily, onCatTool, onHoleTool }: ToolPanelProps) {
  const variants = family === "cat" ? CAT_TOOLS : family === "hole" ? HOLE_TOOLS : [];
  const active: string = family === "cat" ? catTool : holeTool;
  const onVariant = (id: string) => (family === "cat" ? onCatTool(id as CatTool) : onHoleTool(id as HoleTool));

  return (
    <section className="tool-panel">
      <div className="tool-family">
        <span className="tool-panel-label">Add</span>
        <button className={family === "cat" ? "tool-chip active" : "tool-chip"} onClick={() => onFamily("cat")}>
          <CatIcon />
          Cat
        </button>
        <button className={family === "hole" ? "tool-chip active" : "tool-chip"} onClick={() => onFamily("hole")}>
          <HoleIcon />
          Hole
        </button>
        <span className="tool-panel-divider" />
        <button className={family === "select" ? "tool-chip active" : "tool-chip"} onClick={() => onFamily("select")}>
          <CursorIcon />
          Select
        </button>
      </div>

      {variants.length > 0 && (
        <div className="tool-variants">
          <span className="tool-panel-label">{family === "cat" ? "Cat type" : "Hole type"}</span>
          {variants.map((variant) => (
            <button
              key={variant.id}
              className={active === variant.id ? "tool-variant active" : "tool-variant"}
              onClick={() => onVariant(variant.id)}
              title={variant.note}
            >
              {family === "cat" && variant.id === "cat" && <CatIcon />}
              {family === "cat" && variant.id === "stack" && <StackIcon />}
              {family === "cat" && variant.id === "gate" && <GateIcon />}
              <span className="tool-variant-body">
                <strong>{variant.label}</strong>
                <em>{variant.note}</em>
              </span>
            </button>
          ))}
        </div>
      )}
    </section>
  );
}
