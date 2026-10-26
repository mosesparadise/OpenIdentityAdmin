# Disposable B3 demo from Windows

**Candidate implementation: required hosted acceptance and actual Windows Docker Desktop execution are not yet recorded.** See [verification](verification.md). Default startup remains health-only; this profile exposes the accepted administrator browser flows.

## Prerequisites

- Windows with Docker Desktop running **Linux containers**. No WSL terminal is required. SQL Server requires a supported x64 container host; ARM64 suitability is not claimed.
- PowerShell **7.4 or later**, .NET SDK **10.0.401**, and access to NuGet and the pinned Microsoft/container image registries.
- A current .NET development HTTPS certificate trusted by Windows and valid for `*.dev.localhost`. Chrome or Edge can resolve these localhost names without changing the Windows hosts file.
- Available dashboard port 17100, issuer port 17443 and BFF port 17444.

From the repository root in PowerShell:

```powershell
dotnet tool restore
dotnet restore --locked-mode
dotnet dev-certs https --trust
./scripts/local-demo/Start-LocalDemo.ps1 -Provider PostgreSql
```

Select `-Provider SqlServer` for the other existing provider. The script passes the provider identifier through the existing registry; an explicitly registered provider remains selectable. Start checks the SDK, Linux daemon and certificate prerequisites. It never deletes or replaces certificates. If an older certificate lacks the SAN, inspect the development certificates and create a current one deliberately; do not disable browser or backchannel certificate validation.

Keep this terminal open. Image builds can take several minutes on the first run. Open the authenticated Aspire dashboard URL printed locally. Wait for `migrator` to complete successfully, `auth` and `admin-bff` to become healthy, and `initializer` to run. Health confirms schema/runtime availability; it does not mean an administrator has been created.

## Initialize and enroll

In a second PowerShell 7.4+ terminal at the repository root:

```powershell
./scripts/local-demo/Initialize-LocalDemo.ps1
```

The helper requires exactly one running demo initializer, verifies its run label, prompts for a synthetic username/password, and supplies bootstrap JSON only through redirected UTF-8 stdin. Passwords are never placed in argv, configuration, environment or a bootstrap file. Native password policy still applies. It then runs separate public registration and revision-one B3 upgrade operations. No mutation is retried automatically. Do not use real production credentials.

1. Open <https://issuer.dev.localhost:17443/account/administrator/enrollment>.
2. Enter the credentials just initialized, add the displayed setup key to an authenticator, and submit a genuine TOTP.
3. Save the one-time recovery codes privately for this disposable run.
4. Open <https://bff.dev.localhost:17444/bff/login> and start sign-in. Each explicit sign-in requires fresh password and MFA, including when an issuer cookie exists.
5. Inspect <https://bff.dev.localhost:17444/bff/session> for authenticated status. This is a small server-rendered demo; React management and management APIs are not delivered here.
6. Open <https://bff.dev.localhost:17444/bff/logout>. Local sign-out revokes the BFF session. The issuer option first completes local sign-out, then offers a separate issuer confirmation. Returning from the issuer does not independently prove its revocation; the page retains the accepted unverified-return wording.

The dashboard's localhost links are health/resource links. Use the canonical URLs above for account and BFF journeys so origins agree with the registered native protocol identities.

## Stop, restart and troubleshoot

Stop the first terminal with Ctrl+C. The wrapper attempts removal only for containers labelled with its unique run ID, including their attached anonymous volumes. It never prunes unrelated Docker resources. Data and accounts are not reused by subsequent runs. Complete volume-removal behavior after native DCP cleanup remains a runtime verification obligation.

Private application certificates and encrypted key rings live in app-owned Linux tmpfs. Restarting an individual issuer/BFF container loses that protection state while databases may still exist: **restart the whole disposable graph, initialize and enroll again**. There is no single-container continuity or automatic repair guarantee. Application certificates expire after one day; use a fresh complete run for later sessions.

Aspire secret parameters mask display, but Docker-authorized local operators can inspect environment metadata containing encrypted PFX/passwords. Removing owned containers is part of disposal; no secure-erasure or memory-only transport claim is made. The normal TLS development certificate is managed separately by .NET/Aspire.

If initialization fails or is interrupted, the helper attempts to stop only the exact selected initializer. A mutation may already have committed; start a fresh complete run instead of retrying it. If prerequisites, image pulls, migrations, runtime health or TLS fail, use the fixed error and resource state to identify the failing stage. Never bypass TLS or private-file permissions. Browser automation/Playwright report issues remain documented under A-009 and do not change the manual demo commands.

## Acceptance scope

[Requirements](spec.md) and [tasks](tasks.md) require both-provider graph checks, normal runtime startup, native stdin initialization, genuine browser enrollment/login/logout and fresh-run cookie invalidation. Existing Chromium/WebKit security matrices remain required under the exact A-009 deferrals. The added demo-specific automation uses Chromium, matching Windows Chrome/Edge availability. Linux hosted execution does not establish actual Windows Docker Desktop execution; that limit must remain explicit.

M02-C refresh, M02-D API authority, M02-E React, all six historical findings, human assessment and production/public-release gates remain open.
