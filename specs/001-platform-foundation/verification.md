# Foundation verification — 10 October 2026

**State: implementation/review in progress; not foundation acceptance, issuer acceptance, R1 completion or certification.** Version 0.2 and A-001 are approved. Basic OP suite compatibility remains the accepted external certification dependency; it is not reapplied as an implementation blocker.

## Reproducibility

- Genuine GitHub Spec Kit 1.1.3 initialized the Codex skills integration; `specify check` passed. Annotated tag/source commit are recorded in research.md.
- .NET SDK 10.0.401/runtime 10.0.12; EF/Identity 10.0.12, OpenIddict 7.7.1, Npgsql EF 10.0.3, Aspire 13.6.1; dependencies have lock files.
- Docker 28.4.0. PostgreSQL 18 image digest `74935e72241653ca55e0414067e6d8763aceb8a810eb51b452253ec3dcfc4336` pulled and executed.
- SQL Server 2022 image manifest digest `4402d880dd4c34bfa7d8705e56a86cd6c88da80a1f6bbbe741f999e76264a090` resolved. Download redirects to `centralus.data.mcr.microsoft.com`, which the environment proxy denies. The required SQL cell failed startup; no SQL Server test has passed locally. The hostname enablement question remains pending.
- Aspire uses its supported pinned NuGet DCP/dashboard distribution, not CLI-bundle-only features. ASPIRE010 alone is suppressed with the reason in the AppHost project. The optional CLI bundle was unavailable; its absence is not a test pass.
- Local Linux HTTPS uses the actual dev certificate and its OpenSSL trust directory plus system trust. HTTP clients use standard TLS validation; no accept-any callback or protocol authentication bypass exists.

## Executed evidence

Commands were run in the selected repository with the installed SDK on PATH. `artifacts/` contains local synthetic reports, excluded from source control. CI retains test/coverage evidence for seven days under repository access controls.

| Boundary / requirement | Actual result | Scope/limit |
|---|---|---|
| Solution build, FND-006 | Passed, zero warnings/errors | Latest completed 16-project solution build |
| Core configuration, FND-005/008 | 25 passed, zero skipped | Provider spelling/invalid enum and safe HTTPS issuer URI parsing; no issuer is configured yet |
| Host/configuration/telemetry seams, FND-004/005/008 | 35 passed, zero skipped | Real configuration-error child processes for all four hosts and migrator; safe health, deadlines, cache behavior and exporter redaction |
| Architecture/model parity, FND-002/006/008 | 5 passed, zero skipped | Pure Core, reference/schema-change rules, both-provider generated/runtime model parity and schema catalog; model checks do not execute SQL Server |
| Native PostgreSQL storage, FND-002/003 | 3 passed, zero skipped | Migrator initialization/repetition/partial failure/recovery/cancellation; native users/passkeys/clients/scopes; case, concurrency, runtime/BFF privileges, outage bounds and failed-provisioning log safety |
| PostgreSQL Aspire process profile, FND-001/004 | Health/outage scenario passed | Actual migrator, four HTTPS hosts; 503 readiness and live liveness after database stop |
| Additional Aspire overlap/failure cleanup | Both passed in the full three-case PostgreSQL profile | Captured container IDs/processes removed; second overlapping environment remained healthy. New stricter PID/cancellation cases awaiting final rerun |
| SQL Server persistence/distributed profile | Blocked locally | Download-policy denial; no in-memory substitution or skipped-as-passed result |
| AI structure/fixture evaluation, FND-007 | Structure passed; eight bounded reviewer evaluations completed | See docs/engineering/ai-evaluation.md; not system assurance or proof of native discovery/sandbox enforcement |
| Execution coverage, FND-006 | Collection includes child host processes | Initial merged gate correctly failed missing startup/SQL branch coverage; additional startup tests added; final matrix gate not yet cleared |
| GitHub CI | Workflow written, remote execution pending | All three profiles required; provider failure prevents aggregate coverage acceptance |
| Supply chain | 16-project advisory lookup clean; 205 NuGet package/version pairs inventoried; Gitleaks 8.30.1 source scan clean | Time/feed/tool-bounded evidence; proprietary/legacy license exceptions recorded for release review |
| Verification gates | 15 Python regression tests passed | TRX completeness, advisory/license rejection and per-module coverage thresholds |

`python3 scripts/verify.py unit` runs the three unit/host/architecture projects. Provider profiles run Persistence.Tests then AppHost.Tests with the matching theory filter. `dotnet coverage collect -s eng/coverage.xml ... python3 scripts/verify.py <profile>` collects parent and child processes. `scripts/check-coverage.py` requires each named handwritten module, including composition roots, to reach 90% lines and 80% branches. Generated migrations remain behaviorally tested, even though excluded from numeric coverage.

## Before/after and review evidence

- Initial configuration tests failed because types did not yet compile; this was not a behavioral regression result.
- Before initial migrations, real PostgreSQL failed with missing `platform.OpenIddictApplications`; generated schema then passed the same native-manager scenario.
- Security review identified trace state escaping redaction and malformed bootstrap flags echoing input. The new regressions ran against the faulty code: **2 failed, 8 passed**. After the fixes: **10 passed**, subsequently expanded to 35 host tests. Exporter tests preserve useful safe spans and suppress immutable unsafe payloads.
- Architecture review found Identity V1 omitted the promised passkey table. Runtime/design-time now share explicit Identity V3 options. Unpublished initial platform migrations were regenerated; disposable tests alone had used the old initial files. Native synthetic passkey persistence is not a WebAuthn enrollment/login claim.
- Test review prompted successful BFF-state operations before provider-specific isolation denials, fresh-scope account reads, scope-name case tests, competing native manager updates and actual migration-runner failure/recovery tests.
- Scope and disposition detail: [AI evaluation and implementation reviews](../../docs/engineering/ai-evaluation.md).

## Open acceptance work

Both-provider execution remains mandatory. Locked restore and the 16-project build passed. One parallel MSBuild attempt exited prematurely without retained diagnostic files; a serial build with build servers disabled completed with zero warnings/errors. No failed-build test run is used as evidence for changed source.

The failed-provisioning PostgreSQL regression was observed failing before the fix and passing afterward with a diagnostic positive control. Transaction-sampled logging and SQL Server capture controls have since been added. Complete their final execution, the new startup-cancellation/strict PID cleanup cases, final per-module coverage and actual GitHub CI. Phase 7 convergence tasks T021–T024 retain these acceptance gaps. See docs/engineering/supply-chain.md for license exceptions and release evidence still required.

No browser UI, OAuth/OIDC journey, BFF session lifecycle, API contract/docs UI, performance benchmark, accessibility audit, conformance suite, independent penetration test or external certification has been completed by this foundation slice. Their approved feature plans remain required. No production deployment or external certification action was performed.
