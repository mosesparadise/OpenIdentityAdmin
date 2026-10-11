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

CI schedules `python3 scripts/verify.py <provider> --partition standard` (300 cases) separately from `--partition lifecycle` (13 cases). Omitting `--partition` keeps the complete 313-case local profile. Each CI job uses `dotnet coverage collect -s eng/coverage.xml -o artifacts/coverage/<label>.cobertura.xml -f cobertura` around its runner command, where labels are `<provider>` and `<provider>-lifecycle`. Coverage requires both new lifecycle artifacts plus every existing input: eleven with all six browser cells. The nine production-module floors are unchanged. Every provider test invocation remains bounded by 660 seconds and each CI job by 20 minutes; splitting jobs addresses the measured combined SQL job timeout.

For a focused development experiment, use the exact project and explicit provider selection:

```bash
dotnet test --project tests/aspire/IdentityPlatform.Issuer.Lifecycle.Tests/IdentityPlatform.Issuer.Lifecycle.Tests.csproj \
  --no-build --no-restore --no-progress --pre-enumerate-theories on \
  --filter 'Provider=PostgreSql' --report-trx --report-trx-filename Issuer.Lifecycle.trx \
  --results-directory artifacts/tests/PostgreSql
```

Replace `PostgreSql` with `SqlServer` for the other provider. A focused command is diagnostic only: it does not run the complete profile, exact discovery/evidence gates, browser regression or coverage acceptance. The runner expects 13 lifecycle cases per provider, with no browser cases. Default/focused reports are `artifacts/tests/<provider>/Issuer.Lifecycle.trx`; CI and `--partition lifecycle` use `artifacts/tests/<provider>-lifecycle/Issuer.Lifecycle.trx`.

The harness replaces only the issuer child while retaining its original origin, database containers, completed migrator/bootstrap generations, HTTPS relying party, private files and populated DP ring. It verifies old-process exit, new generation, fresh health and a real HTTPS response. It tests original artifacts; a fresh credential login cannot substitute for old-cookie continuity. Native authorization-code lifetime remains 60 seconds. Failed setup/restart invalidates the environment; required cases cannot silently skip or retry.

All application generations and containers must be removed before private material is disposed. Do not use production identities/secrets, disable TLS, enlarge artifact lifetimes, or restart the entire disposable AppHost as a substitute for issuer continuity. Do not publish raw diagnostic output containing local infrastructure details.

Restart source acceptance and its final PR delivery gate are recorded in [verification.md](verification.md). Two-node and key-rotation cases are later sequential PRs; no Windows assistive-technology, backup restore, production RTO or certification acceptance follows from this harness.
