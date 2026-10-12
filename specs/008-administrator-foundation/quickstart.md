# Bootstrap validation guide — planned

Status: design only until increment1 source and verification evidence land. The administrator created by that increment is disabled/enrollment-pending; it will not provide an administrator login yet.

Use the existing Linux/WSL2 .NET10/Aspire prerequisites and disposable PostgreSQL/SQL Server profiles. Apply the current product migrations through Migrator using schema credentials, then invoke the proposed `--Operation initialize-administrator` operation separately with runtime platform DML credentials. Supply the bounded JSON in [the command contract](contracts/bootstrap.md) from a protected secret source via redirected standard input. Never type a real password into shell history, command arguments or ordinary configuration. No real credential examples are checked in.

Validate successful creation, unchanged repeated invocation with the same operation ID, rejection of a different operation, password verification through native Identity and the disabled/pending state. After changing/removing the account or its memberships in an isolated fixture, verify that repeating bootstrap does not repair it. Do not attempt manual database edits in a real deployment as a recovery procedure.

Run the dedicated administrator project once per provider through the finalized strict test runner after implementation; exact runnable commands and evidence paths must be added before delivery. Current design scenarios are listed in [test-strategy.md](test-strategy.md). Preserve existing issuer/lifecycle/browser, contract, static-analysis, mutation and per-module coverage gates.

If initialization reports failure after possible commit, reconcile with the original operation ID and username. Do not invent a new operation ID to force another administrator. Bootstrap is permanently consumed after success. Lost passwords/factors need the later authorized recovery design, not bootstrap replay.
