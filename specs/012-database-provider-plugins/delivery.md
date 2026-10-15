# Accepted internal provider delivery

16 October 2026 (Europe/London). [PR #58](https://github.com/mosesparadise/OpenIdentityAdmin/pull/58) was normally squash-merged as `f8539bf5aad826319864550c4113609f2205e2e1`. Feature 012 delivers the user-approved relational EF Core extension boundary under A-008. PostgreSQL and SQL Server use separate implementations; another project can implement the public contracts and register its provider without changing shared persistence. Aspire adapters remain separate and optional. See the [implementation guide](quickstart.md).

## Exact accepted candidate

[Run 38822519090](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38822519090), attempt 1, passed all **63 jobs** at head `bcfcf6b630bba0cc95c18e555e9e008f1c539379`. Tested merge `dcbbcdc222aa19456246745e67a2a0b54240373d` has parents main `38eb41e06d40b6addbe57e3af839f912645e6dea` and that head. Head and tested merge share tree `d34a69bedd15173f11d36d4147b1b5d6cbbf060b`; the delivered squash has that same tree. The final gate explicitly accepted full scope. Fresh remote checks found unchanged main, 63 successful exact-head check runs, a clean merge and no outstanding submitted review or restriction to bypass.

## Database preservation and conformance

All **44** generated migration/designer/snapshot files remain byte-identical; all 20 migration IDs, existing assembly names, schemas and history tables are preserved. No migration was added and no production database was operated on.

Raw unit reports pass **525/525**: Core 25, Host 430, Architecture 5 and Harness 65. Each real provider passes **20/20**: Persistence 5, Foundation 8 and BffSessions 7, with zero failures/skips/errors. Independent review reconciles exact unique result/definition/execution bindings. The new frozen compatibility case passes in 11.033 seconds on PostgreSQL and 11.678 seconds on SQL Server.

The old producer is separately compiled against frozen main `38eb41e`, populates the disposable database with native/product data, and supplies the same physical database to the new migrator twice. Complete schema/history/data fingerprints, models and physical identity remain unchanged. Native manager operations, ordinal lookup, both runtime readiness boundaries, qualified runtime permission denials, nested connection/timeout restoration, lock contention, commit and rollback pass on both providers.

Each retained baseline manifest identifies 77 executable files and the frozen commit, including old Core/Infrastructure/Migrator and both migration assemblies; no new Database.* module appears. The test verifies the complete bounded manifest before launching the producer. The ZIP retains hashes, not those 77 binary files, so independent review verifies the source-bound receipt and passing case rather than independently rehashing unavailable binaries. This does not qualify a third database engine.

## Coverage and security evidence

All **48** required coverage inputs are present under A-007. All **15** production modules pass the unchanged 90% line/80% branch floors: nine existing modules plus six new provider modules. Only frozen producer binaries are additionally excluded; current provider implementations remain covered. Aggregate XML SHA-256: `59b21e82d5840704fe80e11bc8a4ab5c40f7775600c6b710123d4a5265e860a1`.

| Module | Line coverage | Branch coverage |
| --- | ---: | ---: |
| IdentityPlatform.Infrastructure | 94.58% | 98.06% |
| IdentityPlatform.AppHost | 99.35% | 100.00% |
| IdentityPlatform.Database.Abstractions | 99.11% | 100.00% |
| IdentityPlatform.Database.PostgreSql | 100.00% | 100.00% |
| IdentityPlatform.Database.SqlServer | 100.00% | 100.00% |
| IdentityPlatform.Database.Hosting | 95.45% | 100.00% |
| IdentityPlatform.Database.PostgreSql.Hosting | 100.00% | 100.00% |
| IdentityPlatform.Database.SqlServer.Hosting | 100.00% | 100.00% |
| IdentityPlatform.Migrator | 98.80% | 100.00% |
| IdentityPlatform.Worker | 100.00% | 100.00% |
| IdentityPlatform.Admin.Api | 100.00% | 100.00% |
| IdentityPlatform.AuthServer | 97.33% | 100.00% |
| IdentityPlatform.ServiceDefaults | 100.00% | 100.00% |
| IdentityPlatform.Admin.Bff | 94.96% | 95.40% |
| IdentityPlatform.Core | 98.91% | 96.15% |

Independent security review reconciles all six mutation archives: **42 kills, 42 baseline passes and 42 restored passes**, zero skips and source restored throughout. Run/head/tested-merge bindings, original/recomputed mutant hashes and exact native cases agree with the unchanged manifests/verifier. These sanitized receipts do not contain raw mutation TRX; the unchanged verifier enforces native exit/marker/stack and discovery-restoration checks. Existing security mutations complement the separate provider-conformance cases and do not close historical findings.

Architecture/test, security and Aspire source reviews resolved the findings recorded in [verification](verification.md). Final unit/native/coverage and mutation evidence reviews pass. Earlier failed runs and their corrections remain recorded; they are not acceptance evidence.

| Artifact | ID | ZIP SHA-256 |
| --- | --- | --- |
| critical-mutations-PostgreSql | 12012809585 | `20d6827606e71958588c78e4a6c4b00de93ab1b75919d3d7b3f8b631acca87ee` |
| critical-mutations-SqlServer | 12014028499 | `0faf011a369afa219616097c9f1dd5246b4de7394fc3fde0c3c6d937bc8b1615` |
| administrator-mutations-SqlServer | 12013844227 | `86fc2e82f0141b5a723609a41b61e867a97ddbef9797b31954e7a95f8b64bbb4` |
| administrator-mutations-PostgreSql | 12013749607 | `ca4518ca87f5167c6bcbed969c1965bc62a770ad0971a361071c6ca3437d55c6` |
| unit | 12013049018 | `80b09d1a54af466ec5aec71357955197d25ec47cd20ce2340fabd02ef172bb70` |
| foundation-SqlServer | 12014515680 | `512d8ca6d1e4012f0b14a48b3b432e45d9046b06a8973196201c32d491ddd4ce` |
| foundation-PostgreSql | 12013779043 | `32b83d296506f49901b4c3f0f8b34763f7c5439761665c5d29e0bb14946d2e39` |
| foundation-coverage | 12013804437 | `3d4402b164e50653b47cc31d43dad7a189ad68747989deb6221c4d9051537e91` |
| governance-mutations-PostgreSql | 12013084070 | `a7a0195163fe8f24e861eb8be230a2e541d9fe8fa683d00fa7fdeb52337968f3` |
| governance-mutations-SqlServer | 12014305461 | `1b2588ab741d104a93da003fd5ca806c615668bf52a46ba3f6967b2a91d15596` |

## Continuation and limits

Feature 012 T001–T008 is delivered; the prose record has its own proportional exact-head checks and normal squash, recorded on its PR. Feature 011 T005 resumes at the native store ownership prerequisite. PR #57 already delivered the four installed-manager characterization controls as `38eb41e`; do not repeat them. The next executable work binds native store calls/enumerations to one original deadline and an exact scoped context/transaction, then proves actual manager cancellation and rollback on both providers and both logical stores. Dispatcher/event ordering, early durable consumption, quotas and issuance commit remain part of T005 and the complete B2 path.

No B2 acceptance, product browser/API activation or production release is claimed. A-007 defers Firefox only through internal B2 and requires restoration before B3 implementation/activation/release. All six historical findings, 48 human assessment rows and external/public-release gates remain open. A-004 remains expired; A-005/A-006 concluded. Preserve the diagnostic branches and their receipts.
