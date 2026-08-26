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
