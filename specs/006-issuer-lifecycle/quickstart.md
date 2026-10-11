# Lifecycle verification quickstart

This is a synthetic automated test harness, not a production restart command or the interactive pilot UI. Use Linux or Windows with WSL2, the pinned .NET SDK, Docker, and trusted development HTTPS as described in the [pilot quickstart](../004-first-party-login/quickstart.md). PostgreSQL and SQL Server run as isolated real containers. The [issuer protection runbook](../../docs/operations/issuer-protection.md) explains the existing durable-key requirements.

From the repository root:

```bash
dotnet tool restore
dotnet restore --locked-mode
dotnet build --no-restore --nologo
python3 scripts/test_lifecycle_selection.py
```

The ordinary provider runner includes the existing persistence/AppHost/issuer cases and the separate lifecycle invocation. It verifies exact discovered/executed test identities and rejects missing, skipped, duplicated or contradictory results:

```bash
python3 scripts/verify.py PostgreSql
python3 scripts/verify.py SqlServer
```

CI schedules standard (300 cases), restart (13) and distribution (23) as separate jobs for each provider:

```bash
python3 scripts/verify.py PostgreSql --partition standard
python3 scripts/verify.py PostgreSql --partition lifecycle --story Restart
python3 scripts/verify.py PostgreSql --partition lifecycle --story Distribution
```

Omitting `--partition` executes the complete 336-case provider profile, expanding lifecycle stories into separate bounded invocations. `--partition lifecycle` without `--story` runs both stories separately. No case is omitted. CI wraps each command in `dotnet coverage collect -s eng/coverage.xml -o artifacts/coverage/<label>.cobertura.xml -f cobertura`. Labels are `<provider>`, `<provider>-lifecycle-restart` and `<provider>-lifecycle-distribution`. Every story has its own report and coverage artifact. All thirteen full-matrix coverage inputs and the nine unchanged production-module floors are mandatory. Each provider test invocation remains <=660 seconds and each CI job <=20 minutes.

For a focused development experiment, use the exact project and explicit provider selection:

```bash
dotnet test --project tests/aspire/IdentityPlatform.Issuer.Lifecycle.Tests/IdentityPlatform.Issuer.Lifecycle.Tests.csproj \
  --no-build --no-restore --no-progress --pre-enumerate-theories on \
  --filter 'Provider=PostgreSql&FullyQualifiedName~IdentityPlatform.Issuer.Lifecycle.Tests.Features.Distribution.' --report-trx --report-trx-filename Issuer.Lifecycle.trx \
  --results-directory artifacts/tests/PostgreSql-lifecycle-distribution
```

Replace `PostgreSql` with `SqlServer` for the other provider. A focused command is diagnostic only: it does not run the complete profile, exact discovery/evidence gates, browser regression or coverage acceptance. The matrix requires 13 restart and 23 distribution cases per provider, no browser cases. Story reports are `artifacts/tests/<provider>-lifecycle-<restart|distribution>/Issuer.Lifecycle.trx`.

The harness replaces only the issuer child while retaining its original origin, database containers, completed migrator/bootstrap generations, HTTPS relying party, private files and populated DP ring. It verifies old-process exit, new generation, fresh health and a real HTTPS response. It tests original artifacts; a fresh credential login cannot substitute for old-cookie continuity. Native authorization-code lifetime remains 60 seconds. Failed setup/restart invalidates the environment; required cases cannot silently skip or retry.

All application generations and containers must be removed before private material is disposed. Do not use production identities/secrets, disable TLS, enlarge artifact lifetimes, or restart the entire disposable AppHost as a substitute for issuer continuity. Do not publish raw diagnostic output containing local infrastructure details.

Distribution adds a second real AuthServer to the disposable graph. Requests preserve the canonical HTTPS origin, Host, TLS validation and original browser state; a test-only socket selector pins each request to a verified process-owned loopback listener. Token exchange and public metadata validation are explicitly pinned too. It exercises cross-node continuity, simultaneous code attempts, committed restrictions, surviving-node operation and bad/mismatched credential recovery. Old clients reject replacement process generations; clients must be recreated after a node restarts. No production proxy trust policy or automatic security retry is introduced.

Restart delivery and current distribution acceptance are recorded in [verification.md](verification.md). Key rotation remains the third sequential PR; no Windows assistive-technology, backup restore, production RTO or certification acceptance follows from this harness.
