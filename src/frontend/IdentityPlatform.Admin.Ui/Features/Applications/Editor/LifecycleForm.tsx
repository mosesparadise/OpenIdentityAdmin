import { useEffect, useRef, useState } from "react";
import { Button } from "../../../Components/Button";
import type { Details } from "./observation";
import type { Errors, LifecycleDraft } from "./input";

/** Explains the bounded effect before one confirmed lifecycle request; reasons contain no free text. */
export function LifecycleForm({ details, enabled, submit, cancel }: { details: Details; enabled: boolean;
  submit: (input: LifecycleDraft) => Promise<Errors>; cancel: () => void }) {
  const [reason, setReason] = useState("maintenance");
  const [confirm, setConfirm] = useState(false);
  const [errors, setErrors] = useState<Errors>({});
  const summary = useRef<HTMLDivElement>(null);
  useEffect(() => { if (Object.keys(errors).length) summary.current?.focus(); }, [errors]);
  return <form className="application-editor-form" noValidate autoComplete="off" onSubmit={async event => {
    event.preventDefault(); setErrors(await submit({ reason, confirm }));
  }}>
    <p>Application: <strong className="application-client-id">{details.clientId}</strong></p>
    <div className="editor-notice">
      <p>{enabled ? "New sign-ins can start again. Old sign-in attempts remain invalid. Re-enabling does not extend the secret’s expiry."
        : "New sign-ins and token issuance will stop. Existing grants are not revoked. Sessions in other applications and already issued tokens validated offline may continue."}</p>
      <p>This action requires current application lifecycle permission and a recent MFA sign-in.</p>
    </div>
    {Object.keys(errors).length > 0 && <div className="field-error" role="alert" tabIndex={-1} ref={summary}>Review the reason and confirmation before continuing.</div>}
    <label htmlFor="lifecycle-reason">Reason</label>
    <select id="lifecycle-reason" value={reason} onChange={event => setReason(event.target.value)} aria-invalid={!!errors.reason}
      aria-describedby={errors.reason ? "lifecycle-reason-error" : undefined}>
      <option value="maintenance">Maintenance</option><option value="security">Security</option><option value="configuration">Configuration</option>
    </select>
    {errors.reason && <p className="field-error" id="lifecycle-reason-error">{errors.reason}</p>}
    <label className="editor-confirmation" htmlFor="lifecycle-confirm"><input id="lifecycle-confirm" type="checkbox" checked={confirm}
      onChange={event => setConfirm(event.target.checked)} aria-invalid={!!errors.confirmed}
      aria-describedby={errors.confirmed ? "lifecycle-confirm-error" : undefined} required />
      I confirm that this application should be {enabled ? "re-enabled" : "disabled"}.</label>
    {errors.confirmed && <p className="field-error" id="lifecycle-confirm-error">{errors.confirmed}</p>}
    <div className="actions"><Button type="submit">{enabled ? "Re-enable application" : "Disable application"}</Button>
      <Button type="button" secondary onClick={cancel}>Cancel</Button></div>
  </form>;
}
