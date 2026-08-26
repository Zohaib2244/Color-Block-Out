import { MinusIcon, PlusIcon } from "./icons";
import { ZOOM_MAX, ZOOM_MIN, clampZoom, stepZoom } from "./zoom";

interface ZoomControlProps {
  value: number;
  onChange: (value: number) => void;
  /** Sizes the grid to the viewport. Omitted on screens with no measurable viewport. */
  onFit?: () => void;
}

/**
 * Zoom is a multiplier over each screen's auto-fit cell size rather than an
 * absolute pixel value, so resizing a grid still fits on screen while the
 * user's zoom preference sticks.
 */
export function ZoomControl({ value, onChange, onFit }: ZoomControlProps) {
  return (
    <div className="zoom-control">
      <button
        type="button"
        className="zoom-step"
        title="Zoom out"
        aria-label="Zoom out"
        disabled={value <= ZOOM_MIN}
        onClick={() => onChange(stepZoom(value, -1))}
      >
        <MinusIcon />
      </button>

      <input
        type="range"
        aria-label="Zoom"
        min={ZOOM_MIN}
        max={ZOOM_MAX}
        step={0.05}
        value={value}
        onChange={(e) => onChange(clampZoom(Number(e.target.value)))}
      />

      <button
        type="button"
        className="zoom-step"
        title="Zoom in"
        aria-label="Zoom in"
        disabled={value >= ZOOM_MAX}
        onClick={() => onChange(stepZoom(value, 1))}
      >
        <PlusIcon />
      </button>

      <span className="zoom-value">{Math.round(value * 100)}%</span>

      {onFit && (
        <button type="button" className="zoom-preset" title="Size the grid to the window" onClick={onFit}>
          Fit
        </button>
      )}
      <button type="button" className="zoom-preset" title="Back to 100%" onClick={() => onChange(1)}>
        100%
      </button>
    </div>
  );
}
