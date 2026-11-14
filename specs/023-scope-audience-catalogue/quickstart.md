# Scope/audience catalogue validation

Catalogue management is delivered on `main` in [PR128](https://github.com/mosesparadise/OpenIdentityAdmin/pull/128).
See [accepted scope and verification limits](acceptance.md). Use the ordinary Aspire launch model:

```sh
dotnet run --project src/aspire/IdentityPlatform.AppHost --launch-profile local-demo
```

Use disposable synthetic data, the dashboard initialization link and genuine password/MFA.
After initialization, open the administrator UI at <https://bff.dev.localhost:17444/>;
dashboard resource links may lead to health endpoints. Follow the
[local demo prerequisites and enrollment steps](../014-local-b3-demo/quickstart.md).
The workspace includes **Audiences** and **Scopes** in the navigation:

1. Create an audience with resource `orders-api`, a display name and owner.
2. Create an API scope named `orders.read`, choose that audience and enter its consent
   description. An identity scope can select the finite `email` disclosure policy.
3. Open and edit either entry. Stale versions require reloading before another explicit
   submission. Scope audience selection remains visible when search results change.
4. Inspect the referenced audience: its Delete control remains disabled and its current
   references explain why deletion is unavailable. Direct API deletion also rejects references.
5. Explicitly confirm scope deletion and follow its durable job status. After completion,
   delete the now-unreferenced audience. Status remains readable after the target is gone.

Writes require the corresponding current permission and fresh MFA. Read access does not
renew MFA. A lost response requires checking the existing job or explicitly recovering
the original request; the UI does not blindly repeat a write. A changed or expired browser
session clears observed data and requires sign-in again.

Application assignment is delivered; see [assignment acceptance](assignment-acceptance.md).
Consented scopes and UserInfo are delivered on main in [PR134](https://github.com/mosesparadise/OpenIdentityAdmin/pull/134).
See [qualified acceptance and reliability limits](consent-acceptance.md). The temporary
assignment hold is removed; the steps below use the ordinary main branch.
Creating a catalogue entry alone does not grant an application scope
permission or change token contents. Do not manually alter the database to bypass these
boundaries. Administrator bearer tokens stay server-side.

Both providers and retained Chromium/WebKit journeys remain required. Actual commands,
source bindings, failures and acceptance limits are recorded in [verification.md](verification.md).
Windows/manual/Firefox/A-009 and existing external gates remain open.

## Try application scope assignment

Use a disposable application and keep an enabled scope/audience from steps 1–2 above.
If you completed the deletion exercise, create another scope and audience first.
In **Applications**, open the application's details and find **Application scopes**.

1. Search for the scope name, select it and read the sign-in warning. Selection remains
   visible when you change the search or page.
2. Confirm replacing the additional scopes, then select **Save scope assignment**.
   Current application write permission and fresh MFA are required. The application
   details reload with the new version and saved assignment.
3. Read the saved consent notice. Assignment permits a client to request those scopes;
   it does not grant them automatically. Each sign-in requires fresh password/MFA and
   explicit consent. Assignment changes invalidate pending sign-ins and codes; start a
   fresh sign-in after a change. Existing grants are not revoked by assignment.
4. Remove scopes, confirm and save again to narrow future eligible requests. An
   `openid`-only request remains possible even when additional scopes are assigned,
   subject to current credential/lifecycle checks.
5. If a save is stale or its result is uncertain, explicitly reload the details and
   inspect the current assignment before deciding to save again. Recovery never
   silently repeats the mutation. Catalogue read permission is needed to browse new
   scopes, but current assignments remain removable without it.

Do not use assignment as grant revocation. Untouched applications keep their existing behavior.

## Try consented tokens and UserInfo

Use an integrating confidential client registered through the existing application editor,
with an exact HTTPS callback, Basic client authentication, S256 PKCE, nonce and state.
Continue using native discovery and signature/issuer/subject/audience validation. The
administrator BFF is not a general-purpose relying party and never sends UserInfo requests.

1. Request `openid orders.read` after explicitly assigning `orders.read`. The issuer
   shows the scope's current name, label, consent description and `orders-api` audience.
   **Allow sign-in** grants the entire displayed eligible request; **Cancel** grants none.
2. Exchange the code once. The native access token has exactly the granted scopes and
   the one mapped API audience. The ID token identifies the client audience. Email,
   administrator roles and management permissions do not appear in the API access token.
3. Call the discovered `/connect/userinfo` over HTTPS with that access token in one
   `Authorization: Bearer` header. GET and POST are supported; POST may instead use one
   form `access_token` field. Do not combine transports or put tokens in query strings.
   The response contains the exact opaque `sub`; compare it to the validated ID token.
4. To request email, create an enabled identity scope with the finite email disclosure,
   assign it explicitly, then request it alongside `openid` in a fresh sign-in. Only after
   consent may the ID token and UserInfo contain current `email` and boolean
   `email_verified`. If the Identity account has no email, both fields are omitted.
   UserInfo reads current account data; the earlier signed ID token does not change.
5. Disable the application or the requested scope, or revoke its grant through the existing
   explicit workflow. UserInfo must stop disclosing the profile. Metadata changes also
   invalidate earlier policy-bound requests/codes and make UserInfo reject the old token;
   begin a new sign-in after correcting policy. Already-issued tokens validated offline
   and sessions in other applications are not erased. No automatic retries or additional
   grant types are enabled.

This batch adds no sample external application or API resource server. The permanent
browser/HTTP tests run real relying-party exchange and independent native-token validation.
The ordinary Aspire administrator UI remains the place to create and assign the catalogue.
