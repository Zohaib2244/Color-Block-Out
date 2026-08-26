import { getCanvasPalette } from "../canvasPalette";
import { BLOCK_COLOR_HEX } from "../colors";
import { useTheme } from "../theme";
import { BLOCK_COLORS } from "../types";
import type { BlockColor, Direction } from "../types";
import { GridCanvas } from "./GridCanvas";
import { drawCatStack, drawGate } from "./drawShapes";

/** Big enough that every band, pip and badge is legible at a glance. */
const PREVIEW_PX = 108;

export type StackKind = "stack" | "gate";

/**
 * What each kind calls its ordering, and which END of the stored array is the
 * top. The two differ: a cat stack is stored bottom-first (matching Unity's
 * `RestackCats`, which lifts each cat by its list index) while a gate queue is
 * stored top-first (index 0 leaves next). The panel hides that split by always
 * listing top-first on screen and converting on the way in and out.
 */
const COPY: Record<
  StackKind,
  { title: string; topEnd: "start" | "end"; topTag: string; addLabel: string; deleteLabel: string; empty: string; hint: string }
> = {
  stack: {
    title: "Cat Stack",
    topEnd: "end",
    topTag: "top",
    addLabel: "Add to the bottom of the pile",
    deleteLabel: "Clear Cell",
    empty: "Nothing on this cell yet. Pick a colour below — the first one you add becomes the top of the pile.",
    hint: "Reading down the list is reading the pile from its top cat to the floor. A hole can only ever take the top one.",
  },
  gate: {
    title: "Gate Queue",
    topEnd: "start",
    topTag: "next",
    addLabel: "Add to the back of the queue",
    deleteLabel: "Delete Gate",
    empty: "This gate is empty, so it will never release a cat. Pick a colour below to queue the first one.",
    hint: "Park a hole on this gate's cell in play; if the cat at the head of the queue matches that hole's colour, it hops in.",
  },
};

interface StackVisualizerProps {
  kind: StackKind;
  /** Gate only: the edge its bar sits on, so the preview matches the board. */
  side?: Direction;
  /** Shown next to the title — the cell, and for a gate its edge. */
  location: string;
  /** Stored order, whichever way round this kind stores it. */
  cats: BlockColor[];
  /** Receives the new sequence in stored order. */
  onChange: (cats: BlockColor[]) => void;
  onDelete: () => void;
  onDeselect: () => void;
}

/**
 * The sequence editor shared by cat stacks and gate queues.
 *
 * Both are the same thing to a designer — an ordered run of coloured cats where
 * only one end is live — so they get one panel rather than two that drift. It
 * pairs a full-size preview drawn through the very same canvas routines the
 * grid uses (so the panel can never disagree with the board) with a numbered
 * list, because order is the whole point and a colour count would hide it.
 */
export function StackVisualizer({ kind, side, location, cats, onChange, onDelete, onDeselect }: StackVisualizerProps) {
  const copy = COPY[kind];
  const palette = getCanvasPalette(useTheme());

  // Everything below works top-first; only these two lines know which way the
  // stored array runs.
  const display = copy.topEnd === "start" ? cats : [...cats].reverse();
  const emit = (next: BlockColor[]) => onChange(copy.topEnd === "start" ? next : [...next].reverse());

  function move(index: number, delta: number) {
    const target = index + delta;
    if (target < 0 || target >= display.length) return;
    const next = [...display];
    [next[index], next[target]] = [next[target], next[index]];
    emit(next);
  }

  return (
    <section className="inspector stack-visualizer">
      <header className="inspector-header">
        <h3>{copy.title}</h3>
        <span className="muted">{location}</span>
      </header>

      <div className="stack-preview">
        <GridCanvas
          width={1}
          length={1}
          cellPx={PREVIEW_PX}
          showLabels={false}
          renderCell={(ctx, _cell, rect) => {
            ctx.fillStyle = palette.floor;
            ctx.fillRect(rect.x, rect.y, rect.width, rect.height);
          }}
          renderOverlay={(ctx, cellRect) => {
            const hexes = cats.map((c) => BLOCK_COLOR_HEX[c]);
            if (kind === "gate") drawGate(ctx, cellRect(0, 0), side ?? "Down", hexes, palette.textMuted);
            else drawCatStack(ctx, cellRect(0, 0), hexes);
          }}
        />
        <div className="stack-preview-note">
          <strong>{cats.length}</strong>
          <span className="muted"> cat{cats.length === 1 ? "" : "s"}</span>
          <p className="muted">exactly as the grid draws it</p>
        </div>
      </div>

      <label className="color-field">
        {copy.addLabel}
        <div className="color-picker">
          {BLOCK_COLORS.map((c) => (
            <button
              key={c}
              type="button"
              title={`Add ${c}`}
              aria-label={`Add ${c}`}
              className="color-chip"
              style={{ background: BLOCK_COLOR_HEX[c] }}
              onClick={() => emit([...display, c])}
            />
          ))}
        </div>
      </label>

      <div className="stack-list-header">
        <span>Sequence</span>
        {display.length > 1 && (
          <button className="link-action" onClick={() => emit([...display].reverse())} title="Flip the whole order">
            reverse
          </button>
        )}
        {display.length > 0 && (
          <button className="danger-link" onClick={() => emit([])} title="Remove every cat">
            clear
          </button>
        )}
      </div>

      {display.length === 0 ? (
        <p className="hint inspector-hint">{copy.empty}</p>
      ) : (
        <ol className="stack-list">
          {display.map((color, i) => (
            <li key={`${color}-${i}`} className={i === 0 ? "top" : ""}>
              <span className="stack-list-index">{i === 0 ? copy.topTag : i + 1}</span>
              <span className="stack-list-swatch" style={{ background: BLOCK_COLOR_HEX[color] }} />
              <span className="stack-list-name">{color}</span>
              <span className="stack-list-actions">
                <button onClick={() => move(i, -1)} disabled={i === 0} title="Move up" aria-label="Move up">
                  ↑
                </button>
                <button
                  onClick={() => move(i, 1)}
                  disabled={i === display.length - 1}
                  title="Move down"
                  aria-label="Move down"
                >
                  ↓
                </button>
                <button
                  className="danger-link"
                  onClick={() => emit(display.filter((_, at) => at !== i))}
                  title="Remove this cat"
                  aria-label="Remove this cat"
                >
                  ×
                </button>
              </span>
            </li>
          ))}
        </ol>
      )}

      <p className="hint inspector-hint">{copy.hint}</p>

      <div className="inspector-actions">
        <button onClick={onDeselect}>Deselect</button>
        <button className="destructive" onClick={onDelete} disabled={kind === "stack" && cats.length === 0}>
          {copy.deleteLabel}
        </button>
      </div>
    </section>
  );
}
