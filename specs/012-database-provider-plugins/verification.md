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
