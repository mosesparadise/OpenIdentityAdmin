# Foundation verification — 10 October 2026

**State: foundation acceptance checks passed.** This accepts feature 001's infrastructure scope only. It does not accept a working issuer, complete approved milestone 001, complete R1 or establish certification. Version 0.2 and A-001 are approved; mandatory PKCE and the external public-release certification gate remain unchanged.

## Accepted execution

[GitHub Actions run 38016602794](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38016602794) passed all five jobs for implementation commit `55a6f766a821a587a3b802d975470e22e790aba1`: unit, PostgreSQL, SQL Server, supply chain and per-module coverage. [Draft PR #1](https://github.com/mosesparadise/OpenIdentityAdmin/pull/1) contains the reviewable change. Subsequent documentation reconciliation does not change product or test code.

| Requirement / boundary | Passed evidence | Scope |
|---|---|---|
| FND-005/008: Core configuration | 25 tests | Provider and HTTPS issuer parsing; issuer is not yet configured in a protocol host |
| FND-004/005/008: hosts/configuration/telemetry | 43 tests | Real startup-error child processes, safe health responses/deadlines and actual exporter redaction |
| FND-002/006/008: architecture/model parity | 5 tests | Project boundaries, no runtime schema mutation/controllers, both-provider runtime/design parity and Identity V3/passkey schema |
| FND-001/006: Aspire harness/configuration | 5 tests | Required process metadata and Development-only composition guard |
| FND-002/003: PostgreSQL native persistence | 3 tests | Repeated/partial initialization, recovery/cancellation, users/passkeys/clients/scopes, case/concurrency, runtime/BFF permissions, outages and provisioning-log safety |
| FND-002/003: SQL Server native persistence | 3 tests | Same real-provider scenarios; no SQLite/InMemory substitution |
| FND-001/004: PostgreSQL distributed profile | 4 tests | Four HTTPS hosts, outage/liveness, overlapping isolated environments, configuration-failure cleanup and cancelled-startup cleanup |
| FND-001/004: SQL Server distributed profile | 4 tests | Same actual Aspire process scenarios |
| FND-006: verification gate regressions | 15 Python tests | Four TRX completeness, eight advisory/license and three coverage-gate cases |
| FND-007: AI instructions | Structure passed; eight bounded reviewer evaluations | Eight skills and eight reviewer configurations; effectiveness limits in the AI evaluation record |
| FND-006: supply chain | Passed | 16-project advisory lookup reports no known vulnerable packages; 205 NuGet package/version pairs inventoried; Gitleaks 8.30.1 source-snapshot scan clean |

**Total: 92 .NET tests passed, zero failed or skipped, plus 15 distinct Python verification-gate regressions.** Multiple assertions inside integration scenarios cover their named boundaries; this count does not imply product-wide E2E acceptance. Locked restores and 16-project builds passed with zero warnings/errors on the clean CI matrix.

## Per-module execution coverage

The successful aggregate job merged all three profiles, including child host processes. Every required handwritten module meets the approved 90% line / 80% branch floor. Tests, SDK-generated metadata and generated migrations are excluded from numeric product coverage; model parity and real-provider integration still verify the generated schema. Policy: `eng/coverage-policy.json`.

| Module | Lines | Branches |
|---|---:|---:|
| Core | 100% | 100% |
| Infrastructure | 98.6% | 100% |
| ServiceDefaults | 100% | 100% |
| Migrator | 98.6% | 90.0% |
| AuthServer | 100% | 100% |
| Admin.Api | 100% | 100% |
| Admin.Bff | 100% | 100% |
| Worker | 100% | 100% |
| AppHost | 100% | 100% |

Numeric coverage is a floor; it is not a claim that all possible behavior is proven. The artifact IDs and SHA256 digests for this exact successful run are recorded in [evidence-manifest.json](evidence-manifest.json). CI retains synthetic reports for seven days under repository access controls; hashes preserve provenance but do not guarantee continued artifact availability. Release assurance requires its own evidence retention policy.

## Reproducibility and local scope

- Genuine GitHub Spec Kit 1.1.3 initialized the Codex integration; `specify check` passed. Source/tag provenance is in research.md and third-party attribution is preserved.
- .NET SDK 10.0.401/runtime 10.0.12; EF/Identity 10.0.12, OpenIddict 7.7.1, Npgsql EF 10.0.3 and Aspire 13.6.1; dependencies have lock files.
- PostgreSQL 18 digest `74935e72241653ca55e0414067e6d8763aceb8a810eb51b452253ec3dcfc4336`; SQL Server 2022 digest `4402d880dd4c34bfa7d8705e56a86cd6c88da80a1f6bbbe741f999e76264a090`. Both executed in GitHub CI.
- The local managed environment blocks the SQL image redirect host `centralus.data.mcr.microsoft.com`; no local SQL execution is claimed. GitHub's real SQL execution supplies the required provider evidence. No network restriction was bypassed.
- Aspire uses its pinned supported NuGet DCP/dashboard distribution; only ASPIRE010 is suppressed with a documented reason. CLI-bundle-only features are not used.
- Normal HTTPS validation is used throughout. Local Linux and CI trust the actual development certificate alongside system trust; no accept-any callback exists.
- The documented standalone PostgreSQL launch served the authenticated dashboard (302 redirect) over trusted HTTPS, then stopped with exit 0 on terminal-style Ctrl+C. No containers remained. This dashboard smoke is separate from the distributed four-service tests.

Run `python3 scripts/verify.py unit`, `PostgreSql` and `SqlServer` after a current build. The scripts reject absent/empty/skipped/failed reports and impose outer process deadlines. CI's exact coverage collection and merge commands are in `.github/workflows/verify.yml`.

## Defects and review dispositions

- Initial types and migrations were absent: initial configuration tests did not compile, then native persistence failed on a missing table. Compilation failures were not represented as behavioral regression evidence; generated schema subsequently passed.
- Trace state escaped redaction and malformed bootstrap flags echoed input. Two regressions failed before fixes; explicit safe parsing and exporter-level redaction/suppression tests now pass.
- Identity V1 omitted the promised passkey table. Runtime/design options now use V3; unpublished initial platform migrations were regenerated. Only disposable test databases had used the earlier schema. Native passkey storage is not WebAuthn enrollment/login acceptance.
- Persistence tests now exercise the actual migrator, prove successful BFF state operations before permission denials, read through fresh scopes and verify case/concurrency through native managers.
- Verbose PostgreSQL bootstrap logging exposed a synthetic password. A strengthened regression failed before the dedicated unpooled connection/logging protections, then passed including transaction sampling and a diagnostic capture control. SQL Server's log regression passed for its normal server error log; arbitrary auditing/tracing is outside that evidence.
- First CI SQL outage readiness exceeded the eight-second assertion because EF history/existence probing used different connection behavior. Readiness now performs one fixed, read-only history query with explicit connection/command timeouts and no SQL reconnect retries. The real SQL outage regression passed. These are cooperative provider/deadline controls, not a universal hard real-time guarantee for every network failure.
- Aspire configuration-failure observation originally awaited the wrong process state; it now observes Finished and exit 2. Cancellation initially awaited intentionally held startup; it now cancels the concurrent caller wait and then disposes the underlying lifecycle. Both scenarios pass on both providers.
- Cleanup now verifies exact container-ID absence and captured process termination. Missing PID evidence for running/unknown states fails. A redundant EF CanConnect probe against already removed storage caused the second SQL workflow failure; independent test review approved its removal while retaining direct cleanup assertions.
- Build/test evidence rejects stale outputs. One local parallel MSBuild attempt exited prematurely; the subsequent serial build with build servers disabled passed. The standalone smoke initially signalled only the launcher; terminal-style process-group signal delivery corrected that harness.

Read-only architecture, test and security reviews and dispositions are recorded in [AI evaluation](../../docs/engineering/ai-evaluation.md). All current foundation findings are resolved. AI review is not independent certification or a penetration test.

## Remaining product and release work

Login/token issuance, BFF session lifecycle, management APIs, React/Razor themes, Scalar/Swagger, product browser E2E, accessibility audits, load benchmarks, mutation campaigns, independent penetration testing and official conformance/certification remain later approved work. No production deployment or external certification action occurred. Proprietary/legacy license exceptions and a complete deployment SBOM remain release obligations in [supply-chain evidence](../../docs/engineering/supply-chain.md).
