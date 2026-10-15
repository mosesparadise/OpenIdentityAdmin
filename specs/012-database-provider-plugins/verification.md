# Verification

Baseline: main `38eb41e06d40b6addbe57e3af839f912645e6dea`; remote reverified 15 October 2026. Implementation is complete locally; real database/full CI acceptance and delivery remain pending. No production database operation has run.

## Scope and traceability

FR-001 → `ExternalProviderTests` arbitrary public-contract registration, incompatible EF/native connections, exact-instance/model isolation and invalid catalogue controls. FR-002–005 → unchanged generated migration hashes, existing model parity tests, frozen-process same-database upgrade, exact schema/data/history fingerprints, runtime grants, governed transaction contention/commit/rollback, timeout restoration and readiness. FR-006 → architecture and four Aspire adapter identity/provisioning controls. FR-007 → reusable contract/native/compatibility helpers and exact persistence discovery. FR-008 → compiling contract implementation guide and delivery handoff. Existing mandatory suites remain applicable under A-007.

## Local execution

SDK10.0.401 (repository roll-forward), Linux. Full solution build passed with zero warnings/errors. Disk capacity required disabling local dependency/browser copies (`CopyLocalLockFileAssemblies=false`, `CopyLocalProjectReference=false`, `PlaywrightPlatform=none`); package probing and copies of each project's own freshly built assemblies supported execution. These options do not modify CI or establish browser evidence.

- Host: 430/430 passed, zero failures/skips. Includes 26 new provider/manifest/cancellation cases and existing model parity for both engines.
- AppHost Harness: 65/65 passed, including four new hosting catalogue/identity cases.
- Core: 25/25 passed.
- Architecture: 5/5 passed. Initial execution from an external symlink target could not locate the repository; copying that same build into the repository restored the expected fixture layout and all five passed.
- Python issuer selection: 9/9; persistence selection: 5/5.
- Roslyn source-quality check: PASS; 66 obsolete exact legacy entries retired, no new allowance/exception. Existing debt remains tracked.
- All 44 generated migration/designer/snapshot SHA256 values match the frozen baseline.
- Frozen producer restored with its separate locked graph and compiled against the verified detached baseline. Its dependency graph contains none of the new provider modules. The process has not populated a database locally.

The first full Host run found two existing account connection timeout assertions failing (native connection changed, EF cached string did not). `AccountDatabaseBounds` now synchronizes the bounded string through EF; the corrected full 430-case run passes. An initial model invocation used stale copied local DLLs; selecting each assembly's own build corrected the harness. These initial failures are not counted as acceptance.

## Independent review dispositions

Architecture/test design: PASS after exact registration/model-cache identity, direct public-contract external fixture and frozen-baseline compatibility clarifications. Source review corrected native connection validation before context use, fixture-owned physical database binding, complete artifact manifests, schema constraint/index metadata, and actual reusable transaction contention. Its final stale Python count was corrected (4→5 Persistence) and the nine selection controls pass.

Security: scoped source PASS after context/store rejection, entry/pre-launch cancellation checks, independent bounded child cleanup, early manifest-size bound, bounded link-rejecting file enumeration and isolated negative controls. Existing runtime ownership, native permission error codes and safe credentials remain preserved. Source review does not establish real database outcomes.

Aspire: design/source review resolved exact returned-provider instance validation and unsupported provisioning preflight. Four new adapter controls pass locally. Native lifecycle/credential behavior still requires the ordinary CI partitions.

## Required remaining evidence

Run the exact published head through all required CI jobs. PostgreSQL and SQL Server foundation jobs build the old producer, retain its complete file manifest, populate disposable databases with native managers, then run the new migrator twice on those same physical databases. Require schema/history/all-row fingerprints unchanged, real runtime manager/readiness/grant controls, nested timeout/connection restoration and governed contention/commit/rollback. Persistence discovery is exact at five cases/provider. Unit inventory is 525 (430 Host,65 Harness,25 Core,5 Architecture).

Coverage retains all nine original floors and adds the six new production modules at90% lines/80% branches. Only the frozen producer output is excluded; no current provider is excluded. All48 coverage inputs, non-Firefox browsers and mutation controls remain required. No full-run pass, real-database preservation, merge, release readiness or closure of the six historical findings/48 human rows is claimed here.

## First CI correction

PR58 head `578d5ff` started run38820424321. Required jobs exposed stale consumers of the moved image declarations and the increased persistence inventory. Image preparation now reads each explicit hosting adapter and requires its single named image constants plus the original bounded pull/digest inspection. Lifecycle totals increase by exactly the new case/provider. All197 Python seeded controls pass after these corrections. This failed run supplies no native acceptance.

## Second CI correction

Head `132e12d`, run38820632168, reported the new hosting projects resolving vulnerable MessagePack2.5.302 and Persistence discovery rejecting path-dependent UIDs before database execution. The shared hosting project now carries the same existing2.5.307 pin as AppHost; all eight affected graphs resolve2.5.307. A fresh local advisory report covers all30 projects with no known vulnerable package reported;257 package/version license pairs pass existing policy. No suppression or new license exception was introduced.

Persistence now reuses the existing schema2 portable native inventory validator: exact names, full native type and all traits, followed by exact run-local UID/metadata equality between full and selected discovery. TRX remains bound by exact selected names/counts and its own unique internal IDs. The10 reviewed names/provider assignments are unchanged. Independent review accepts this correction; relocation-positive/stale-selection-negative controls pass. The moved image constants also have independent missing/duplicate/change controls.

SDK formatting removed obsolete imports left by the test compatibility aliases; aliases are generated per test project using ordinary SDK Using items, so linked source does not carry aliases unused in some consumers. Current full solution build, style verification and source-quality checks pass; all199 Python controls pass. These are corrections awaiting exact-head hosted verification; they do not establish real database preservation.

## First native compatibility execution

Head `15e3276`, run38821283045: unit job passed all525 cases. Each real-provider Persistence run passed the four retained cases and reached governed contention in the new case, after frozen-baseline schema/history/data preservation, runtime managers/readiness, native permission denials and nested connection/timeout restoration. The new case failed because both EF non-retrying execution strategies wrap native lock-timeout errors in `InvalidOperationException`; the observed inner errors were PostgreSQL55P03 and SQL Server lock timeout. The corrected test permits only direct `DbException` or exact `InvalidOperationException` with an immediate `DbException`, then retains exact engine type/code checks (55P03/1222). Independent review accepts that narrow assertion correction. Corrected native execution remains required.

The added image-constant control also required the existing exact runner count to increase6→7; unittest reported7 successes but correctly returned failure until that inventory was reconciled. Its direct command now exits0. No runtime, retry, timeout or acceptance criterion changed.

## Reusable fingerprint boundary

Final API review identified that the public preservation helper itself assumed `information_schema`. It now takes a fixture-owned physical fingerprint callback; built-in PostgreSQL/SQL Server tests supply their unchanged capture implementation. The callback runs before provider configuration and after both upgrades, requires a nonblank initial fingerprint and exact final equality. Physical identity, complete migration history and model checks remain shared. The guide specifies independent complete schema/history/data inspection. Independent source review passes; the corrected test project builds cleanly and the quality gate passes. This adds no production interface or third supported engine.
