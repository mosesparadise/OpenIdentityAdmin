import { useEffect, useLayoutEffect, useRef, useState } from "react";
import { flushSync } from "react-dom";
import type { SessionState } from "../Session/observation";
import { bindLifecycle } from "../Applications/Editor/lifecycle";
import { CatalogueController, type CatalogueState } from "./controller";
import { catalogueClient } from "./client";
import type { Kind } from "./input";
/** Binds one catalogue owner to current session and native visibility/history events without storing state across page loads. */
export function useCatalogue(kind: Kind, session: SessionState) {
  const [state, setState] = useState<CatalogueState>({ kind: "idle" });
  const activated = useRef<string | undefined>(undefined), owner = useRef<CatalogueController | null>(null), latest = useRef(session); latest.current = session;
  useEffect(() => {
    const current = new CatalogueController(kind, catalogueClient(), setState); owner.current = current; current.authorize(latest.current);
    activated.current = latest.current.kind === "authenticated" ? latest.current.subject : undefined;
    if (activated.current) void current.list();
    const unbind = bindLifecycle(current, window, document, flushSync);
    return () => { unbind(); current.dispose(); owner.current = null; };
  }, [kind]);
  useLayoutEffect(() => {
    owner.current?.authorize(session);
    if (owner.current && session.kind === "authenticated" && activated.current !== session.subject) { activated.current = session.subject; void owner.current.list(); }
    if (session.kind !== "authenticated") activated.current = undefined;
  }, [session]);
  const reload = (search = "", page = 1) => { owner.current?.authorize(latest.current); void owner.current?.list(search, page); };
  return { state, owner, reload };
}
