import { BLOCK_COLOR_HEX } from "../colors";
import { BLOCK_COLORS } from "../types";
import type { BlockColor, GatePlacement } from "../types";

interface GateInspectorProps {
  gate: GatePlacement;
  /** The colour the toolbar is set to, used by the Add button. */
  selectedColor: BlockColor;
  onQueueChange: (cats: BlockColor[]) => void;
  onDelete: () => void;
  onDeselect: () => void;
}

/**
 * The queue editor. Order is the whole point of a gate, so it is shown as a
 * numbered list — top of the list is the topmost cat, the one that leaves on
 * the next matching hole — rather than as a colour count that would hide it.
 */
export function GateInspector({ gate, selectedColor, onQueueChange, onDelete, onDeselect }: GateInspectorProps) {
  const cats = gate.cats;

  function move(index: number, delta: number) {
    const target = index + delta;
    if (target < 0 || target >= cats.length) return;
    const next = [...cats];
    [next[index], next[target]] = [next[target], next[index]];
    onQueueChange(next);
  }

  return (
    <section className="inspector">
      <header className="inspector-header">
        <h3>Selected Gate</h3>
        <span className="muted">
          ({gate.x}, {gate.z}) · {gate.side}
        </span>
      </header>

      <label className="color-field">
        Add a cat
        <div className="color-picker">
          {BLOCK_COLORS.map((c) => (
            <button
              key={c}
              type="button"
              title={`Add ${c} to the back of the queue`}
              aria-label={`Add ${c}`}
              className="color-chip"
              style={{ background: BLOCK_COLOR_HEX[c] }}
              onClick={() => onQueueChange([...cats, c])}
            />
          ))}
        </div>
      </label>

      <div className="gate-queue-header">
        <span>
          Queue · {cats.length} cat{cats.length === 1 ? "" : "s"}
        </span>
        {cats.length > 0 && (
          <button className="danger-link" onClick={() => onQueueChange([])} title="Empty the queue">
            clear
          </button>
        )}
      </div>

      {cats.length === 0 ? (
        <p className="hint inspector-hint">
          Empty. Pick a colour above to queue a cat — or use the toolbar colour and click the gate again to push a{" "}
          {selectedColor} on.
        </p>
      ) : (
        <ol className="gate-queue">
          {cats.map((color, i) => (
            <li key={`${color}-${i}`} className={i === 0 ? "next" : ""}>
              <span className="gate-queue-index">{i === 0 ? "next" : i + 1}</span>
              <span className="gate-queue-swatch" style={{ background: BLOCK_COLOR_HEX[color] }} />
              <span className="gate-queue-name">{color}</span>
              <span className="gate-queue-actions">
                <button onClick={() => move(i, -1)} disabled={i === 0} title="Move up">
                  ↑
                </button>
                <button onClick={() => move(i, 1)} disabled={i === cats.length - 1} title="Move down">
                  ↓
                </button>
                <button
                  className="danger-link"
                  onClick={() => onQueueChange(cats.filter((_, at) => at !== i))}
                  title="Remove this cat"
                >
                  ×
                </button>
              </span>
            </li>
          ))}
        </ol>
      )}

      <p className="hint inspector-hint">
        Park a hole on ({gate.x}, {gate.z}) in play; if the top cat matches that hole's colour it hops in.
      </p>

      <div className="inspector-actions">
        <button onClick={onDeselect}>Deselect</button>
        <button className="destructive" onClick={onDelete}>
          Delete Gate
        </button>
      </div>
    </section>
  );
}
