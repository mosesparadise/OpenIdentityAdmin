# xUnit v3/MTP and grouped layout verification

User-requested update, 10 October 2026; FND-009/010 and tasks T025–T029. Earlier foundation acceptance is preserved in [verification.md](verification.md) and its original evidence manifest. This record covers the subsequent tooling/layout update; issuer and R1 acceptance remain outside this change.

## Implementation and local evidence

- All 16 projects moved to responsibility groups matching solution folders. Project references retain the same target assemblies and metadata. Production C# source, migrations, configuration and lock graphs are unchanged.
- All five test projects now use native .NET 10 MTP, xUnit v3 family package `xunit.v3.mtp-v2` 4.0.2, MTP 2.5.1 and TRX reporter 2.5.1. VSTest/Test.Sdk/Coverlet dependencies are removed.
- `dotnet restore --locked-mode` passed. A build from fresh grouped output directories passed with zero warnings/errors using SDK 10.0.401. The first migration build correctly rejected the obsolete parallelization API and missing cancellation arguments; both were fixed, with no analyzer suppression.
- `dotnet coverage collect -s eng/coverage.xml -o artifacts/coverage/unit.cobertura.xml -f cobertura python3 scripts/verify.py unit` passed: Core 25, Host 43, Architecture 5, AppHost harness 5; **78 tests, zero skips**.
- The PostgreSQL coverage-wrapped profile passed locally: all three persistence and four Aspire tests, zero failures/skips; process/container cleanup completed. Its TRX rows have unique IDs and the correct provider arguments. Local unit + PostgreSQL coverage contains all nine required first-party modules, including all four child runtime hosts; complete provider coverage still requires SQL Server.
- Native MTP `--list-tests --pre-enumerate-theories on --filter-trait Provider=...` passed for both assemblies and both providers. Each selects exactly three persistence cases (native persistence/idempotency, failure/resume, provisioning-log secrecy) and four Aspire cases (healthy profile/outage, cancellation/teardown, concurrent isolation, invalid-service teardown). Displayed arguments match the selected provider; harness cases are absent from provider runs.
- Eight TRX/report regressions, three coverage-gate regressions and eight supply-chain regressions passed: **19 Python tests**. Newly enforced case counts reject partial/expanded selections even when every selected test passes; missing reports fail. AI instructions pass structural validation.
- Current advisory inventory covers 16 projects with no reported known vulnerable packages. License inventory covers 209 package/version pairs; three previously reviewed exceptions remain. Legacy xunit.abstractions and its URL-only exception were removed. Redacted Gitleaks 8.30.1 scan of the staged source snapshot passed.

## Independent review

Architecture reviewer: normalized dependency graph and metadata unchanged, 16 solution entries resolve without duplicates/omissions, original files mapped to new groups, Features and immutable approved snapshots preserved. Its evidence-clarity comment is addressed by separating this update from the earlier verified foundation and linking this record from README.

Test reviewer: no assertion removals, consistent five-project MTP graph, matching provider traits/arguments, explicit theory enumeration, strict exit/report/count checks, preserved AppHost serialization and independent teardown bounds. No blocking findings. Runtime/provider/coverage results remain separate from these static reviews.

## Accepted matrix and retained evidence

[CI run 38040891293](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38040891293) passed on implementation commit `bf538166bab414b3bf2c8f81476acd01ea098143`. All five jobs succeeded on their first execution; no failed check was retried into a pass. Later acceptance-documentation commits change no implementation or test inputs.

| Profile/gate | Required evidence | Result |
| --- | --- | --- |
| unit | Core 25 + Host 43 + Architecture 5 + AppHost harness 5 | 78 passed, zero skips |
| PostgreSql | 3 persistence + 4 Aspire | 7 passed, zero skips; also passed locally |
| SqlServer | 3 persistence + 4 Aspire | 7 passed, zero skips in GitHub CI |
| Verification scripts | 8 TRX + 3 coverage + 8 supply-chain regressions | 19 passed |
| Build | All 16 projects, locked dependencies | Zero warnings/errors |
| Supply chain | Advisory, license and secret checks | Passed; 209 package/version pairs, three tracked license exceptions |

All **92 .NET tests** passed through native MTP. Original assertions were retained. The downloaded SQL Server TRX artifact additionally confirms seven unique passing rows with SQL Server arguments and no PostgreSQL rows. Its archive and the merged coverage archive match GitHub's SHA-256 digests. Artifact IDs/digests are recorded in [evidence-mtp-layout.json](evidence-mtp-layout.json); CI retention is seven days. Earlier evidence is preserved separately.

| Handwritten module | Lines | Branches |
| --- | ---: | ---: |
| Core | 100.0% | 100.0% |
| Infrastructure | 98.6% | 100.0% |
| ServiceDefaults | 100.0% | 100.0% |
| Migrator | 98.6% | 97.8% |
| AuthServer | 100.0% | 100.0% |
| Admin.Api | 100.0% | 100.0% |
| Admin.Bff | 100.0% | 100.0% |
| Worker | 100.0% | 100.0% |
| AppHost | 100.0% | 100.0% |

The unchanged 90% line / 80% branch per-module gate passed in CI and against the downloaded merged report. All child hosts remain instrumented; no coverage exclusions or floors were relaxed. Generated migration verification remains the real-provider/model checks described in the foundation record.

FND-009 is evidenced by native MTP execution, provider discovery/row identity, strict TRX/count regression tests, locked dependencies and full child coverage. FND-010 is evidenced by all 16 solution projects matching disk groups, resolving references, preserved architecture checks, updated launch/test documentation and clean-checkout CI builds. T025–T029 are complete.

The managed local environment still cannot download the redirected SQL Server image; SQL acceptance comes from the real GitHub runner, not a local skip or substitute. No product endpoint, browser UI, deployment or certification claim is added by this engineering update.
