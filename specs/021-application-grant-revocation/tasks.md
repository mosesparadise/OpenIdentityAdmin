# Tasks: Managed application grant revocation

## Phase 1: Setup and design

- [x] T001 Record both selected scope decisions in docs/approval.md and finalize specs/021-application-grant-revocation/{spec,plan,research,data-model,contracts/revocation,quickstart}.md; obtain independent design review before affected implementation.

## Phase 2: Shared foundation

- [x] T002 Add finite input/state/failure/version rules in src/core/IdentityPlatform.Core/Features/ApplicationGrants/ and test strict canonical validation in tests/core/IdentityPlatform.Core.Tests/Features/ApplicationGrants/; preserve existing identifiers and finite reasons.
- [x] T003 Add job/approval/audit entities and relational mappings in src/persistence/IdentityPlatform.Infrastructure/Features/ApplicationGrants/: positive revisions, nonnegative counts, ordered UTC timestamps, finite state/failure/action/reason, actor max450, application max existing identifier limit, request hash64, scope digest64 unique key and required restricted FKs per data-model.md.
- [x] T004 Generate additive Platform migrations in src/migrations/IdentityPlatform.Migrations.PostgreSql/Platform/ and src/migrations/IdentityPlatform.Migrations.SqlServer/Platform/; prove fresh/populated history/data preservation and actual relational constraints in tests/persistence/IdentityPlatform.Persistence.Tests/Features/ProviderCompatibility/.

## Phase 3: US1 explicit revocation and visible progress (P1)

Independent test: genuinely admit an eligible disabled target, complete native batches,
read truthful protected status and verify peer/configuration isolation.

- [x] T005 [US1] Add both-provider admission/selection/atomicity cases in tests/aspire/IdentityPlatform.Administrator.Tests/Features/ApplicationGrants/ for independent native authorization/token families, empty/eight/nine sets, invalid states, protected IDs and target/version errors.
- [x] T006 [US1] Implement target, job admission and audit staging in src/persistence/IdentityPlatform.Infrastructure/Features/ApplicationGrants/; exact actor/action/target key/hash binding for24hours, disabled/non-system target, one incomplete job, fresh persisted verification and no automatic re-enable.
- [x] T007 [US1] Implement bounded native batch selection/revocation and atomic job progress in src/persistence/IdentityPlatform.Infrastructure/Features/ApplicationGrants/; at most8authorizations and8tokens, false native result aborts, fresh native verification, current authority before commit and empty-set completion audit.
- [ ] T008 [US1] Add independent current permission/MFA admission, protected status/latest10reads and versioned contracts in src/backend/IdentityPlatform.Admin.Api/Features/ApplicationGrants/; preserve original request/publication deadlines and native cleanup.
- [x] T009 [US1] Enforce incomplete-job re-enable conflict in src/persistence/IdentityPlatform.Infrastructure/Features/Applications/ApplicationMutationWriter.cs; test both issuance/re-enable/acceptance commit orders and actual provider lock waits in Administrator ApplicationGrants tests.
- [ ] T010 [US1] Add exact BFF routes/CSRF/key forwarding and generated strict contracts in src/backend/IdentityPlatform.Admin.Bff/Features/ApplicationGrants/ and frontend generated contracts; verify actual HTTP negatives, secrecy and compatibility in tests/contracts/IdentityPlatform.Api.Contracts.Tests/ and Administrator tests.
- [x] T011 [US1] Add confirmation/progress/finite-error UI in src/frontend/IdentityPlatform.Admin.Ui/Features/Applications/Editor/ with current permission visibility, explicit offline limits, read-only polling/navigation cancellation, light/dark/focus and UI tests.

## Phase 4: US2 durable continuation (P1)

Independent test: accepted multi-batch job continues after logout/MFA expiry and actual
worker restart; lost current authority stops the next batch with ordered races.

- [ ] T012 [US2] Implement metadata-only current actor/permission eligibility and post-save rereads in Infrastructure Features/ApplicationGrants/; test logout/MFA continuation and disabled/deleted/suspended/locked/factor-generation/permission loss in Administrator ApplicationGrants tests.
- [x] T013 [US2] Compose bounded worker polling and safe telemetry in src/backend/IdentityPlatform.Worker/Features/ApplicationGrants/ and Features/Setup.cs; use native owned store lifetime, no factor keys, actual two-worker contention/restart and startup/shutdown coverage in Host/Administrator tests.
- [x] T014 [US2] Prove actual native entry validation separately from revision/wrong-audience denial in tests/aspire/IdentityPlatform.Administrator.Tests/Features/ApplicationGrants/ and genuine cross-instance issuer flows with unaffected controls, measured from each committed batch.

## Phase 5: US3 explicit recovery (P2)

Independent test: fail a later batch, read earlier progress, freshly approve the same
remaining scope and complete without silent revival or duplicate work.

- [ ] T015 [US3] Implement serialized stopped outcomes, fresh approved continuation and unknown-commit reconciliation in Infrastructure/Admin.Api ApplicationGrants features; tests cover lost acknowledgements, native-save/audit faults, cancellation/deadlines, same-key replay/conflict/expiry, effective-approver change, processing beyond24hours, late-stop versus progress/new approval/success, unrecordable failure and missing actor lock records.
- [ ] T016 [US3] Connect BFF/UI recovery to exact job/application versions in their ApplicationGrants/Editor features; verify current authorized status, explicit recovery and stale-key/version handling via actual HTTP and browser journeys.

