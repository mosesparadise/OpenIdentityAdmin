import { useEffect, useState } from "react";
import { Icon } from "../../Components/Icon";
const key = "identity-theme";
const choices = ["system", "light", "dark"] as const;
type Theme = typeof choices[number];
/** Reads only the shared allowlisted visual preference; denied storage never prevents navigation. */
function preference(): Theme {
  try { const value = localStorage.getItem(key); return choices.includes(value as Theme) ? value as Theme : "system"; }
  catch { return "system"; }
}
/** Applies a non-sensitive preference without sharing cookies or authentication state across origins. */
function apply(value: Theme): void {
  if (value === "system") document.documentElement.removeAttribute("data-theme");
  else document.documentElement.dataset.theme = value;
}
/** Owns the React-mounted native control; the shared early script supplies first-paint palette only. */
export function ThemeControl() {
  const [theme, setTheme] = useState(preference);
  useEffect(() => {
    const changed = () => { const next = preference(); setTheme(next); apply(next); };
    window.addEventListener("storage", changed);
    return () => window.removeEventListener("storage", changed);
  }, []);
  return <label className="theme-control"><Icon name="sun" /><span className="sr-only">Appearance</span>
    <select aria-label="Appearance" value={theme} onChange={event => {
      const value = event.target.value as Theme;
      if (!choices.includes(value)) return;
      apply(value); setTheme(value);
      try { localStorage.setItem(key, value); } catch { /* Preference storage is optional. */ }
    }}><option value="system">System</option><option value="light">Light</option><option value="dark">Dark</option></select>
  </label>;
}
