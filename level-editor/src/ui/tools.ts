export type CatTool = "cat" | "stack" | "gate";
export type HoleTool = "hole-shape" | "hole-preset";
export type ToolFamily = "cat" | "hole" | "select";
export type Tool = CatTool | HoleTool | "select";

/**
 * The tool is stored as a family plus the last variant chosen within each
 * family, rather than as one flat value, so that switching Cat -> Hole -> Cat
 * lands back on the cat tool you were actually using.
 */
export function toolFor(family: ToolFamily, catTool: CatTool, holeTool: HoleTool): Tool {
  if (family === "select") return "select";
  return family === "cat" ? catTool : holeTool;
}
