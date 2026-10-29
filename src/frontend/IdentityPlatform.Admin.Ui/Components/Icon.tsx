import type { ComponentProps } from "react";
const shapes = {
  grid: "M3 3h7v7H3z M14 3h7v7h-7z M3 14h7v7H3z M14 14h7v7h-7z",
  shield: "M12 3 4 6v6c0 5 8 9 8 9s8-4 8-9V6l-8-3Z M8 12l3 3 5-6",
  arrow: "M5 12h14 M13 6l6 6-6 6",
  sun: "M12 3v2 M12 19v2 M3 12h2 M19 12h2 M5.6 5.6 7 7 M17 17l1.4 1.4 M5.6 18.4 7 17 M17 7l1.4-1.4 M16 12a4 4 0 1 1-8 0 4 4 0 0 1 8 0",
  exit: "M9 4H4v16h5 M10 12h10 M16 8l4 4-4 4",
  check: "M5 12l4 4L19 6",
  clock: "M12 7v5l3 2 M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0",
  menu: "M4 6h16 M4 12h16 M4 18h16"
};
/** Locally authored decorative vector; adjacent text supplies every accessible name. */
export function Icon({ name, ...props }: ComponentProps<"svg"> & { name: keyof typeof shapes }) {
  return <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" {...props}><path d={shapes[name]} /></svg>;
}
