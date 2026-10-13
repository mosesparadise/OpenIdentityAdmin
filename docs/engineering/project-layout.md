# Project layout and test runner

The user requested this organization on 10 October 2026 (FND-009/010). Physical groups and `IdentityPlatform.slnx` folders agree. Project and assembly names stay unchanged; use cases remain under `Features` inside their owning projects. Composition roots and generated migrations retain their documented exceptions.

| Group | Projects / responsibility |
| --- | --- |
| `src/aspire` | `IdentityPlatform.AppHost`, `IdentityPlatform.ServiceDefaults`: local composition, health and telemetry defaults |
| `src/backend` | `IdentityPlatform.AuthServer`, `IdentityPlatform.Admin.Api`, `IdentityPlatform.Admin.Bff`, `IdentityPlatform.Worker`: runtime hosts |
| `src/core` | `IdentityPlatform.Core`: transport/provider-independent policy and configuration |
| `src/migrations` | `IdentityPlatform.Migrator`, `IdentityPlatform.Migrations.PostgreSql`, `IdentityPlatform.Migrations.SqlServer`: controlled schema changes |
| `src/persistence` | `IdentityPlatform.Infrastructure`: native Identity/OpenIddict contexts, provider registration and schema readiness |
| `src/frontend` | Shared `IdentityPlatform.DesignTokens` assets used by the Razor pilot; React/TypeScript admin client remains planned |
| `tests/architecture` | `IdentityPlatform.Architecture.Tests`: dependency and native model boundaries |
| `tests/aspire` | `IdentityPlatform.AppHost.Tests`: distributed lifecycle/harness; `IdentityPlatform.Issuer.Tests`: real HTTP/protocol/database and browser journeys |
| `tests/backend` | `IdentityPlatform.Host.Tests`: host configuration, health and telemetry |
| `tests/core` | `IdentityPlatform.Core.Tests`: core policy/configuration |
| `tests/persistence` | `IdentityPlatform.Persistence.Tests`: real database/native-manager behavior |

The BFF stays in backend: it holds browser sessions and tokens server-side. STS Razor Pages live in AuthServer; the Development-only pilot already uses the shared theme. The original grouping change created no runtime capability or schema changes; later feature increments are recorded separately. The approved v0.2 snapshots and earlier evidence retain historical paths; current quickstart commands use this layout.

## Native MTP

All six test projects use `xunit.v3.mtp-v2` **4.0.2** and Microsoft Testing Platform **2.5.1**. The package is part of the xUnit **v3** product family despite its 4.x package version. Central package pins and project lock files resolve the compatible graph. `global.json` selects the native .NET 10 MTP runner; `tests/Directory.Build.props` makes test assemblies executable and selects the MTP entry point. No VSTest adapter, Microsoft.NET.Test.Sdk or Coverlet collector is used.

Build before running verification:

```sh
dotnet restore --locked-mode
dotnet build --no-restore
python3 scripts/verify.py unit
python3 scripts/verify.py PostgreSql
python3 scripts/verify.py SqlServer
# After explicit browser installation and HTTPS trust provisioning (see verify.yml):
python3 scripts/verify.py PostgreSql --browser chromium
python3 scripts/verify.py SqlServer --browser chromium
```

For an individual project, use the explicit native MTP project option:

```sh
dotnet test --project tests/core/IdentityPlatform.Core.Tests/IdentityPlatform.Core.Tests.csproj --no-build --no-restore
# Discover provider rows without starting containers:
dotnet test --project tests/persistence/IdentityPlatform.Persistence.Tests/IdentityPlatform.Persistence.Tests.csproj --no-build --no-restore --list-tests --pre-enumerate-theories on --filter-trait Provider=PostgreSql
```

Use `--filter-trait Provider=PostgreSql` or `Provider=SqlServer` with pre-enumerated theories for provider runs; `Category=Harness` selects only the AppHost harness cases in the unit profile. Bare solution-wide `dotnet test` includes container tests for both providers and therefore needs all container/TLS prerequisites. The acceptance path includes unit, both HTTP provider profiles and both Chromium browser profiles. The provider profiles exclude browser cases; browser profiles require the selected engine to be installed and genuinely trust issuer/RP TLS. Firefox/WebKit use the same commands with `--browser firefox` or `--browser webkit` on both providers. CI runs all six browser cells nightly, manually and for dependency/browser/trust/harness changes; unknown diff evidence selects the full matrix. Browser provisioning is explicit in `.github/workflows/verify.yml` and never occurs during discovery.

`Microsoft.Testing.Extensions.TrxReport` emits native TRX reports. Verification deletes old reports, checks process exit status and enforces nonempty, complete, unskipped results. `eng/test-matrix.json` records reviewed case counts and project groups; update counts with intentional test additions/removals. A partial or accidentally expanded selection fails the gate. Existing assertions and per-test deadlines remain, linked to runner cancellation; resource cleanup retains its independent bounded teardown tokens.

The pinned external `dotnet-coverage` wrapper captures the test runner and child hosts. Do not add a second collector inside that session. CI first requires every selected report, then merges unit, PostgreSQL/SQL Server HTTP and all selected provider/browser reports and keeps the same 90% line/80% branch floor for each handwritten module. See the [verification record](../../specs/001-platform-foundation/verification-mtp-layout.md) for executed evidence.

Issuer verification also discovers the full test catalogue and both provider partitions, checks their disjoint/exhaustive identity union, then reconciles executed TRX names against discovery. Provider/browser artifact paths are distinct. Repeated positive xUnit simple trait filters use OR; the verifier instead uses an explicit VSTest-syntax conjunction within the native MTP runner. This does not add a VSTest adapter.

Issuer HTTP execution uses two fixed groups without splitting existing shared-fixture collections: `Flows` (130 cases/provider) and `Boundaries` (162). CI runs foundation separately from both issuer groups; `standard` and `all` still include both groups. For focused execution, after the same build/container/TLS prerequisites:

```sh
python3 scripts/verify.py PostgreSql --partition foundation
python3 scripts/verify.py PostgreSql --partition issuer --issuer-group Flows
python3 scripts/verify.py PostgreSql --partition issuer --issuer-group Boundaries
# Repeat all three commands with SqlServer for the other mandatory provider.
```

Before either issuer group runs, discovery proves both filters form the exact disjoint HTTP inventory. Execution uses strict TRX identity/completion checks. CI requires four separate `issuer-{provider}-{flows|boundaries}` reports; all-engine combined coverage requires 49 inputs. Each invocation retains its 660-second cap and each CI job its 20-minute cap. Aggregate available issuer time increases to two invocations per provider; this is test execution organization, not a product-performance claim. [The operations evidence](../../specs/008-administrator-foundation/operations-evidence.md#issuer-execution-deadline-and-fixed-decomposition) retains the rejected original timeout and review limits.
