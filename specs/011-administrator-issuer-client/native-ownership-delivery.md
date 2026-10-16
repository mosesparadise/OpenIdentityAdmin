# Accepted native storage ownership prerequisite

16 October 2026 (Europe/London). [PR #60](https://github.com/mosesparadise/OpenIdentityAdmin/pull/60) delivers the dormant native storage ownership prerequisite for feature 011 T005. It adds an explicit child-scope owner and four OpenIddict EF adapters, with exact context/transaction guards, strict monotonic deadlines, reader ownership and cleanup containment. Native manager semantics remain intact. Production composition does not activate it. Late results are rejected, but noncooperative native calls remain attached until actual completion; a hard dispatcher response bound remains unproved T005 work.

## Exact accepted candidate

[Run 38883236968](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38883236968), attempt 1, passed all **63 jobs** at source `e911781f87143bc73fbf2b6504e2d03a81596aa7`. Tested merge `7a27d74553acba79b074ab5768dea3da5cfa862a` has parents main `456a99c362ba3fb397901cfe41e1e086e2036268` and that source. Both share tree `48a607746c6391b5760e29e07e0a85719d9db686`. The normal squash `a4db4228026d2b02cd1d4ba38995fe8e84384752` has that same tree and sole parent `456a99c362ba3fb397901cfe41e1e086e2036268`; remote main was verified at that squash. The final gate explicitly accepts full scope. Fresh remote checks found unchanged main, 63 successful exact-head checks, a clean merge and no submitted review or restriction to bypass.

## Runtime and regression evidence

All 552 unit cases pass: Host 457, Core 25, Architecture 5 and AppHost harness 65. All 430 prior Host cases remain; 27 ownership cases are added. Each real provider passes 22 foundation cases, including seven Persistence, eight AppHost Foundation and seven BffSessions cases. The four new provider/store cells cover PostgreSQL and SQL Server with Platform and Sessions contexts. They exercise all four native managers, partial-create and nested-delete rollback, committed state, native filters and generic queries, actual SaveChanges cancellation swallowed by TryRedeem/TryRevoke, and rejection of reader/replaced-transaction/Prune commits. Independent architecture/test review reconciles all 596 raw cases (552 unit plus 44 native), exact unique identities and result/definition/entry bindings, with zero failures/skips/errors. All prior identities are retained. SQL Server’s new Sessions and Platform cases complete in 18.203 and 9.558 seconds; PostgreSQL’s in 10.143 and 2.488 seconds.

Independent review reconciles all **48** required coverage paths actually merged and their downloaded artifact IDs/digests to this run/head. The separately downloaded supply-chain archive is not a coverage input. Individual raw XML reports for all 48 inputs were not separately downloaded. All **15** modules pass the unchanged 90% line/80% branch floors. The new ownership production sources remain included. Merged XML SHA-256: `ac37398c4477857ea8d4a7503fda57a82ae119f0755f26b99fe604badf173998`.

| Module | Line coverage | Branch coverage |
| --- | ---: | ---: |
| IdentityPlatform.Core | 98.91% | 95.00% |
| IdentityPlatform.Infrastructure | 93.09% | 96.65% |
| IdentityPlatform.ServiceDefaults | 100.00% | 100.00% |
| IdentityPlatform.Migrator | 98.80% | 100.00% |
| IdentityPlatform.AuthServer | 97.33% | 100.00% |
| IdentityPlatform.Admin.Api | 100.00% | 100.00% |
| IdentityPlatform.Admin.Bff | 94.96% | 95.40% |
| IdentityPlatform.Worker | 100.00% | 100.00% |
| IdentityPlatform.AppHost | 99.35% | 100.00% |
| IdentityPlatform.Database.Abstractions | 99.11% | 100.00% |
| IdentityPlatform.Database.PostgreSql | 100.00% | 100.00% |
| IdentityPlatform.Database.SqlServer | 100.00% | 100.00% |
| IdentityPlatform.Database.Hosting | 95.45% | 100.00% |
| IdentityPlatform.Database.PostgreSql.Hosting | 100.00% | 100.00% |
| IdentityPlatform.Database.SqlServer.Hosting | 100.00% | 100.00% |

| Artifact | ID | ZIP SHA-256 |
| --- | --- | --- |
| governance-mutations-PostgreSql | 12039335505 | `a33498e33df57ebe4ea64fde3a5001dde1a7d92e4db09ed3011f85c4dcdbbc90` |
| foundation-unit | 12039030442 | `18d3188d80aea265528c9921579f45b99abdfc5305fe7cd43a1a3a49f0f1542b` |
| critical-mutations-PostgreSql | 12038463748 | `d3827b3d7743602630c6ef0da13cdc1992c98332f1c0402b241e5fc7406039bd` |
| administrator-mutations-PostgreSql | 12039590153 | `70c72eea8088a49af126c34ace75918cb12235ebc42d6aa5feaa85e6d4cea2b8` |
| governance-mutations-SqlServer | 12038542431 | `247ea6cc3d2bca9822da9ae82366d2c9e545c66fb1dd67886549ee26e2f13d2f` |
| foundation-PostgreSql | 12038722632 | `87be4f5a8b7e3ab2c64079ed01f40d4f28b9041e352d4aae45cd6d6a45907575` |
| critical-mutations-SqlServer | 12039620626 | `96d8369d90092192959e4c55268afc0d75724caa2ba6e9e15266936eaaf26417` |
| administrator-mutations-SqlServer | 12039755059 | `b2ded0169f848f90e558c5ff2e04c2e77c2effcbb97804afb264b79b3138b533` |
| foundation-coverage | 12039691297 | `78804734356b6b00a9a474434ae1eb6cdd6e5c627e1cb2b8e88326ad5346bc48` |
| foundation-SqlServer | 12039591271 | `36950630ae42b513efb4b6aaa49a83335f15efbc4faac9d1004f25f9f93f7aa1` |

The existing six security mutation suites retain 42 kills, 42 passing baseline controls and 42 passing restored controls. Independent security review reconciles all six ZIP digests, exact run/head/tested-merge bindings, manifests, source and recomputed mutant hashes, native test identities and restoration flags, with zero skips. The strict mutation verifier is unchanged. These sanitized receipts are not raw mutation TRX and do not establish new T005-specific mutation coverage.

Two separate local negative controls check the new test oracles at the accepted source. Changing strict expiry from <= to < produces the intended assertion failures at 10,000 and 10,001 milliseconds while the 9,999-millisecond control passes. Removing terminal fault rejection produces the intended assertion failure after context substitution. Baseline and restored runs pass all four cases, with source bytes restored and restored binaries rebuilt. An initial bare-method filter selected zero tests; the corrected fully qualified filter supplies the actual evidence. The initial zero-case TRX was overwritten. Exact mutant process exits were not separately retained; raw TRX records MTP’s exit-2 diagnostic. Independent review accepts the intended assertion/source-restoration evidence, without treating this local harness as equivalent to the stricter hosted mutation verifier.

The initial full run 38881117434 on 52c6aa860521cfbd203681f01a400f583bca72b1 failed strict import formatting and final acceptance, with the other 61 jobs passing. The correction changed three imports and recorded the failure; no test assertion, timeout, coverage floor or runtime behavior was weakened. Only the corrected full run supplies final acceptance.

## Continuation

This completes the ownership prerequisite, not T005 or B2. Next implement and prove one original request lifetime across fresh native scopes, then bind cleanup to actual dispatcher rejection/error/finally paths. Early durable proof consumption/native redemption, quota reservation and issuance commit/linearization remain required. Preserve the fresh-context boundary after each transaction; creating a fresh scope must not restart the original request budget.

All six historical findings, 48 human assessment rows and activation/public-release gates remain open. A-007 defers Firefox through internal B2 only, with restoration before B3 implementation/activation/release. No production activation, schema migration, diagnostic approval renewal or external release occurred.

The documentation delivery uses its own proportional exact-head checks and normal squash, recorded on its PR; it does not repeat executable acceptance.
