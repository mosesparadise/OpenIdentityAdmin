# Edit/replacement atomicity checkpoint — 4 November 2026

T015 extends the already implemented T006 mutation coordinator with decisive provider tests. Production code, migrations, generated contracts, packages and UI are unchanged. This is a component verification checkpoint on `123-application-editor`; actual PUT/replacement endpoints and visible forms remain T016/T017. The previous create/details UI remains available through native Aspire.

## Requirements and scenarios

FR-005–008/010 require current authority, one aggregate version, native-manager ownership, exact credential/configuration state and atomic audit. Existing tests retain basic lifecycle, permission, MFA, system binding, post-commit ambiguity and failure coverage. Eight new cases per provider close narrower evidence gaps:

| Cases per provider | Added proof |
| --- | --- |
| Four concurrent pairs: edit/edit, replace/replace, edit/replace, replace/edit | Two independent API service roots enter actual database contention using the same version. Distinct fields or expiries identify the winner. Exactly one 200 and one 412, with no losing details/version/secret. |
| Edit and replacement, failure after native save | Detached queries inside the transaction witness the requested field/hash/lifecycle changes before throwing the exact retained synthetic exception. Fresh post-disposal snapshots prove complete rollback. |
| Edit and replacement, real audit insertion denial | The disposable database denies INSERT on SecurityAuditIntent. The preceding native change is witnessed. PostgreSQL 42501 or SQL Server 229 must be the actual failure. Permissions restore under independent bounded cleanup, and the original version still succeeds. |

The race uses a held governance singleton and provider wait-graph observation of both runtime requests. The independent peer warms with a deliberately stale command before the held-lock window; the entire aggregate remains unchanged. Production operation deadlines remain unchanged. Cleanup releases the blocker and joins both operations even after observation failure. These races prove overlapping admissions and one aggregate winner; they do not force both possible mixed-operation winner orders or prove multi-process/HTTP behavior.

The snapshot covers every scalar native application property, all ApplicationPolicy fields and every complete ordered application audit row. Secret-bearing state is compared by SHA-256, never printed. Separate assertions verify exact submitted edit fields or replacement lifecycle, credential hash preservation/change, native old/new-secret acceptance, unchanged fields, the native concurrency token and one aggregate revision advance. Audits retain all original rows and add exactly one event with the expected actor, target, outcome, controlled UTC time and caller-known correlation. A later stale command cannot change the committed snapshot.

## Runtime evidence and review

[Evidence manifest](update-atomicity-evidence.json) binds source, exact discovered identities, final reports and failures. The prior 2,323 administrator metadata records and provider order remain unchanged; 16 cases are added for 2,339. Both complete Applications selections pass **44 cases per provider**, with zero failures/skips. All **67 selection controls** also pass. The focused PostgreSQL 8-case run is supplementary; complete final selections determine runtime acceptance.

Architecture/security and test source reviews pass after requiring actual requested effects in the pre-failure witness, exact exception identity, peer warm-up and cancellation-safe audit permission restoration. Independent final raw-evidence review passes for this qualified scope: 88 exact passing provider executions, 9 source and 22 artifact hashes, preserved metadata/order and the explicitly failed global quality gate. The five C# test/helper files introduce no new code-quality findings. There is no new Host/frontend/browser execution, C# coverage measurement or dedicated mutation-testing claim; unchanged implementation evidence remains linked explicitly.

## Existing quality blocker

The global code-quality check **fails**, independently of these additions. The unchanged CQ008 partial-class exception for `tests/aspire/IdentityPlatform.Administrator.Tests/Features/Bootstrap/BootstrapTests.cs` expired on **2 November 2026**. Its exact finding fingerprint and all other findings remain unchanged. The allowance is neither renewed nor removed, and the check is not counted as passed. Feature009 engineering T015 owns consolidation; feature019 T022 delivery must resolve this blocker before full verification/merge. The evidence manifest records the failed receipts and dated disposition.

## Scope and continuation

The component fixture uses genuine account enrollment/session and native lifecycle rows, but constructs ApiIdentity directly; it does not prove HTTP bearer/CSRF boundaries for the new operations. T016 must implement and verify actual If-Match PUT and explicit credential-replacement API/BFF endpoints. T017 adds the forms and issuer-change journeys. T007/T014 retained-browser/native-demo acceptance, T019 browser work, T022/T023 full feature verification/review/squash and all historical/human/Windows/Firefox/A-009/vendor/production/public-release gates remain open. No blocked browser download, expired diagnostic approval, refresh activation or full CI/PR/merge is involved.
