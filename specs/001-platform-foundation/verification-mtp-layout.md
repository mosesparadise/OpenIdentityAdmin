# xUnit v3/MTP and grouped layout verification

User-requested update, 10 October 2026; FND-009/010 and tasks T025–T029. Earlier foundation acceptance is preserved in [verification.md](verification.md) and its original evidence manifest. This record covers the subsequent tooling/layout update; issuer and R1 acceptance remain outside this change.

## Implementation and local evidence

- All 16 projects moved to responsibility groups matching solution folders. Project references retain the same target assemblies and metadata. Production C# source, migrations, configuration and lock graphs are unchanged.
- All five test projects now use native .NET 10 MTP, xUnit v3 family package `xunit.v3.mtp-v2` 4.0.2, MTP 2.5.1 and TRX reporter 2.5.1. VSTest/Test.Sdk/Coverlet dependencies are removed.
- `dotnet restore --locked-mode` passed. A build from fresh grouped output directories passed with zero warnings/errors using SDK 10.0.401. The first migration build correctly rejected the obsolete parallelization API and missing cancellation arguments; both were fixed, with no analyzer suppression.
- `dotnet coverage collect -s eng/coverage.xml -o artifacts/coverage/unit.cobertura.xml -f cobertura python3 scripts/verify.py unit` passed: Core 25, Host 43, Architecture 5, AppHost harness 5; **78 tests, zero skips**.
- Native MTP `--list-tests --pre-enumerate-theories on --filter-trait Provider=...` passed for both assemblies and both providers. Each selects exactly three persistence cases (native persistence/idempotency, failure/resume, provisioning-log secrecy) and four Aspire cases (healthy profile/outage, cancellation/teardown, concurrent isolation, invalid-service teardown). Displayed arguments match the selected provider; harness cases are absent from provider runs.
- Eight TRX/report regressions, three coverage-gate regressions and eight supply-chain regressions passed: **19 Python tests**. Newly enforced case counts reject partial/expanded selections even when every selected test passes; missing reports fail. AI instructions pass structural validation.
- Current advisory inventory covers 16 projects with no reported known vulnerable packages. License inventory covers 209 package/version pairs; three previously reviewed exceptions remain. Legacy xunit.abstractions and its URL-only exception were removed. Redacted Gitleaks 8.30.1 scan of the staged source snapshot passed.

## Independent review

Architecture reviewer: normalized dependency graph and metadata unchanged, 16 solution entries resolve without duplicates/omissions, original files mapped to new groups, Features and immutable approved snapshots preserved. Its evidence-clarity comment is addressed by separating this update from the earlier verified foundation and linking this record from README.

Test reviewer: no assertion removals, consistent five-project MTP graph, matching provider traits/arguments, explicit theory enumeration, strict exit/report/count checks, preserved AppHost serialization and independent teardown bounds. No blocking findings. Runtime/provider/coverage results remain separate from these static reviews.

## Required matrix status

| Profile/gate | Required evidence | Status |
| --- | --- | --- |
| unit | 78 cases | Local native MTP pass |
| PostgreSql | 3 persistence + 4 Aspire | Local run in progress |
| SqlServer | 3 persistence + 4 Aspire | Pending GitHub CI; local redirected image host remains blocked |
| Per-module coverage | All nine handwritten modules, 90% lines / 80% branches | Pending complete CI merge |
| Supply chain | Advisory, license and secret checks | Local pass; CI pending |

This status will be replaced with the exact run/commit and retained artifact evidence after the required matrix completes. No provider run is substituted or counted as skipped success.
