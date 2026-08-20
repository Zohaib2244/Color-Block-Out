import { BLOCK_COLOR_HEX } from "../colors";
import { BLOCK_COLORS } from "../types";
import type { BlockColor, HolePlacement } from "../types";

interface HoleInspectorProps {
  hole: HolePlacement;
  onColorChange: (color: BlockColor) => void;
  onCapacityChange: (capacity: number) => void;
  onDelete: () => void;
  onDeselect: () => void;
}

export function HoleInspector({ hole, onColorChange, onCapacityChange, onDelete, onDeselect }: HoleInspectorProps) {
  return (
    <section className="inspector">
      <header className="inspector-header">
        <h3>Selected Hole</h3>
        <span className="muted">
          {hole.cells.length} cell{hole.cells.length === 1 ? "" : "s"}
        </span>
      </header>

      <label className="color-field">
        Color
        <div className="color-picker">
          {BLOCK_COLORS.map((c) => (
            <button
              key={c}
              type="button"
              title={c}
              aria-label={c}
              aria-pressed={hole.color === c}
              className={hole.color === c ? "color-chip active" : "color-chip"}
              style={{ background: BLOCK_COLOR_HEX[c] }}
              onClick={() => onColorChange(c)}
            />
          ))}
        </div>
      </label>

      <label>
        Cat Capacity
        <input
          type="number"
          min={1}
          max={99}
          value={hole.capacity}
          onChange={(e) => onCapacityChange(Math.max(1, Math.floor(Number(e.target.value)) || 1))}
        />
      </label>

      <p className="hint inspector-hint">
        Click an empty cell touching this hole to grow it, or one of its own cells to carve that cell away.
      </p>

      <div className="inspector-actions">
        <button onClick={onDeselect}>Deselect</button>
        <button className="destructive" onClick={onDelete}>
          Delete Hole
        </button>
      </div>
    </section>
  );
}