## Phase 6: Integrated acceptance and delivery

- [x] T017 Exercise actual Worker through ordinary Aspire local-demo on both providers and retained Chromium/WebKit; extend tests/aspire native demo/browser fixtures, eng test inventories and behavioral mutation/coverage controls without losing prior identities or lowering floors.
- [ ] T018 Run full applicable exact-head checks, reconcile native reports/artifacts/coverage, resolve independent architecture/security/test/API/UX/Aspire findings, and record actual evidence in specs/021-application-grant-revocation/verification.md; update docs/roadmap.md and docs/operations/new-task-handoff.md before reviewed squash delivery.

## Current boundary verification

[Run531 receipts](run531-evidence.json) at `0a5fc76` reconcile all 124 successful jobs,
50 grant cases per provider, both native demos and four retained protection-browser cells,
110 mutation kills and all 15 unchanged coverage floors. Independent task review closes
T007 (native batches), T011 (confirmation/progress UI), T013 (Worker) and T017 (integrated
Aspire/browser/mutation/coverage execution). T001–T004 were already complete.

[Run533 receipts](run533-evidence.json) at `da6f378` independently reconcile 51 grant
and 64 managed-issuance cases per provider (230 passes, zero failures/skips in these four
reports). Four admission/re-enable cases, all eight extended genuine issuance-race cases
and both commit-timed cross-instance cases pass. T009 and T014 are complete on that
focused evidence; full CI remains rejected for the separate import and native replay failures.

[Run534 receipts](run534-evidence.json) at `2a18e68` verify all 124successful jobs and
364 selected native passes. Both-provider HTTP target negatives and the valid control
pass. Administrator persisted-ID protection remains open. The earlier replay failure is
now reproduced by a controlled real PostgreSQL ordering test. A pilot-only repeated-replay
cleanup correction is implemented with exact native bindings, native rejection, independent
authorization and failure controls; current-head integrated acceptance remains pending.
This bounded correction does not close the initial-replay/late-issuance window.

[Run535 receipts](run535-evidence.json) at `bdad762` verify124successful jobs and 586
selected native passes. Both controlled and original replay cases pass on both providers;
the initial replay/late-issuance window remains open. The remaining administrator-ID target
case now passes on PostgreSQL and SQL Server ([receipts](protected-target-evidence.json)).
It isolates the production target guard under real owned locks, proves exact Forbidden
without name/authentication masking, preserves state, restores the profile and admits
the same intent. This closes T005/T006 with the previously verified admission/atomicity
and real HTTP negatives. Twelve of 18 tasks are complete; full feature acceptance is not.

Implemented work is not repeated. Remaining acceptance evidence is specific:

- T008: fresh MFA/permission admission, original publication/deadline/cleanup, latest-ten ordering.
- T010: downstream malformed/failed/late responses and changed durable browser generation.
- T012: deleted actor and ordered permission loss/final authority change versus batch commit.
- T015: elapsed request/transaction deadlines and actual ordered late-failure capture.
- T016: genuine stopped-job continuation and unknown-acceptance recovery in the browser.
- T018: verify the bounded replay correction at the final head, retain the initial-replay/late-issuance window as unresolved, then complete-spec review and exact-head delivery after the remaining obligations.

See [hosted verification](hosted-verification.md) for prior failed attempts and review dispositions.

## Dependencies and execution

T001 precedes code; T002–T004 establish shared storage. T005 specifies cases before
T006/T007. T012 authority is a prerequisite of executable T007, and T013 composes it;
US1 and US2 are independently testable outcomes sharing the same safe processor. T008
requires T006, T010 requires T008, T011 requires T010. T009 must exist before activating
acceptance. T015 requires T006/T007/T012; T016 requires T010/T011/T015. T014 and T017/018
complete acceptance, not optional hardening. Root owns edits; independent reviewers may
inspect API/UX and test evidence in parallel. No parallel .NET builds/provider runtimes.

## Requirement and control traceability

| Requirements / approved controls | Tasks | Planned evidence |
| --- | --- | --- |
| GR-001–004; FR-01/06/11 | T002,005,006,008,009,010,011 | Admission/MFA/version/CSRF/protected-target and disabled lifecycle scenarios |
| GR-005/008/014; FR-09/10/12, NFR-05 | T003,004,005,007,009,014 | Native states/atomic audit, entry validation, cross-instance/race/upgrade cases |
| GR-006/007/012; FR-06/11/12/17 | T006,012,013,015,016 | Immutable approvals, current effective actor, logout/expiry/restart/explicit recovery |
| GR-009/010; FR-12/17/18 | T006,007,013,015 | Two-worker, commit ambiguity, scoped idempotency and actual failure rollback |
| GR-011/013/015; FR-10/16/18 | T008,010,011,016,017 | Current protected metadata, generated contracts, real UI/Aspire/browser behavior |
| All; constitution13/18/23 | T018 | Exact IDs, invariant links, reviewer dispositions and full applicable acceptance |

Exact discovered test IDs and outcomes are added as tests are implemented; planned
scenario coverage is not runtime evidence. All external/historical gates remain open.
