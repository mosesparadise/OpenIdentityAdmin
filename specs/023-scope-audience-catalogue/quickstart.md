# Scope/audience catalogue validation

Catalogue management is delivered on `main` in [PR128](https://github.com/mosesparadise/OpenIdentityAdmin/pull/128).
See [accepted scope and verification limits](acceptance.md). Use the ordinary Aspire launch model:

```sh
dotnet run --project src/aspire/IdentityPlatform.AppHost --launch-profile local-demo
```

Use disposable synthetic data, the dashboard initialization link and genuine password/MFA.
Open the administrator UI from the Aspire dashboard. The workspace includes **Audiences**
and **Scopes** to the navigation:

1. Create an audience with resource `orders-api`, a display name and owner.
2. Create an API scope named `orders.read`, choose that audience and enter its consent
   description. An identity scope can select the finite `email` disclosure policy.
3. Open and edit either entry. Stale versions require reloading before another explicit
   submission. Scope audience selection remains visible when search results change.
4. Try deleting the referenced audience; the operation must report a reference conflict.
5. Explicitly confirm scope deletion and follow its durable job status. After completion,
   delete the now-unreferenced audience. Status remains readable after the target is gone.

Writes require the corresponding current permission and fresh MFA. Read access does not
renew MFA. A lost response requires checking the existing job or explicitly recovering
the original request; the UI does not blindly repeat a write. A changed or expired browser
session clears observed data and requires sign-in again.

Application scope assignment, consent integration, token claim emission and UserInfo are
later tasks T008–T014. Creating a catalogue entry alone does not grant an application scope
permission or change token contents. Do not manually alter the database to bypass these
boundaries. Administrator bearer tokens stay server-side.

Both providers and retained Chromium/WebKit journeys remain required. Actual commands,
source bindings, failures and acceptance limits are recorded in [verification.md](verification.md).
Windows/manual/Firefox/A-009 and existing external gates remain open.
