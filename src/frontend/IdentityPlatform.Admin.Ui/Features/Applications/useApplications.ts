import { flushSync } from "react-dom";
import { useEffect, useRef, useState } from "react";
import { ApplicationController } from "./controller";
import { readApplications } from "./client";
import type { ApplicationState, Query } from "./observation";

/** Binds list observations to the mounted page and clears them before browser history snapshots. */
export function useApplications(retired = false) {
  const [state, setState] = useState<ApplicationState>({ kind: "loading" });
  const owner = useRef<ApplicationController | null>(null);
  const query = useRef<Query>({ search: "", page: 1, pageSize: 25 });
  useEffect(() => {
    const controller = new ApplicationController((query, signal) => readApplications(query, signal, retired), setState);
    owner.current = controller;
    const hide = () => { flushSync(() => controller.suspend()); };
    const show = (event: PageTransitionEvent) => { if (event.persisted) void controller.check(query.current); else controller.checkDeadline(); };
    const visible = () => controller.checkDeadline();
    window.addEventListener("pagehide", hide); window.addEventListener("pageshow", show);
    document.addEventListener("visibilitychange", visible);
    void controller.check(query.current);
    return () => {
      controller.dispose(); owner.current = null;
      window.removeEventListener("pagehide", hide); window.removeEventListener("pageshow", show);
      document.removeEventListener("visibilitychange", visible);
    };
  }, [retired]);
  return { state, check: (next?: Query) => { if (next) query.current = next; void owner.current?.check(query.current); } };
}
