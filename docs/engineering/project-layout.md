# Project layout and test runner

The user requested this organization on 10 October 2026 (FND-009/010). Physical groups and `IdentityPlatform.slnx` folders agree. Project and assembly names stay unchanged; use cases remain under `Features` inside their owning projects. Composition roots and generated migrations retain their documented exceptions.

| Group | Projects / responsibility |
| --- | --- |
| `src/aspire` | `IdentityPlatform.AppHost`, `IdentityPlatform.ServiceDefaults`: local composition, health and telemetry defaults |
| `src/backend` | `IdentityPlatform.AuthServer`, `IdentityPlatform.Admin.Api`, `IdentityPlatform.Admin.Bff`, `IdentityPlatform.Worker`: runtime hosts |
| `src/core` | `IdentityPlatform.Core`: transport/provider-independent policy and configuration |
| `src/migrations` | `IdentityPlatform.Migrator`, `IdentityPlatform.Migrations.PostgreSql`, `IdentityPlatform.Migrations.SqlServer`: controlled schema changes |
| `src/persistence` | `IdentityPlatform.Infrastructure`: native Identity/OpenIddict contexts, provider registration and schema readiness |
| `src/frontend` | Reserved React/TypeScript admin client; README is a solution item until the application is implemented |
| `tests/architecture` | `IdentityPlatform.Architecture.Tests`: dependency and native model boundaries |
| `tests/aspire` | `IdentityPlatform.AppHost.Tests`: distributed lifecycle and harness checks |
| `tests/backend` | `IdentityPlatform.Host.Tests`: host configuration, health and telemetry |
| `tests/core` | `IdentityPlatform.Core.Tests`: core policy/configuration |
| `tests/persistence` | `IdentityPlatform.Persistence.Tests`: real database/native-manager behavior |

The BFF stays in backend: it holds browser sessions and tokens server-side. Future STS Razor Pages stay in AuthServer. This reorganization creates no new runtime capability and changes no native schema or migration history. The approved v0.2 snapshots and earlier evidence retain historical paths; current quickstart commands use this layout.

## Native MTP

All five test projects use `xunit.v3.mtp-v2` **4.0.2** and Microsoft Testing Platform **2.5.1**. The package is part of the xUnit **v3** product family despite its 4.x package version. Central package pins and project lock files resolve the compatible graph. `global.json` selects the native .NET 10 MTP runner; `tests/Directory.Build.props` makes test assemblies executable and selects the MTP entry point. No VSTest adapter, Microsoft.NET.Test.Sdk or Coverlet collector is used.

Build before running verification:

```sh
dotnet restore --locked-mode
dotnet build --no-restore
python3 scripts/verify.py unit
python3 scripts/verify.py PostgreSql
python3 scripts/verify.py SqlServer
```

For an individual project, use the explicit native MTP project option:

```sh
dotnet test --project tests/core/IdentityPlatform.Core.Tests/IdentityPlatform.Core.Tests.csproj --no-build --no-restore
# Discover provider rows without starting containers:
dotnet test --project tests/persistence/IdentityPlatform.Persistence.Tests/IdentityPlatform.Persistence.Tests.csproj --no-build --no-restore --list-tests --pre-enumerate-theories on --filter-trait Provider=PostgreSql
```

Use `--filter-trait Provider=PostgreSql` or `Provider=SqlServer` with pre-enumerated theories for provider runs; `Category=Harness` selects only the AppHost harness cases in the unit profile. Bare solution-wide `dotnet test` includes container tests for both providers and therefore needs all container/TLS prerequisites. The three-profile script is the acceptance path.

`Microsoft.Testing.Extensions.TrxReport` emits native TRX reports. Verification deletes old reports, checks process exit status and enforces nonempty, complete, unskipped results. `eng/test-matrix.json` records reviewed case counts and project groups; update counts with intentional test additions/removals. A partial or accidentally expanded selection fails the gate. Existing assertions and per-test deadlines remain, linked to runner cancellation; resource cleanup retains its independent bounded teardown tokens.

The pinned external `dotnet-coverage` wrapper captures the test runner and child hosts. Do not add a second collector inside that session. CI merges unit/PostgreSQL/SQL Server reports and keeps the same 90% line/80% branch floor for each handwritten module. See the [verification record](../../specs/001-platform-foundation/verification-mtp-layout.md) for executed evidence.
