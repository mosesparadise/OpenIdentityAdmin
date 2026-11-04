/** Connects real document events to synchronous view erasure; returning never replays a creation request. */
export function bindLifecycle(owner: { suspend(): void; checkDeadline(): void }, page: Window, document: Document, synchronize: (action: () => void) => void): () => void {
  const hide = () => synchronize(() => owner.suspend());
  const visible = () => document.visibilityState === "hidden" ? hide() : owner.checkDeadline();
  const show = () => owner.checkDeadline();
  page.addEventListener("pagehide", hide); page.addEventListener("pageshow", show); document.addEventListener("visibilitychange", visible);
  return () => { page.removeEventListener("pagehide", hide); page.removeEventListener("pageshow", show); document.removeEventListener("visibilitychange", visible); };
}
