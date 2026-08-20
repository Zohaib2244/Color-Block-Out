export const ZOOM_MIN = 0.5;
export const ZOOM_MAX = 2.5;

/**
 * Zoom is a multiplier over each screen's auto-fit cell size rather than an
 * absolute pixel value, so resizing a grid still fits on screen while the
 * user's zoom preference sticks.
 */
export function ZoomControl({ value, onChange }: { value: number; onChange: (value: number) => void }) {
  return (
    <label className="zoom-control">
      Zoom
      <div className="zoom-control-row">
        <input
          type="range"
          min={ZOOM_MIN}
          max={ZOOM_MAX}
          step={0.1}
          value={value}
          onChange={(e) => onChange(Number(e.target.value))}
        />
        <span className="zoom-value">{value.toFixed(1)}×</span>
      </div>
    </label>
  );
}

export function zoomedCellPx(width: number, length: number, zoom: number, fitPx = 640): number {
  const autoFit = Math.max(14, Math.min(40, Math.floor(fitPx / Math.max(width, length))));
  return Math.max(8, Math.min(96, Math.round(autoFit * zoom)));
}
