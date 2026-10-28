# Native Aspire and dependency delivery — 28 October 2026

[PR #107](https://github.com/mosesparadise/OpenIdentityAdmin/pull/107) is squash-merged as `b9fdfc7bf3e18c20efe655cdcfe48bd405c9edd3`. Feature 015 T001–T007 are complete for internal development and Linux automated verification. Start the AppHost with its `local-demo` profile and initialize through the authenticated Aspire dashboard; no custom PowerShell entry point remains. [Current quickstart](../014-local-b3-demo/quickstart.md).

The user explicitly selected **“Retain Playwright 1.63.0 and finish (Recommended)”** on 28 October. This confirms the [documented compatibility exception](dependencies.md#playwright-compatibility-exception--28-october-2026) and supersedes earlier pending-preference wording. Microsoft.OpenApi 2.13.0 and TypeScript 5.9.3 remain the other compatibility constraints. All remaining direct package/tool upgrades are retained. Stable .NET 10.x SDK selection replaces the exact SDK pin; actual verification used SDK 10.0.403/runtime 10.0.14. Package locks remain exact.

## Exact source and acceptance

| Identity | Value |
| --- | --- |
| Base | `1d1ce53597d96abe063caf51a0d037f817589a8d` |
| Reviewed source | `78a9c165f3ac17c88a34dc298ed64a4333162d7f` |
| Tested merge | `97070f5241daeaa93845843f053e2db06e5dc4f2` |
| Squash | `b9fdfc7bf3e18c20efe655cdcfe48bd405c9edd3` |
| Identical source/merge/squash tree | `1624bb4667e0565565fb23440380596b739dc482` |
| Accepted CI | [40662669919, attempt 1](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919) |

All **93 required jobs** passed. The exact source had 93 successful check runs, no submitted change-request review and no unresolved inline thread before the normal expected-head squash. No review restriction was bypassed.

Independent ordinary evidence reconciliation covers **74 jobs, 77 native report summaries and 4,639 passes**, with zero failures/skips. Core contracts additionally verify **37 Node identities**, five generated documents and unchanged contract output. Unit results are Core 25, Host 1,561, Architecture 5 and AppHost 100, totaling 1,691. All prior AppHost identities remain; 33 command/process controls are added. The issuer keyboard scenario was intentionally renamed to the required Given/When/Then form while preserving both provider cases and assertions.

Both PostgreSQL and SQL Server pass the real dashboard-command bootstrap, enrollment, fresh password/MFA, local/issuer-assisted logout, replay rejection, fresh-run invalidation and container/volume disposal. Both historical database upgrade lanes preserve old rows and migration histories while applying new rows with the upgraded EF runtime. Both retained browser engines pass their required scenarios, including zero-CSP login and the separate deliberate inline-style rejection control. No CSP bypass, event filter, new browser deferral, retry or deadline expansion was introduced.

Architecture, security, Aspire, test, vendor/dependency and final evidence reviews are complete within their recorded scopes. Local locked restore, full solution build, formatting/imports, code-quality and supply-chain checks pass. Seven obsolete quality allowances were removed without expanding the legacy baseline. Earlier failed/cancelled candidates remain in [verification](verification.md); none contributes passing acceptance to this run.

## Coverage and mutation evidence

The aggregate verifies all **74 required coverage inputs** and all **15 unchanged module floors** (90% lines, 80% branches). Handwritten composition remains included; exclusions and the frozen producer boundary are unchanged.

| Module | Lines | Branches |
| --- | ---: | ---: |
| IdentityPlatform.Core | 99.0% | 91.7% |
| IdentityPlatform.Infrastructure | 93.4% | 95.6% |
| IdentityPlatform.ServiceDefaults | 100.0% | 100.0% |
| IdentityPlatform.Migrator | 98.7% | 100.0% |
| IdentityPlatform.AuthServer | 94.7% | 98.3% |
| IdentityPlatform.Admin.Api | 100.0% | 100.0% |
| IdentityPlatform.Admin.Bff | 94.3% | 97.6% |
| IdentityPlatform.Worker | 100.0% | 100.0% |
| IdentityPlatform.AppHost | 97.0% | 99.0% |
| IdentityPlatform.Database.Abstractions | 99.1% | 100.0% |
| IdentityPlatform.Database.PostgreSql | 100.0% | 100.0% |
| IdentityPlatform.Database.SqlServer | 100.0% | 100.0% |
| IdentityPlatform.Database.Hosting | 95.5% | 100.0% |
| IdentityPlatform.Database.PostgreSql.Hosting | 100.0% | 100.0% |
| IdentityPlatform.Database.SqlServer.Hosting | 100.0% | 100.0% |

All ten mutation jobs pass independent review: **64 baseline passes, 64 expected behavioral kills and 64 restored passes**. Each job checks out the tested merge; exact mutation identities, phase outcomes and unchanged verifiers were reconciled. Final acceptance validates all 16 required job groups in full scope.

## Native report reconciliation

Each row below comes from finite successful native console summaries and the repository's strict report verifier. Every checkout matches the tested merge above; all rows have zero failures/skips.

| Job | Native reports | Passed |
| --- | ---: | ---: |
| [Browser administrator Accounts / PostgreSql / chromium](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730153228) | 1 | 8 |
| [Browser administrator Accounts / PostgreSql / webkit](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730153264) | 1 | 8 |
| [Browser administrator Accounts / SqlServer / chromium](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730153422) | 1 | 8 |
| [Browser administrator Accounts / SqlServer / webkit](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730153435) | 1 | 8 |
| [Browser administrator BrowserLogin / PostgreSql / chromium](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730153159) | 1 | 9 |
| [Browser administrator BrowserLogin / PostgreSql / webkit](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730153151) | 1 | 9 |
| [Browser administrator BrowserLogin / SqlServer / chromium](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730153224) | 1 | 9 |
| [Browser administrator BrowserLogin / SqlServer / webkit](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730153268) | 1 | 9 |
| [Browser administrator BrowserProtection / PostgreSql / chromium](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730153135) | 1 | 11 |
| [Browser administrator BrowserProtection / PostgreSql / webkit](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730153315) | 1 | 11 |
| [Browser administrator BrowserProtection / SqlServer / chromium](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730153208) | 1 | 11 |
| [Browser administrator BrowserProtection / SqlServer / webkit](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730153196) | 1 | 11 |
| [Browser administrator BrowserSessions / PostgreSql / chromium](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730153253) | 1 | 13 |
| [Browser administrator BrowserSessions / PostgreSql / webkit](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730153221) | 1 | 13 |
| [Browser administrator BrowserSessions / SqlServer / chromium](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730153201) | 1 | 13 |
| [Browser administrator BrowserSessions / SqlServer / webkit](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730153152) | 1 | 13 |
| [Browser administrator Security / PostgreSql / chromium](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730153141) | 1 | 5 |
| [Browser administrator Security / PostgreSql / webkit](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730153363) | 1 | 5 |
| [Browser administrator Security / SqlServer / chromium](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730153200) | 1 | 5 |
| [Browser administrator Security / SqlServer / webkit](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730153312) | 1 | 5 |
| [Browser standard  / PostgreSql / chromium](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730153239) | 1 | 30 |
| [Browser standard  / PostgreSql / webkit](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730153131) | 1 | 29 |
| [Browser standard  / SqlServer / chromium](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730153186) | 1 | 30 |
| [Browser standard  / SqlServer / webkit](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730153301) | 1 | 29 |
| [Contracts chromium](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730152789) | 1 | 7 |
| [Contracts core](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730152842) | 1 | 43 |
| [Contracts webkit](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730152674) | 1 | 7 |
| [PostgreSql](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730064400) | 1 | 12 |
| [PostgreSql-apphost-bffsessions](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730064446) | 1 | 10 |
| [PostgreSql-apphost-foundation](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730064702) | 1 | 8 |
| [PostgreSql-apphost-localdemo](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730064262) | 1 | 1 |
| [PostgreSql-lifecycle-distribution](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730064767) | 1 | 23 |
| [PostgreSql-lifecycle-keyrotation-activation](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730064794) | 1 | 3 |
| [PostgreSql-lifecycle-keyrotation-retirement](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730064742) | 1 | 2 |
| [PostgreSql-lifecycle-restart](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730064660) | 1 | 13 |
| [SqlServer](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730064451) | 1 | 12 |
| [SqlServer-apphost-bffsessions](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730064485) | 1 | 10 |
| [SqlServer-apphost-foundation](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730064303) | 1 | 8 |
| [SqlServer-apphost-localdemo](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730064517) | 1 | 1 |
| [SqlServer-lifecycle-distribution](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730064715) | 1 | 23 |
| [SqlServer-lifecycle-keyrotation-activation](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730064657) | 1 | 3 |
| [SqlServer-lifecycle-keyrotation-retirement](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730064973) | 1 | 2 |
| [SqlServer-lifecycle-restart](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730064550) | 1 | 13 |
| [administrator-PostgreSql-accounts](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730064488) | 1 | 58 |
| [administrator-PostgreSql-bootstrap](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730064403) | 1 | 45 |
| [administrator-PostgreSql-browsersessions-foundations](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730064714) | 1 | 167 |
| [administrator-PostgreSql-browsersessions-lifecycle](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730064645) | 1 | 57 |
| [administrator-PostgreSql-browsersessions-product](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730064629) | 1 | 98 |
| [administrator-PostgreSql-ceremonies](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730064678) | 1 | 41 |
| [administrator-PostgreSql-factors](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730064344) | 1 | 58 |
| [administrator-PostgreSql-governance-commands](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730064565) | 1 | 68 |
| [administrator-PostgreSql-governance-failures](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730064522) | 1 | 61 |
| [administrator-PostgreSql-governance-policy](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730064695) | 1 | 74 |
| [administrator-PostgreSql-protocoladmission](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730064533) | 1 | 110 |
| [administrator-PostgreSql-recovery](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730064821) | 1 | 19 |
| [administrator-PostgreSql-security](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730064598) | 1 | 21 |
| [administrator-SqlServer-accounts](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730064668) | 1 | 58 |
| [administrator-SqlServer-bootstrap](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730064392) | 1 | 45 |
| [administrator-SqlServer-browsersessions-foundations](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730064672) | 1 | 167 |
| [administrator-SqlServer-browsersessions-lifecycle](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730064583) | 1 | 57 |
| [administrator-SqlServer-browsersessions-product](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730064610) | 1 | 98 |
| [administrator-SqlServer-ceremonies](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730064603) | 1 | 41 |
| [administrator-SqlServer-factors](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730064360) | 1 | 58 |
| [administrator-SqlServer-governance-commands](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730064690) | 1 | 68 |
| [administrator-SqlServer-governance-failures](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730064929) | 1 | 166 |
| [administrator-SqlServer-governance-policy](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730064731) | 1 | 74 |
| [administrator-SqlServer-protocoladmission](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730064698) | 1 | 110 |
| [administrator-SqlServer-recovery](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730064578) | 1 | 19 |
| [administrator-SqlServer-security](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730064339) | 1 | 21 |
| [issuer-PostgreSql-boundaries](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730064283) | 1 | 162 |
| [issuer-PostgreSql-flows](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730064372) | 1 | 131 |
| [issuer-SqlServer-boundaries](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730064490) | 1 | 162 |
| [issuer-SqlServer-flows](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730064294) | 1 | 131 |
| [unit](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40662669919/job/122730064464) | 4 | 1691 |
| **Total** | **77** | **4,639** |

## Retained artifacts and review limits

Artifact API metadata reconciles 85 required report/mutation/coverage artifacts among 87 retained artifacts. Representative digests:

| Artifact | ID | API SHA-256 |
| --- | --- | --- |
| PostgreSQL demo | 12805046980 | `9b3f72a906d001145769cda21d4bcda2371b7712a48cf49e167198f19576ad54` |
| SQL Server demo | 12804649745 | `5337facfc34c476808756bbf2f9fd4d21168a1993cf12e6c1a30494e3649c79d` |
| Merged coverage | 12805866769 | `e4928e0369381e8fceb30b08b4ab1bfc4d101f0e6bdbad2324511d83322de1fa` |

Review uses exact-source CI strict verifiers, finite logs and artifact API metadata. **Raw ZIP bytes were not independently downloaded/hashed or XML reparsed.** These API digests are not local byte-verification claims. Supplementary workspace receipts are not portable repository artifacts:

- `/tmp/pr107-delivery-evidence/ordinary-review-final-40662669919.json`: SHA-256 `e1d8909590845b0ff34bf45d47c3e7e4a29918d2b411c845bf34a9bafa0ce05e`.
- `/tmp/pr107-delivery-evidence/mutation-review-40662669919.json`: SHA-256 `7a8a44e12f11bb561df6573d4c0742f2621684bd5b0d60a0a6836b7f609dbf1f`.
- `/tmp/pr107-delivery-evidence/final-review-40662669919.json`: SHA-256 `d3707e15dac3910367b983817ac51b1748783d71f941b896be1a6c18e32cfc7f`.

## Remaining work

Actual Windows Docker Desktop execution remains **unperformed**; feature 014 SC-001/T014 retains the complete both-provider walkthrough. Linux CI establishes the current native command path and disposal, not Windows platform acceptance.

Fourteen documentation-viewer findings remain unresolved, including affected deepmerge code in the shipped Swagger bundle. Viewers remain unmapped in default/demo hosts; inventory, notice, activation and public-release obligations remain open. A-009 restoration, all six historical findings, 48 human rows, 345 not-assessed assurance controls and production/public-release gates remain open. No external certification or deployment is claimed.

Next bounded roadmap work is the reviewed **M02-C specification and plan** for server-held refresh, two-node serialization, rotation/replay, current eligibility and logout revocation. Do not repeat delivered B3, local-demo or dependency implementation.
