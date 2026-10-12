# Protected administrator bootstrap — increment 1

This creates a **disabled, enrollment-pending** administrator. It does not enable administrator login, issue a session, or unlock Admin API/BFF access. Explicitly enabled enrollment and MFA login are accepted in T025; restricted recovery follows in T026. See [verification.md](verification.md) for acceptance status.

## Prerequisites

Use the existing Linux/WSL2 .NET 10/Aspire prerequisites. Apply current product migrations using the ordinary Migrator `upgrade` operation and schema credentials first. The bootstrap command uses only the platform connection, with runtime DML credentials. It cannot create schemas or database roles. Do not enable `LocalDevelopment:ProvisionRuntimeRoles` for this operation.

Set `Database__Provider` to `PostgreSql` or `SqlServer` and supply `ConnectionStrings__platform` from your protected configuration/secret source. Do not put database passwords or administrator passwords in command arguments, shell history, checked-in files or retained logs. Do not reuse the Aspire pilot account as the administrator.

## Invoke separately from schema upgrade

Build the pinned solution:

```bash
dotnet restore --locked-mode
dotnet build IdentityPlatform.slnx --no-restore
```

Choose and retain an operation GUID and the original username in the deployment record **before** submitting the command. Reconciliation requires those same values. The operator reference is a self-declared audit reference, not external proof of operator identity.

Supply the [bounded JSON contract](contracts/bootstrap.md) from a protected source via redirected standard input. For an interactive local evaluation, this Python helper prompts on the terminal without echoing values and emits only JSON to the Migrator. The password must be 16–256 UTF-16 code units. Run without shell tracing or terminal recording:

```bash
python3 -c 'import getpass,json,sys; json.dump({"operationId":getpass.getpass("Retained operation GUID: "),"operatorReference":getpass.getpass("Operator reference: "),"userName":getpass.getpass("Administrator username: "),"password":getpass.getpass("Administrator password: ")},sys.stdout)' \
  | dotnet run --project src/migrations/IdentityPlatform.Migrator --no-build --no-launch-profile -- --Operation initialize-administrator
```

The equivalent published-executable invocation is `dotnet IdentityPlatform.Migrator.dll --Operation initialize-administrator`, receiving the same redirected JSON and protected database configuration. No HTTP listener is opened. Input must finish at EOF within the three-minute deadline.

Exit 0 means initialization committed, or the original receipt was already present; fixed output distinguishes these. Exit 2 means invalid input/configuration. Exit 1 means conflict, schema/operational failure or deadline. Product output intentionally omits raw provider exceptions and submitted values. Use restricted infrastructure diagnostics to investigate operational failures; never add credential-bearing console diagnostics.

If the result is lost after possible commit, submit the original operation ID and username again. Do not invent a new operation ID to force another administrator. Replay never resets a password, enables an account, restores membership or recreates a deleted user. Bootstrap is permanently consumed after success. Lost credentials require the later authorized recovery design, not manual production database edits.

## Automated validation

The dedicated Aspire suite creates disposable databases and synthetic secrets. It runs the production executable, native managers and real HTTP pilot controls. Docker and trusted local HTTPS certificates are required; see the existing WSL2/pilot runbook for machine setup.

```bash
python3 scripts/verify.py unit
python3 scripts/verify.py PostgreSql --partition administrator
python3 scripts/verify.py SqlServer --partition administrator
```

Each provider runs isolated Bootstrap, Factors, Ceremonies and Accounts stories. Exact reviewed case counts live in `eng/test-matrix.json`. Every identity must be discovered and executed, none skipped. Use `--story Bootstrap`, `--story Factors`, `--story Ceremonies` or `--story Accounts` to select one explicitly. TRX reports are under `artifacts/tests/administrator-{provider}-{story}/`, with lowercase story names. These isolated jobs retain the 660-second invocation limit and do not enlarge issuer suites. An unavailable provider is a failed prerequisite, not a successful or skipped test. Full CI additionally enforces regression, static analysis, mutation, secret/dependency checks and all required module coverage inputs.

## Enrollment and MFA — accepted T025

PR #30 adds accepted enrollment, ordinary password/TOTP login and stored account-session validation. Follow [enrollment/login](enrollment-login.md) and [verification](verification.md) for the current evidence. Restricted recovery, authenticator replacement, code regeneration and logout remain T026. Administrator API/BFF/docs and OAuth authorization remain unavailable.

The enrollment/login configuration was accepted in PR #30; T026 security routes require their own final acceptance below. In WSL2, use the [pilot prerequisites](../../docs/operations/first-party-pilot.md#windows-with-wsl2) for Docker, Linux private file permissions and separate Windows/browser HTTPS trust. Provide the complete [issuer protection](../../docs/operations/issuer-protection.md) settings through protected local configuration. Keep the signing, encryption and wrapping keys distinct and retain the Data Protection ring while its sessions/factors are in use.

