const ICON_PROPS = {
  width: 14,
  height: 14,
  viewBox: "0 0 24 24",
  fill: "none",
  stroke: "currentColor",
  strokeWidth: 1.75,
  strokeLinecap: "round" as const,
  strokeLinejoin: "round" as const,
};

export function GridIcon() {
  return (
    <svg {...ICON_PROPS}>
      <rect x="3" y="3" width="18" height="18" rx="2" />
      <path d="M3 9h18M3 15h18M9 3v18M15 3v18" />
    </svg>
  );
}

export function LayersIcon() {
  return (
    <svg {...ICON_PROPS}>
      <path d="m12 2 9 5-9 5-9-5 9-5Z" />
      <path d="m3 12 9 5 9-5" />
      <path d="m3 17 9 5 9-5" />
    </svg>
  );
}

export function ShapesIcon() {
  return (
    <svg {...ICON_PROPS}>
      <path d="M8.3 10a.7.7 0 0 1-.626-1.079L11.4 3a.7.7 0 0 1 1.198-.043L16.3 8.9a.7.7 0 0 1-.572 1.1Z" />
      <rect x="3" y="14" width="7" height="7" rx="1" />
      <circle cx="17.5" cy="17.5" r="3.5" />
    </svg>
  );
}

export function MinusIcon() {
  return (
    <svg {...ICON_PROPS}>
      <path d="M5 12h14" />
    </svg>
  );
}

export function PlusIcon() {
  return (
    <svg {...ICON_PROPS}>
      <path d="M12 5v14M5 12h14" />
    </svg>
  );
}

/** A cat head — the same silhouette the canvas draws, so the tool matches its output. */
export function CatIcon() {
  return (
    <svg {...ICON_PROPS}>
      <path d="M5 10.5 4.5 4l5 3.2" />
      <path d="m19 10.5.5-6.5-5 3.2" />
      <path d="M12 21a7 7 0 0 1-7-7 7 7 0 0 1 14 0 7 7 0 0 1-7 7Z" />
      <path d="M9.5 13.5h.01M14.5 13.5h.01" />
    </svg>
  );
}

/** Three bands piled up — the cat-stack glyph. */
export function StackIcon() {
  return (
    <svg {...ICON_PROPS}>
      <rect x="4" y="15.5" width="16" height="4.5" rx="2" />
      <rect x="4" y="9.75" width="16" height="4.5" rx="2" />
      <rect x="4" y="4" width="16" height="4.5" rx="2" />
    </svg>
  );
}

/** A barred opening in a wall, with the queue behind it. */
export function GateIcon() {
  return (
    <svg {...ICON_PROPS}>
      <rect x="3" y="14.5" width="18" height="5" rx="2" />
      <circle cx="7.5" cy="9" r="2.5" />
      <path d="M13 9h1.5M17.5 9H19" />
    </svg>
  );
}

/** A ring cut into the floor — the hole glyph. */
export function HoleIcon() {
  return (
    <svg {...ICON_PROPS}>
      <ellipse cx="12" cy="12" rx="9" ry="7" />
      <ellipse cx="12" cy="12" rx="4.5" ry="3.5" />
    </svg>
  );
}

export function FolderIcon() {
  return (
    <svg {...ICON_PROPS}>
      <path d="M3 7.5A1.5 1.5 0 0 1 4.5 6h4l2 2.5h7A1.5 1.5 0 0 1 19 10v7.5a1.5 1.5 0 0 1-1.5 1.5h-13A1.5 1.5 0 0 1 3 17.5Z" />
    </svg>
  );
}

/** Pointer — the select/edit tool. */
export function CursorIcon() {
  return (
    <svg {...ICON_PROPS}>
      <path d="M4.5 3.5 19 11l-6.4 1.9L9.6 19Z" />
    </svg>
  );
}
