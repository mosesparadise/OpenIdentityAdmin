import { useEffect, useLayoutEffect, useRef, useState } from "react";
import { flushSync } from "react-dom";
import { Button } from "../../../../Components/Button";
import type { Observed } from "../observation";
import { bindLifecycle } from "../lifecycle";
import { grantClient } from "./client";
import { GrantController, type GrantState } from "./controller";

/** Shows explicit, current-permission-gated revocation and truthful committed counts with no percentage or offline-erasure claim. */
export function GrantPanel({ base }: { base: Observed }) {
  const [state, setState] = useState<GrantState>({ kind: "loading" });
  const [confirming, setConfirming] = useState(false);
  const [reason, setReason] = useState("security");
  const [confirm, setConfirm] = useState(false);
  const [invalid, setInvalid] = useState(false);
  const controller = useRef<GrantController | null>(null);
  const heading = useRef<HTMLHeadingElement>(null);
  const action = useRef<HTMLButtonElement>(null);
  const focus = useRef<"action" | "heading" | null>(null);
  useEffect(() => {
    const owner = new GrantController(grantClient(), base, setState); controller.current = owner;
    const unbind = bindLifecycle(owner, window, document, flushSync); void owner.refresh();
    return () => { unbind(); owner.dispose(); controller.current = null; };
  }, [base.details.id, base.etag, base.expires]);
  useEffect(() => { if (confirming || invalid) heading.current?.focus(); }, [confirming, invalid]);
  useLayoutEffect(() => {
    if (!confirming && focus.current) { (focus.current === "action" ? action : heading).current?.focus(); focus.current = null; }
  }, [confirming, state.kind]);
  const job = state.kind === "ready" ? state.jobs.find(value => value.state !== "succeeded") ?? state.jobs[0] : undefined;
  const available = state.kind === "ready" && base.details.enabled === false && (!job || job.state === "succeeded" || job.state === "stopped");
  return <section className="editor-notice" aria-labelledby="grant-heading" aria-busy={state.kind === "loading" || state.kind === "sending"}>
    <h3 id="grant-heading" tabIndex={-1} ref={heading}>Revoke application grants</h3>
    <p>Revocation invalidates stored authorizations and tokens. Tokens validated offline and sessions in other applications may continue.</p>
    <p>The application must stay disabled until revocation succeeds. You can re-enable it separately afterward. Accepted work continues after you sign out while its approving administrator remains eligible.</p>
    <div role="status" aria-live="polite" aria-atomic="true">
      {state.kind === "loading" && <p>Checking current access and committed progress…</p>}
      {state.kind === "sending" && <p>Sending your confirmation once. An interrupted response may leave an accepted job.</p>}
      {state.kind === "cleared" && <p>Progress was cleared. Reload application details to check current status.</p>}
      {job && <><p>Job reference: <span className="application-client-id">{job.id}</span></p><p>Revocation status: <strong>{job.state}</strong></p><p>Authorizations revoked: {job.authorizationCount}. Tokens revoked: {job.tokenCount}.</p>
        {job.state === "stopped" && <p>Work stopped ({job.failure}). Earlier committed progress is preserved. Resolve the cause and sign in with fresh MFA before confirming continuation.</p>}
        {job.state === "succeeded" && <p>Revocation completed. The application remains disabled until you explicitly re-enable it.</p>}</>}
    </div>
    {state.kind === "problem" && <div role="alert"><p>{state.reason === "forbidden" ? "Current grant permissions are required. Confirming revocation or continuation also requires a fresh MFA sign-in." : state.reason === "expired" ? "Sign in again to check current progress." : state.reason === "stale" ? "Application or job details changed. Reload details before confirming again." : "Progress could not be confirmed. Reload details to check current state."}</p>
      {state.uncertain && <><p>Your request may already be accepted. This check sends the same request key and confirmation again; it does not create a different intent.</p><Button secondary onClick={() => { focus.current = "heading"; void controller.current?.recover(); }}>Check acceptance of this request</Button></>}
      <a href="/bff/login">Sign in with fresh MFA</a></div>}
    {state.kind === "ready" && base.details.enabled && <p>Disable the application before requesting grant revocation.</p>}
    {available && !confirming && <Button secondary ref={action} onClick={() => { setConfirm(false); setInvalid(false); setConfirming(true); }}>{job?.state === "stopped" ? "Continue stopped revocation" : "Revoke grants"}</Button>}
    {available && confirming && <form className="application-editor-form" autoComplete="off" onSubmit={async event => {
      event.preventDefault(); if (!confirm) { setInvalid(true); return; }
      const sent = controller.current?.submit(reason, confirm, job?.state === "stopped" ? job.id : undefined);
      focus.current = "heading"; setConfirming(false); setConfirm(false); await sent;
    }}>
      <p>Application: <strong>{base.details.clientId}</strong></p>
      <label htmlFor="grant-reason">Reason</label><select id="grant-reason" value={reason} onChange={event => setReason(event.target.value)}>
        <option value="security">Security</option><option value="maintenance">Maintenance</option><option value="configuration">Configuration</option></select>
      <label className="editor-confirmation" htmlFor="grant-confirm"><input id="grant-confirm" type="checkbox" checked={confirm} onChange={event => setConfirm(event.target.checked)} aria-invalid={invalid} aria-describedby={invalid ? "grant-error" : undefined} />I confirm revocation of this application’s remaining grants.</label>
      {invalid && <p id="grant-error" role="alert">Confirm the destructive action before submitting.</p>}
      <div className="actions"><Button type="submit">Confirm {job?.state === "stopped" ? "continuation" : "revocation"}</Button><Button type="button" secondary onClick={() => { focus.current = "action"; setInvalid(false); setConfirming(false); setConfirm(false); }}>Cancel</Button></div>
    </form>}
  </section>;
}
