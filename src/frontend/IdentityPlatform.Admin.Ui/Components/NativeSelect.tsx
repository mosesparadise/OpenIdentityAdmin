import type { ComponentProps } from "react";

/** Retains native selection and keyboard semantics with a bounded, theme-aware disclosure arrow. */
export function NativeSelect(props: ComponentProps<"select">) {
  return <span className="native-select"><select {...props} /><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" focusable="false"><path d="m6 9 6 6 6-6" /></svg></span>;
}
