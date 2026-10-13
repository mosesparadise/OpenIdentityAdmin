# Administrator accounts: operation and recovery

This runbook covers the implemented local administrator enrollment, password/TOTP login, restricted recovery, security actions and logout. It is a controlled evaluation handoff, not production deployment approval or certification. Administrator API/BFF/React access, current-permission/last-administrator enforcement and the wider authorization server remain separate work. See the [roadmap](../roadmap.md), [feature tasks](../../specs/008-administrator-foundation/tasks.md) and [automated MFA evidence](../../specs/008-administrator-foundation/mfa-evidence.md).

## Start a controlled Windows/WSL2 evaluation

Use new synthetic identities, a disposable authenticator profile and an isolated password-manager vault. The current private-file credential adapter runs on Linux; on Windows run .NET, Docker integration and Aspire within WSL2. Follow the [WSL2 prerequisites](first-party-pilot.md#windows-with-wsl2) for the pinned SDK, Docker and HTTPS. Windows browser trust and Linux process trust are separate. Trust only the intended public development certificate; never bypass certificate errors or copy an issuer private PFX into the browser trust store.

1. Restore/build the pinned repository with `dotnet restore --locked-mode` and `dotnet build IdentityPlatform.slnx --no-restore`.
2. Privately configure `Database__Provider=PostgreSql` or `SqlServer`, `Identity__AdministratorAccounts__Enabled=true` and `Pilot__Enabled=false`. Supply the complete [issuer-protection configuration](issuer-protection.md), including distinct signing, encryption and wrapping keys and a private Linux key-ring directory. Use protected configuration; do not put secrets in shell arguments, history, checked-in settings or recordings.
3. Run `dotnet run --project src/aspire/IdentityPlatform.AppHost --no-build` in WSL2. AppHost supplies the actual canonical HTTPS issuer and secret resource configuration. Wait for the ordinary Migrator schema upgrade to complete and the `auth` resource to become healthy.
4. In another WSL2 terminal, privately configure the same live platform database's runtime connection and invoke the explicit [administrator bootstrap procedure](../../specs/008-administrator-foundation/quickstart.md#invoke-separately-from-schema-upgrade). Retain its operation ID before submission. Bootstrap is not automatic; it creates one disabled, enrollment-pending account and permanently consumes its receipt. A pilot account cannot substitute for this account.
5. Open the `auth` resource's HTTPS endpoint from the authenticated Aspire dashboard, then `/account/administrator/enrollment`. The dashboard URL is not the issuer URL. These local account journeys need no OAuth relying party.

Aspire's default database containers are disposable and are removed on shutdown. This is not durable account hosting. Do not assume retained credential files can restore a deleted database. Repeat the controlled assessment on the other provider using a separately provisioned synthetic environment.

## Feature activation

Both switches default to false. Complete issuer protection and current schemas are required for either enabled account family.

| Pilot enabled | Administrator accounts enabled | Available behavior |
| --- | --- | --- |
| false | false | Foundation/protection profile: health endpoints; no login journey. |
| true | false | Development-only pilot login and its bounded OIDC flow. |
| false | true | Administrator enrollment/MFA/recovery/security pages; no pilot OIDC flow. |
| true | true | Both isolated account families; pilot still requires Development and its explicit bootstrap/client setup. |

An administrator session grants local account-status/security access. It does not authorize Admin API/BFF, documentation routes or OAuth requests. A password-only interaction and recovery-code redemption do not grant a full account session. Enabling accounts neither creates an administrator nor reopens bootstrap.

## Supported journeys

| Journey | Procedure and security boundary |
| --- | --- |
| Enrollment | Submit bootstrap username/password at `/account/administrator/enrollment`. Add the displayed setup key manually to a TOTP authenticator; no QR workflow is implemented. Confirm a current six-digit code, preserving leading zeroes. Save all ten single-use recovery codes from the successful response, then use **I have saved my codes**. |
| Ordinary login | Start at `/account/administrator/login`; submit password, then TOTP through its bound `/account/administrator/mfa` continuation. Never construct or share an interaction URL as a substitute for the browser binding. |
| Restricted recovery | Choose **Use a recovery code**, submit password and one unused recovery code. Redemption revokes old sessions and permits only replacement setup. Confirm the new authenticator to obtain a session and new codes. The old factor cannot restore full authority while recovery is required. |
| Voluntary replacement | From account status, choose **Replace authenticator**, supply fresh password and unused current TOTP, then prove the new key. Until completion the old factor remains usable. Completion revokes old sessions and replaces recovery codes. |
| Code regeneration | From account status, provide fresh password and unused current TOTP. Save the ten new codes immediately. Old codes and local sessions are invalidated; the completing browser receives a replacement session. |
| Local logout | Choose **Sign out**, then POST its confirmation. GET does not revoke anything. Only the current stored account session is revoked; this is not OIDC or global logout. |

Password/step-up interactions expire after five minutes. GET does not extend freshness. Account sessions have a thirty-minute idle ceiling and eight-hour absolute ceiling. Stored eligibility, security stamp, authentication/factor generations and session revocation are rechecked; a cookie alone is not authority. Correct passwords do not erase the shared factor-failure budget. Five failed attempts can impose a five-minute lockout across nodes/browser jars; wait for the supported policy and begin a legitimate fresh interaction. Process restarts, new cookies or direct database edits are not lockout recovery procedures.

## Lost responses, abandonment and factor loss

Do not automatically retry ambiguous security mutations. Keep the authenticator's newly configured key until the outcome is reconciled, without retaining it in logs or support tickets.

| Boundary | Supported next action |
| --- | --- |
| Password-admission response lost | Start a fresh password interaction; no recovery verifier has necessarily been consumed at this point. |
| Recovery redemption committed, but its new binding/response lost | Old sessions may already be revoked. Start with fresh password and another unused recovery code. Do not replay the consumed verifier. |
| Setup GET response lost, original binding retained | Reopen that bound stage within its original expiry. The same staged key can be read; GET does not renew the password proof. |
| Enrollment/replacement completion response lost | Codes from a committed completion cannot be redisplayed. If the newly installed factor works, authenticate afresh and deliberately regenerate codes with fresh proof. A failed login does not prove the earlier transaction rolled back. |
| Regeneration response lost | Old codes/sessions may already be invalidated. The installed factor remains the supported way to authenticate afresh and explicitly regenerate. |
| Voluntary replacement abandoned | Use the existing factor; staging alone does not replace it. |
| Restricted recovery stage expired | Use fresh password and another unused recovery code. Remaining codes work until successful replacement invalidates the old set. |
| Every factor/recovery code lost, or pending bootstrap password lost | No implemented operator bypass, reset endpoint or bootstrap reopening exists. Record the gap; do not edit production state, suppress MFA or claim recovery support. Recreating an entirely disposable evaluation environment does not demonstrate account recovery. |

Recovery codes appear only in the successful completion response. Refresh/back navigation must not be used as a recovery-code archive. Never capture setup keys, passwords, codes, cookies, antiforgery values or protected payloads in screenshots, HAR files, HTML dumps, traces or issue reports.

## Health, telemetry and incident triage

`/alive` means the process is alive; `/health` checks configured readiness. Neither proves a complete login, usable historical keys, healthy cleanup under sustained load, safe backup restoration or last-administrator availability. Use controlled synthetic journey probes only under an appropriate operational plan; do not replay real one-time proofs.

Enabled administrator composition subscribes the host's OpenTelemetry metric provider to these exact meters. With the existing OTLP endpoint configuration, they can be sent to the configured collector; Aspire supplies its local telemetry endpoint. The host-exporter regression verifies registration/export to an in-memory reader, not a deployed dashboard or collector. Confirm collector reachability, access controls and actual receipt in each deployment before relying on an alert.

| Meter / instrument | Meaning and limits |
| --- | --- |
| `IdentityPlatform.Administrator.Ceremonies` / `administrator.ceremony.decisions` | Counter with only `outcome`: `verified`, `replay-denied`, `capacity-denied`, `locked`. Verification occurs inside a mechanism; it does not prove the surrounding transaction committed or a login succeeded. |
| Same / `administrator.cleanup.backlog` | Histogram samples eligible metadata row counts, tagged only by `category`: `interactions`, `sessions`, `browsers`, `replays`. It is not a continuously current gauge or total database size. |
| `IdentityPlatform.Administrator.Cleanup` / `administrator.cleanup.deleted` | Counter of a successfully returned pass's removed rows. Categories commit independently; earlier deletion can commit before a later category fails, so this counter can undercount committed deletion. |
| Same / `administrator.cleanup.deferred` | Untagged counter of failed passes that are deferred to a later tick. It does not identify the failing dependency or prove data loss. |

No user identifiers, IP addresses, interaction IDs or credentials are metric dimensions. There are no separate audit/database/protection failure counters in this increment. Do not infer exact security/audit accounting or an availability SLO from these instruments. Administrator-disabled hosts do not subscribe to them; pilot activation alone does not opt in.

| Symptom | Bounded response |
| --- | --- |
| Generic invalid proof/form or 400/401/403 | Do not infer whether an account exists. Check the current journey, original binding, expiry and supported proof. Start a legitimate fresh interaction where needed; preserve antiforgery and TLS checks. |
| 413 or rate/concurrency rejection | Reduce request size or rate and wait. Current front-door limits are process-local; distributed account budgets are separate. Do not repeatedly resubmit one-time credentials. |
| 503 with `X-Identity-Incident` | Record the generated reference, UTC time, provider, exact deployed revision and non-secret operation category. Administrator-only composition logs warning event **1002** under `IdentityPlatform.AuthServer.Features.Accounts.Shared.AccountRequestProtection`; pilot-enabled composition uses its existing event **1001**. A started/interrupted response may have no incident header. |
| Cleanup backlog/deferred increases or capacity denial | Check authorized infrastructure health, locks and scheduler operation. The scheduler runs every sixty seconds with at most one hundred rows per category and a shared five-second pass deadline. Allow the next scheduled pass after recovery; no blanket retry is added to security mutations. |
| Clock/replay denial | Check trusted host time synchronization and infrastructure clock history. Persisted watermarks prevent rollback from reopening proof; restarting processes or deleting replay rows is not remediation. |
| Protection startup/request failure | Check private file access and the correct complete key inventory through the [protection runbook](issuer-protection.md). Never print raw provider exceptions, key XML, PFX contents or configuration secrets. |

Retained ceilings include 10,000 global interaction rows, 10,000 browser budgets and 32 retained sessions per user. Expiration alone does not free their physical occupancy; cleanup must commit. Admission refuses unsafe new allocations instead of evicting still-needed state. Existing eligible nonallocating work can remain usable. The design's twenty-four-hour healthy cleanup objective is not a measured production retention/SLA guarantee. Cleanup never removes audit history, bootstrap receipts or account clock watermarks.

## Durable state, upgrades and restore limits

For any later durable hosting, preserve the exact canonical HTTPS issuer, database and complete encrypted Data Protection ring across instances and restarts. Retain every required current/historical wrapping decryptor and revocation file. Factor protection is issuer-bound. Keep runtime DML credentials separate from schema credentials and run reviewed provider migrations through the ordinary Migrator before enabling the new host version. Quiesce old writers for a coordinated upgrade unless a separately reviewed mixed-version strategy proves compatibility; current lifecycle tests do not establish arbitrary rolling schema upgrades.

The database includes the permanent bootstrap receipt, factor/authentication generations, interactions, sessions, consumed recovery/replay state, revocations and audit history. Back up this security state with the complete ring/key inventory through a protected operational process. Do not restore just a factor row, reset the receipt, create a fresh ring to hide damage, or return to an older database and assume spent credentials remain spent. A database rollback can resurrect consumed-proof/revocation history. The accepted restart tests preserve the original database and keys; they do not prove arbitrary backup restoration, disaster recovery, RPO or RTO. Those procedures need separate design, authorization and evidence before production use.

## Verification and remaining acceptance

The [quickstart](../../specs/008-administrator-foundation/quickstart.md#automated-validation) gives native xUnit v3/MTP provider/story/browser commands. Required CI executes PostgreSQL and SQL Server, all three browser engines, explicit mutation controls and module coverage. Consult the exact PR/head/run evidence rather than treating a build or one browser as whole-product acceptance.

Use the [administrator manual assessment](administrator-manual-assessment.md) and [result template](administrator-manual-results-template.md) for actual Windows/WSL2 observations. No human results are supplied by automation. T017/T018/T028/T029 whole-increment acceptance, current-permission/last-admin work, later API/BFF/UI features, external assurance and A-001 certification gates remain tracked separately. An exported metric, passing browser scan or reviewed runbook does not establish WCAG, ASVS, NIST or OIDF certification.
