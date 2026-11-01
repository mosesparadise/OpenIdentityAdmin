import { useEffect, useLayoutEffect, useRef, useState } from "react";
import { flushSync } from "react-dom";
import type { SessionState } from "../../Session/observation";
import { EditorController, type EditorState } from "./controller";
import { editorClient } from "./client";
import { bindLifecycle } from "./lifecycle";

/** Owns ephemeral editor state and removes sensitive DOM before browser history capture or document hiding. */
export function useEditor(session: SessionState) {
  const [state, setState] = useState<EditorState>({ kind: "list" });
  const owner = useRef<EditorController | null>(null);
  const latest = useRef(session); latest.current = session;
  useEffect(() => {
    const controller = new EditorController(editorClient(), setState);
    owner.current = controller; controller.authorize(latest.current);
    const unbind = bindLifecycle(controller, window, document, flushSync);
    return () => { unbind(); controller.dispose(); owner.current = null; };
  }, []);
  useLayoutEffect(() => { owner.current?.authorize(session); }, [session]);
  return { state, owner };
}
