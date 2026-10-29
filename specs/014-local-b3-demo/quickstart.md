# Disposable B3 demo through Aspire

The accepted container implementation from PR #105 is retained. [Feature 015](../015-aspire-local-entry/spec.md) replaces the PowerShell helpers with native Aspire launch and dashboard initialization; [accepted delivery](../015-aspire-local-entry/delivery.md) records PR #107 and all required automation. Actual Windows Docker Desktop execution remains unverified (SC-001/T014).

## Prerequisites

- Windows with Docker Desktop running **Linux containers**; no WSL terminal or custom PowerShell scripts. SQL Server requires a supported x64 container host; ARM64 suitability is not claimed.
- A stable **.NET 10.x SDK**, with access to NuGet and the configured container registries.
- A current .NET development HTTPS certificate trusted by Windows and valid for `*.dev.localhost`. Chrome/Edge resolve these names without editing the hosts file.
- Available dashboard port 17100, issuer port 17443 and BFF port 17444.

## Start the AppHost

Set **IdentityPlatform.AppHost** as your IDE startup project, select its **local-demo** launch profile and run/debug normally. Alternatively, from the repository root in any terminal:

```text
dotnet tool restore
dotnet restore --locked-mode
dotnet dev-certs https --trust
dotnet run --project src/aspire/IdentityPlatform.AppHost --launch-profile local-demo
```

Certificate trust is a normal one-time development prerequisite. If a stale certificate lacks the required SAN, inspect it and deliberately create a current .NET development certificate; never disable TLS validation.

PostgreSQL is the existing default. To select SQL Server, append `-- --Database:Provider=SqlServer` to the run command, or set `Database:Provider` through AppHost user secrets. Existing registered provider implementations remain selectable. The ordinary `https` profile retains health-only startup.

The authenticated Aspire dashboard opens from the launch profile. Keep the AppHost running. Initial image builds can take several minutes. Wait for `migrator` to complete, `auth` and `admin-bff` to become healthy, and `initializer` to run. Health does not mean an administrator exists.

## Initialize and enroll

In the dashboard, open the **initializer** resource actions and choose **Initialize administrator**. Enter a synthetic username and password in the dialog; the password field is masked.

Aspire sends the bounded bootstrap document to the exact run-owned initializer through redirected UTF-8 stdin, followed by separate public registration and revision-one B3 upgrade operations. Credentials are not stored in arguments, configuration, resource properties or logs. Native password policy still applies. Once a mutation has been attempted, initialization cannot be repeated for that run. Do not use production credentials.

1. Open <https://issuer.dev.localhost:17443/account/administrator/enrollment>.
2. Enter the initialized credentials, add the setup key to an authenticator, and submit a genuine TOTP.
3. Save the one-time recovery codes privately for this disposable run.
4. Open the workspace at <https://bff.dev.localhost:17444/> and choose **Sign in**. Every explicit sign-in requires fresh password and MFA, including when an issuer cookie exists. Completion returns to Overview.
5. Choose **Your session** to view the account ID and original expiry. **Check session** performs a read without extending the session. Appearance supports light/dark/system. Management APIs and application/user management screens remain pending.
6. Choose **Sign out**. Local sign-out revokes the BFF session. The issuer option first signs out locally and then offers separate issuer confirmation. Returning alone does not prove issuer revocation. Use **Back to workspace** to return and observe signed-out status.

Use these canonical URLs for account/BFF journeys so origins match the native registrations; dashboard resource links may target health endpoints.

## Stop, restart and troubleshoot

Stop the AppHost through your IDE or Ctrl+C. Native Aspire/DCP disposes its run-owned containers and anonymous volumes. No cleanup script or global Docker prune is used. Subsequent runs do not reuse accounts or data. Current both-provider Linux CI verifies the native dashboard command and disposal; the Windows walkthrough remains open. Forced host termination is not a cleanup guarantee.

Private certificates and encrypted key rings live in application-owned Linux tmpfs. Restarting an individual issuer/BFF container loses those keys while its database may still exist: **restart the whole graph, initialize and enroll again**. No individual-container continuity or automatic repair is promised. Application certificates expire after one day; start a fresh complete run for later sessions.

Aspire secret parameters mask display, but Docker-authorized local operators can inspect encrypted PFX/password metadata. No secure-erasure or memory-only transport claim is made. The TLS development certificate remains managed separately by .NET/Aspire.

After failed/interrupted initialization, the command attempts to stop only the verified immutable initializer container and reports if cleanup could not be confirmed. A mutation may already have committed; start a new complete run instead of retrying. Never bypass TLS or private-file permissions. A-009 browser/report deferrals remain documented.

## Acceptance limits

Required automation retains real PostgreSQL/SQL Server, native bootstrap, genuine Chromium enrollment/login/logout, fresh-run invalidation, existing Chromium/WebKit security matrices and exact A-009 deferrals. Linux CI does not establish actual Windows Docker Desktop execution. The M02-E visible React shell is delivered in [PR #116](https://github.com/mosesparadise/OpenIdentityAdmin/pull/116). M02-C refresh, M02-D management API authority, application management screens, all six historical findings, human assessment and production/public-release gates remain open.
