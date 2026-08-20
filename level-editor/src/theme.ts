import { useEffect, useState } from "react";

export type Theme = "dark" | "light";

const STORAGE_KEY = "cbo-level-editor:theme";
const CHANGE_EVENT = "cbo-level-editor:theme-change";

export function getTheme(): Theme {
  return document.documentElement.dataset.theme === "light" ? "light" : "dark";
}

export function setTheme(theme: Theme): void {
  document.documentElement.dataset.theme = theme;
  localStorage.setItem(STORAGE_KEY, theme);
  window.dispatchEvent(new Event(CHANGE_EVENT));
}

export function toggleTheme(): void {
  setTheme(getTheme() === "dark" ? "light" : "dark");
}

/** Re-renders whenever the theme changes, so canvas fills (which can't read CSS vars) stay in sync. */
export function useTheme(): Theme {
  const [theme, setLocalTheme] = useState<Theme>(() => getTheme());
  useEffect(() => {
    const onChange = () => setLocalTheme(getTheme());
    window.addEventListener(CHANGE_EVENT, onChange);
    return () => window.removeEventListener(CHANGE_EVENT, onChange);
  }, []);
  return theme;
}
