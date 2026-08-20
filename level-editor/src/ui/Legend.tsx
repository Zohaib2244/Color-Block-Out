import { getCanvasPalette } from "../canvasPalette";
import { useTheme } from "../theme";
import { GridCanvas } from "./GridCanvas";
import { drawCat, drawHoleGroup } from "./drawShapes";

/** A flat color chip — for grid states whose meaning is just "this fill color". */
export function SwatchLegend({ items }: { items: { color: string; label: string }[] }) {
  return (
    <ul className="legend">
      {items.map((item) => (
        <li key={item.label}>
          <span className="legend-swatch" style={{ background: item.color }} />
          {item.label}
        </li>
      ))}
    </ul>
  );
}

/**
 * Draws the real cat/hole glyphs through the same draw functions the editor
 * canvas uses, so the legend can never drift from what's actually rendered.
 */
export function PieceLegend({ accentHex }: { accentHex: string }) {
  const palette = getCanvasPalette(useTheme());
  return (
    <ul className="legend">
      <li>
        <span className="legend-glyph">
          <GridCanvas
            width={1}
            length={1}
            cellPx={30}
            showLabels={false}
            renderCell={(ctx, _cell, rect) => {
              ctx.fillStyle = palette.floor;
              ctx.fillRect(rect.x, rect.y, rect.width, rect.height);
            }}
            renderOverlay={(ctx, cellRect) => drawCat(ctx, cellRect(0, 0), accentHex, palette.holeVoid)}
          />
        </span>
        Cat — solid, with ears
      </li>
      <li>
        <span className="legend-glyph">
          <GridCanvas
            width={2}
            length={1}
            cellPx={30}
            showLabels={false}
            renderCell={(ctx, _cell, rect) => {
              ctx.fillStyle = palette.floor;
              ctx.fillRect(rect.x, rect.y, rect.width, rect.height);
            }}
            renderOverlay={(ctx, cellRect) =>
              drawHoleGroup(
                ctx,
                [
                  { x: 0, z: 0 },
                  { x: 1, z: 0 },
                ],
                cellRect,
                accentHex,
                palette.holeVoid,
                3
              )
            }
          />
        </span>
        Hole — one outline per hole, however many cells; the number is its cat capacity
      </li>
    </ul>
  );
}
