import type { BlockColor } from "./types";

// Pulled from the real _BaseColor values in
// Assets/Resources/Materials/BlockColors/*.mat so the web tool's palette
// actually matches the game.
export const BLOCK_COLOR_HEX: Record<BlockColor, string> = {
  Red: "#ff544e",
  Orange: "#fd8c16",
  Yellow: "#e0c03a",
  Blue: "#187dec",
  Cyan: "#95d3fd",
  Green: "#5eca38",
  Purple: "#ac4ad5",
  Pink: "#ff4e8d",
  Teal: "#00b985",
};
