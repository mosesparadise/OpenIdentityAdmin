import { flushSync } from "react-dom";
import { useEffect, useRef, useState } from "react";
import { SessionController } from "./controller";
import { readSession } from "./client";
import type { SessionState } from "./observation";

/** Keeps native reads bound to the current page and clears observations before history-cache restoration. */
export function useSession() {
  const [state, setState] = useState<SessionState>({ kind: "loading" });
  const owner = useRef<SessionController | null>(null);
  useEffect(() => {
    const controller = new SessionController(readSession, setState);
    owner.current = controller;
    const hide = () => { flushSync(() => controller.suspend()); };
    const show = (event: PageTransitionEvent) => { if (event.persisted) void controller.check(); else controller.checkDeadline(); };
    const visible = () => controller.checkDeadline();
    window.addEventListener("pagehide", hide);
    window.addEventListener("pageshow", show);
    document.addEventListener("visibilitychange", visible);
    void controller.check();
    return () => {
      controller.dispose(); owner.current = null;
      window.removeEventListener("pagehide", hide); window.removeEventListener("pageshow", show);
      document.removeEventListener("visibilitychange", visible);
    };
  }, []);
  return { state, check: () => { void owner.current?.check(); } };
}
