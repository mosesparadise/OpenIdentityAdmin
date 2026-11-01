/** Restores a usable list target even after session expiry disables creation. */
export function focusList(button: Pick<HTMLButtonElement, "disabled" | "focus"> | null, heading: Pick<HTMLElement, "focus"> | null): void {
  if (button && !button.disabled) button.focus();
  else heading?.focus();
}
