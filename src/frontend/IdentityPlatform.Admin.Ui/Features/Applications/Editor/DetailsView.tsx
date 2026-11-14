import type { Ref } from "react";
import { Button } from "../../../Components/Button";
import type { Details } from "./observation";
/** Shows only checked registration metadata; callbacks remain inert text rather than external navigation. */
export function DetailsView({ details, secret, dismiss, edit, replace, lifecycle, lifecycleRef }: { details: Details; secret?: string; dismiss: () => void; edit?: () => void; replace?: () => void; lifecycle?: () => void; lifecycleRef?: Ref<HTMLButtonElement> }) {
  return <>
    {secret !== undefined && <section className="editor-secret" aria-labelledby="secret-heading"><h3 id="secret-heading">Save your client secret now</h3>
      <p>Store it in your application’s secret store. This is the only time it is shown. Switching tabs, hiding this page or session expiry clears this view.</p>
      <label htmlFor="created-client-secret">Client secret</label><textarea id="created-client-secret" value={secret} readOnly rows={2} spellCheck={false} autoComplete="off" aria-describedby="secret-help" />
      <p id="secret-help">Select and copy the value using your browser. Copies you make elsewhere are not removed when you hide it here.</p>
      <Button type="button" secondary onClick={dismiss}>I’ve saved it — hide secret</Button></section>}
    <dl className="application-detail-grid"><div><dt>Display name</dt><dd>{details.displayName || "Unnamed application"}</dd></div>
      <div><dt>Client ID</dt><dd className="application-client-id">{details.clientId}</dd></div>
      <div><dt>Registration</dt><dd>{details.editable ? "Managed confidential web application" : "Read-only registration"}</dd></div>
      {details.editable && <><div><dt>Application status</dt><dd>{details.enabled ? "Enabled" : "Disabled"}</dd></div><div><dt>Sign-in profile</dt><dd>Authorization code with PKCE S256 and a shared client secret. OpenID sign-in with explicit consent for assigned scopes.</dd></div>
        <div><dt>User authentication</dt><dd>A fresh password, MFA and explicit consent are required. No administrator permissions are granted to this client.</dd></div></>}
      <div><dt>HTTPS callbacks</dt><dd>{details.redirectUris.length ? <ul>{details.redirectUris.map(uri => <li key={uri}>{uri}</li>)}</ul> : "Not exposed for this read-only registration."}</dd></div>
      {details.credential && <><div><dt>Secret issued (UTC)</dt><dd><time dateTime={details.credential.issuedAt}>{details.credential.issuedAt}</time></dd></div>
        <div><dt>Secret expires (UTC)</dt><dd><time dateTime={details.credential.expiresAt}>{details.credential.expiresAt}</time>{Date.parse(details.credential.expiresAt) <= Date.now() && " — expired"}</dd></div></>}
    </dl>
    <p className="editor-help">{details.editable ? "Client ID cannot be changed. Details never return a previously issued secret." : "System-owned and unmanaged registrations cannot be changed through this editor."}</p>
    {details.editable && <div className="actions"><Button onClick={edit}>Edit application</Button><Button secondary onClick={replace}>Replace secret</Button><Button secondary ref={lifecycleRef} onClick={lifecycle}>{details.enabled ? "Disable application" : "Re-enable application"}</Button></div>}
  </>;
}
