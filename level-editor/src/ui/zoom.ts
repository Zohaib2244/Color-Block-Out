export const ZOOM_MIN = 0.4;
export const ZOOM_MAX = 4;

/** One notch of the buttons / one wheel click. Multiplicative, so each step feels equal. */
const ZOOM_FACTOR = 1.15;

export function clampZoom(zoom: number): number {
  return Math.min(ZOOM_MAX, Math.max(ZOOM_MIN, zoom));
}

/** One notch in or out. `direction` is +1 to zoom in, -1 to zoom out. */
export function stepZoom(zoom: number, direction: number): number {
  return clampZoom(direction > 0 ? zoom * ZOOM_FACTOR : zoom / ZOOM_FACTOR);
}

/** Cell size at 1x — the size that fits a `fitPx` box, before the user's zoom is applied. */
export function autoFitCellPx(width: number, length: number, fitPx = 640): number {
  return Math.max(14, Math.min(40, Math.floor(fitPx / Math.max(1, width, length))));
}

export function zoomedCellPx(width: number, length: number, zoom: number, fitPx = 640): number {
  return Math.max(6, Math.min(200, Math.round(autoFitCellPx(width, length, fitPx) * zoom)));
}

/**
 * The zoom that makes a `width` x `length` grid fill a viewport, minus the
 * chrome between the viewport's inner edge and the grid itself.
 *
 * Returned as a zoom multiplier rather than a cell size because zoom is what
 * the user's preference is stored in — see the note on ZoomControl.
 */
export function fitZoom(
  width: number,
  length: number,
  viewportWidth: number,
  viewportHeight: number,
  chromePx: number,
  fitPx = 640
): number {
  if (width < 1 || length < 1 || viewportWidth <= 0 || viewportHeight <= 0) return 1;
  const cell = Math.min((viewportWidth - chromePx) / width, (viewportHeight - chromePx) / length);
  return clampZoom(cell / autoFitCellPx(width, length, fitPx));
}
