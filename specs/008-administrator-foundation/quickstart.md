# Protected administrator bootstrap — increment 1

This creates a **disabled, enrollment-pending** administrator. It does not enable administrator login, issue a session, or unlock Admin API/BFF access. MFA enrollment and restricted recovery are the next increment. See [verification.md](verification.md) for acceptance status.

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

Each provider requires exactly 45 discovered and executed cases, none skipped. TRX reports are under `artifacts/tests/administrator-{provider}/`. These isolated jobs retain the 660-second invocation limit and do not enlarge issuer suites. An unavailable provider is a failed prerequisite, not a successful or skipped test. Full CI additionally enforces regression, static analysis, mutation, secret/dependency checks and all required module coverage inputs.

## Next account journeys — not runnable yet

The [MFA design](mfa-design.md) documents the next increment, including enrollment, TOTP, restricted recovery, lockout, key retention and loss behavior. T022 adds the strict `Identity:AdministratorAccounts:Enabled` composition switch and independent safeguards, but all administrator journey routes remain unimplemented until their later packets pass acceptance; do not treat this document as an instruction to enable unimplemented account pages. Bootstrap remains the only implemented administrator operation.