Set `Identity__AdministratorAccounts__Enabled=true` and `Pilot__Enabled=false` in the AppHost's protected configuration. Select `Database__Provider=PostgreSql` or `SqlServer`. Account activation and pilot activation are independent; enabling accounts never provisions an administrator automatically. AppHost forwards the resolved AuthServer HTTPS issuer and the complete protection settings through secret parameters to their owners.

```bash
dotnet run --project src/aspire/IdentityPlatform.AppHost --no-build
```

Keep this disposable Aspire environment running. Its default database state is removed on shutdown; it is not durable administrator hosting. In a second WSL terminal, configure the same live platform database's **runtime** connection privately, then run the protected `initialize-administrator` operation above. Do not copy a connection string into shell arguments, this document, screenshots or shared logs. The ordinary Migrator must have completed its schema upgrade first.

Open the `auth` resource's HTTPS endpoint from the authenticated Aspire dashboard, then:

1. Visit `/account/administrator/enrollment` and submit the protected-bootstrap username and password.
2. Add the displayed setup key to a TOTP authenticator and submit its current six-digit code within five minutes.
3. Save all ten single-use recovery codes in a secure password manager. They are displayed only in this successful response; refreshing or resubmitting cannot recover them. Their restricted recovery journey is still T026 and must not be represented as available yet.
4. Follow the account-status link. Subsequent authentication begins at `/account/administrator/login`, followed by `/account/administrator/mfa` through its browser-bound continuation.

The session lasts at most eight hours and expires after thirty minutes without accepted activity. Password-only authentication never creates it. Every protected request rechecks stored account/factor/session state; it grants only the account's status surface. Do not capture setup keys, codes or cookies in screenshots, browser traces or reports. Abandoning enrollment leaves the account pending; losing all enrolled factors has no bootstrap-reset or operator-bypass path.

For durable hosting outside disposable Aspire, keep the exact canonical HTTPS issuer, database, Data Protection ring and protection keys stable across instances/restarts. The current native protection adapter requires Linux/WSL2. This paragraph does not authorize production deployment or claim production/certification acceptance.

Focused automated commands:

```bash
python3 scripts/verify.py PostgreSql --partition administrator --story Accounts
python3 scripts/verify.py SqlServer --partition administrator --story Accounts
python3 scripts/verify.py PostgreSql --partition administrator --browser chromium
python3 scripts/verify.py PostgreSql --partition administrator --browser firefox
python3 scripts/verify.py PostgreSql --partition administrator --browser webkit
```

Run all three browser commands with `SqlServer` as well. Browser installation and TLS trust follow the pinned workflow. Test fixtures provision disposable synthetic material themselves; manual bootstrap credentials must never be supplied to these tests. Actual human WSL2/password-manager/screen-reader observations remain distinct from automated browser evidence.

## Recovery and account security — T026

Implementation is under verification; consult [recovery/security](recovery-security.md) and the final PR checks before treating these routes as accepted.

- From administrator login, choose **Use a recovery code** (`/account/administrator/recovery`). Supply the original username/password, then one unused recovery code. Recovery invalidates old local sessions and authorizes only a fresh authenticator setup; it does not sign you in. Confirm the new authenticator's code to obtain a new account session and ten new recovery codes.
- From account status, use **Replace authenticator**. Enter the password and an unused code from the current authenticator, then confirm the displayed new key with its code. Abandoning this voluntary stage keeps the current factor usable. Successful completion invalidates all old local sessions and replaces recovery codes.
- Use **Regenerate recovery codes** from account status with the password and an unused current authenticator code. Save the new codes immediately. All previously issued recovery codes and local sessions are invalidated; the current browser receives a replacement session.
- Use **Sign out** and submit its confirmation. Visiting the confirmation alone does not log out. The POST revokes exactly this stored account session, leaving other current sessions intact.

Fresh-password/step-up interactions expire after five minutes; a GET never extends that time. If recovery staging expires or its response is lost, restart with a fresh password and a different unused recovery code. Remaining codes remain usable until successful factor replacement. If a final completion response is lost, its codes cannot be redisplayed; authenticate with the installed factor and explicitly regenerate codes. Losing every factor/recovery code has no operator bypass or bootstrap reset. These are local account journeys, not Admin API/BFF access or OpenID Connect logout.

Focused verification (repeat for `SqlServer`):

```bash
python3 scripts/verify.py PostgreSql --partition administrator --story Recovery
python3 scripts/verify.py PostgreSql --partition administrator --story Security
python3 scripts/verify.py PostgreSql --partition administrator --browser chromium --browser-story Security
python3 scripts/verify.py PostgreSql --partition administrator --browser firefox --browser-story Security
python3 scripts/verify.py PostgreSql --partition administrator --browser webkit --browser-story Security
```

Omitting `--browser-story` executes both mandatory Accounts and Security invocations separately, retaining each existing timeout. Native no-script, keyboard, theme, accessibility and secret-history checks run in all three engines. Human screen-reader/password-manager evidence remains a separate tracked obligation.
