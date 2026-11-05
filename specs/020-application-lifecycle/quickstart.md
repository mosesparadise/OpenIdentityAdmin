# Application lifecycle walkthrough

## Available now on main

Creation, details, editing and explicit secret replacement were delivered in PR120. PR122
adds managed application Disable/Re-enable. Use Windows
with Docker Desktop in Linux-container mode and a current .NET 10 SDK. From the repository:

```sh
dotnet tool restore
dotnet restore --locked-mode
dotnet dev-certs https --trust
dotnet run --project src/aspire/IdentityPlatform.AppHost --launch-profile local-demo
```

Use the Aspire dashboard initialization flow with synthetic data, enroll administrator MFA and
open `https://bff.dev.localhost:17444/applications`. The demo is disposable per run. No custom
PowerShell launcher or separate frontend server is required. Full setup and existing walkthrough:
[feature019 quickstart](../019-application-editor/quickstart.md).

## Lifecycle walkthrough from main

Disable/Re-enable controls are delivered on main through PR122. Use the same Aspire command
above and disposable data to exercise the following flow. Disabling prevents new issuance; it
does not erase existing offline tokens or sessions in other applications, and existing grants
are not bulk-revoked:

1. Create a managed confidential web application and prove its fresh-password/MFA/consent sign-in.
2. Open its details, record status/expiry, select Disable, choose a reason and confirm.
3. Verify Disabled and a new version. Attempt a fresh issuer journey and a previously admitted
   request/code through a separate issuer instance; both must fail.
4. Re-enable with a fresh version and current lifecycle authority. A new journey must succeed
   while pre-transition artifacts remain rejected.
5. Repeat with an expired credential; re-enable must not renew it. Use the separate explicit
   credential-replacement action to repair it while preserving lifecycle state.
6. Exercise a stale screen and ambiguous response; reload deliberately and inspect state/audit.
   No automatic repeat mutation should occur.

Run the acceptance journey on both real providers and retained Chromium/WebKit configurations.
Automated checks do not replace an actual Windows Docker Desktop walkthrough or qualified manual
accessibility review. Firefox, precise A-009 certificate deferrals and all external release gates
remain open. Hosted Chromium/WebKit and both native provider demos pass in the accepted run.
No local browser or actual Windows walkthrough is claimed: the pinned local browser executable
was unavailable.
See [current verification](verification.md) for the tested scope and remaining gates.
