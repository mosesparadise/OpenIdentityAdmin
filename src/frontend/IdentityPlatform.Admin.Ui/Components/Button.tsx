import { Slot } from "@radix-ui/react-slot";
import type { ComponentProps } from "react";

/** A shadcn-style native button primitive; Slot preserves link semantics without nested interactive controls. */
export function Button({ asChild = false, secondary = false, className = "", ...props }:
  ComponentProps<"button"> & { asChild?: boolean; secondary?: boolean }) {
  const Component = asChild ? Slot : "button";
  return <Component className={`button ${secondary ? "button-secondary" : "button-primary"} ${className}`} {...props} />;
}
