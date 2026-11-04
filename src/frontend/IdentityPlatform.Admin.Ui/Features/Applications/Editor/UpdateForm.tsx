import { useEffect, useRef, useState } from "react";
import { Button } from "../../../Components/Button";
import { CreateFields } from "./CreateForm";
import { emptyDraft, type Draft, type Errors, type EditDraft, type ReplacementDraft } from "./input";
import type { Details } from "./observation";
/** Separates presentation editing from explicit credential replacement; identity remains immutable. */
export function UpdateForm({ details, action, edit, replace, cancel }: { details: Details; action: "edit" | "replace";
  edit: (draft: EditDraft) => Promise<Errors>; replace: (draft: ReplacementDraft) => Promise<Errors>; cancel: () => void }) {
  const [draft, setDraft] = useState<Draft>({ ...emptyDraft, displayName: details.displayName!, redirectUris: details.redirectUris.join("\n") });
  const [confirmed, setConfirmed] = useState(false);
  const [errors, setErrors] = useState<Errors>({});
  const summary = useRef<HTMLDivElement>(null);
  useEffect(() => { if (Object.keys(errors).length) summary.current?.focus(); }, [errors]);
  return <form className="application-editor-form" noValidate autoComplete="off" onSubmit={async event => {
    event.preventDefault(); setErrors(await (action === "edit" ? edit({ displayName: draft.displayName, redirectUris: draft.redirectUris })
      : replace({ confirmed, credentialExpiresAt: draft.credentialExpiresAt })));
  }}>
    <p>Client ID (cannot be changed): <strong className="application-client-id">{details.clientId}</strong></p>
    <CreateFields draft={draft} errors={errors} setDraft={setDraft} summary={summary} mode={action} />
    {action === "replace" && <div className="editor-notice"><h3>The current secret will stop working immediately</h3>
      <p>Replacement requires current credential permission and a recent MFA sign-in. Save the replacement secret when shown and update your application’s secret store. A lost secret cannot be retrieved. Pending sign-ins must start again.</p>
      <label className="editor-confirmation" htmlFor="editor-confirmed"><input id="editor-confirmed" type="checkbox" checked={confirmed}
        onChange={event => setConfirmed(event.target.checked)} aria-invalid={!!errors.confirmed} aria-describedby={errors.confirmed ? "editor-confirmed-error" : undefined} required />
        I confirm that the current secret for {details.clientId} will stop working immediately.</label>
      {errors.confirmed && <p className="field-error" id="editor-confirmed-error">{errors.confirmed}</p>}
    </div>}
    <div className="actions"><Button type="submit">{action === "edit" ? "Save changes" : "Replace secret"}</Button><Button type="button" secondary onClick={cancel}>Cancel</Button></div>
  </form>;
}
