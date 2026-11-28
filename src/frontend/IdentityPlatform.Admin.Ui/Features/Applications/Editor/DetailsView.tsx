import type { Ref } from "react";
import { Button } from "../../../Components/Button";
import { machineProfile, keyedMachineProfile, browserProfile, nativeProfile } from "./input";
import type { Details } from "./observation";
/** Shows only checked registration metadata; callbacks remain inert text rather than external navigation. */
export function DetailsView({ details, secret, dismiss, edit, replace, clone, cloneRef, lifecycle, lifecycleRef, retire, retireRef }: { details: Details; retire?: () => void; retireRef?: Ref<HTMLButtonElement>; secret?: string; dismiss: () => void; edit?: () => void; replace?: () => void; clone?: () => void; cloneRef?: Ref<HTMLButtonElement>; lifecycle?: () => void; lifecycleRef?: Ref<HTMLButtonElement> }) {
  const native = details.profile === nativeProfile;
  const keyed = details.profile === keyedMachineProfile;
  const machine = details.profile === machineProfile || keyed;
  const browser = details.profile === browserProfile;
  const publicClient = native || browser;
  return <>
    {secret !== undefined && <section className="editor-secret" aria-labelledby="secret-heading"><h3 id="secret-heading">Save your client secret now</h3>
      <p>Store it in your application’s secret store. This is the only time it is shown. Switching tabs, hiding this page or session expiry clears this view.</p>
      <label htmlFor="created-client-secret">Client secret</label><textarea id="created-client-secret" value={secret} readOnly rows={2} spellCheck={false} autoComplete="off" aria-describedby="secret-help" />
      <p id="secret-help">Select and copy the value using your browser. Copies you make elsewhere are not removed when you hide it here.</p>
      <Button type="button" secondary onClick={dismiss}>I’ve saved it — hide secret</Button></section>}
    <dl className="application-detail-grid"><div><dt>Display name</dt><dd>{details.displayName || "Unnamed application"}</dd></div>
      <div><dt>Client ID</dt><dd className="application-client-id">{details.clientId}</dd></div>
      <div><dt>Registration</dt><dd>{details.retired ? "Permanently retired managed application" : machine ? "Managed machine client" : browser ? "Managed public browser application" : native ? "Managed public native application" : details.editable ? "Managed confidential web application" : "Read-only registration"}</dd></div>
      {(details.editable || details.retired) && <><div><dt>Application status</dt><dd>{details.retired ? "Permanently retired" : details.enabled ? "Enabled" : "Disabled"}</dd></div><div><dt>Sign-in profile</dt><dd>{machine ? "Client credentials for assigned API scopes." : <>Authorization code with PKCE S256{browser ? " and registered browser origins" : native ? " and an OS-associated HTTPS callback" : " and a shared client secret"}. OpenID sign-in with explicit consent for assigned scopes.</>}</dd></div>
        {!machine && <div><dt>User authentication</dt><dd>A fresh password, MFA and explicit consent are required. No administrator permissions are granted to this client.</dd></div>}</>}
      {!machine && <div><dt>HTTPS callbacks</dt><dd>{details.redirectUris.length ? <ul>{details.redirectUris.map(uri => <li key={uri}>{uri}</li>)}</ul> : "Not exposed for this read-only registration."}</dd></div>}
      {browser && <div><dt>Allowed browser origins</dt><dd><ul>{details.origins?.map(origin => <li key={origin}>{origin}</li>)}</ul></dd></div>}
      {details.credential && <><div><dt>{keyed ? "Key registered" : "Secret issued"} (UTC)</dt><dd><time dateTime={details.credential.issuedAt}>{details.credential.issuedAt}</time></dd></div>
        <div><dt>{keyed ? "Key" : "Secret"} expires (UTC)</dt><dd><time dateTime={details.credential.expiresAt}>{details.credential.expiresAt}</time>{Date.parse(details.credential.expiresAt) <= Date.now() && " — expired"}</dd></div></>}
    </dl>
    {details.editable && <details className="editor-help"><summary>Client integration settings</summary>
      {machine ? <><p>Assign an API scope to this service, then authenticate at the discovered token endpoint using these settings.</p>
      <pre className="integration-example">{JSON.stringify({ client_id: details.clientId, grant_type: "client_credentials",
        ...(keyed ? { token_endpoint_auth_method: "private_key_jwt", token_endpoint_auth_signing_alg: "RS256" } :
          { token_endpoint_auth_method: "client_secret_basic", client_secret: "<read from your server-side secret store>" }) }, null, 2)}</pre></> : <><p>Use your deployment’s issuer address for discovery. Configure the {browser ? "browser" : native ? "native" : "server-side"} OIDC client with
        the following settings and choose one registered callback for each sign-in.</p>
      <pre className="integration-example">{JSON.stringify({
        client_id: details.clientId,
        redirect_uris: details.redirectUris,
        response_types: ["code"],
        token_endpoint_auth_method: publicClient ? "none" : "client_secret_basic",
        code_challenge_method: "S256",
        scope: "openid",
        ...(publicClient ? {} : { client_secret: "<read from your server-side secret store>" })
      }, null, 2)}</pre>
      <p>Generate a fresh PKCE verifier and challenge for each request. Request additional scopes only
        after assignment to this application.</p></>}
    </details>}
    {details.retirement && <p>Retired at <time dateTime={details.retirement.retiredAt}>{details.retirement.retiredAt}</time>. History and the client ID are retained. This application cannot be re-enabled. Tokens validated offline may remain valid until expiry, and sessions in other applications may continue.</p>}
    <p className="editor-help">{details.retired ? "Retained details support auditing and grant-revocation recovery." : details.editable ? "Client ID cannot be changed. Details never return a previously issued secret." : "System-owned and unmanaged registrations cannot be changed through this editor."}</p>
    {details.editable && <div className="actions">{!keyed && <Button onClick={edit}>Edit application</Button>}{!publicClient && <><Button secondary onClick={replace}>{keyed ? "Replace public key" : "Replace secret"}</Button>{!machine && <Button secondary ref={cloneRef} onClick={clone}>Clone application</Button>}</>}<Button secondary ref={lifecycleRef} onClick={lifecycle}>{details.enabled ? "Disable application" : "Re-enable application"}</Button><Button secondary ref={retireRef} onClick={retire}>Retire application</Button></div>}
  </>;
}
