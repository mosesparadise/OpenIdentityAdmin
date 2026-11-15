# Tasks: Application cloning and permanent retirement

Input: spec.md, plan.md, research.md, data-model.md and contracts/management.md.
Tests are mandatory under FR-013 and the constitution. Each checkbox represents completed
work with evidence, not a promise. Clone and retirement have separate delivery checkpoints.

## Phase 1 — Setup
- [x] T001 Record selected permanent-retirement semantics and bounded M04-A scope in specs/025-application-clone-retirement/spec.md.
- [x] T002 Investigate current native references, ownership, permissions and test seams in specs/025-application-clone-retirement/research.md.
- [x] T003 Record independent design reviews, requirement analysis and existing user authorization in specs/025-application-clone-retirement/analysis.md and docs/approval.md.

## Phase 2 — Foundations
- [x] T004 Verify pinned build/restore and existing ignore configuration using global.json, Directory.Packages.props and .gitignore; record actual environment/results in specs/025-application-clone-retirement/verification.md.

## Phase 3 — US1: Clone (FR-001..005, FR-012..014; SC-001/004/005)
Goal: independently create, enable and sign in to a disabled-by-default clone with its own
credential/consent, unchanged source and current copied configuration.
- [x] T005 [US1] Add behavioral clone intent/JSON/precondition cases in tests/backend/IdentityPlatform.Host.Tests/Features/Applications/ApplicationCloneTests.cs and contract assertions in tests/contracts/IdentityPlatform.Api.Contracts.Tests/Features/Generation/.
- [x] T006 [US1] Add real-provider clone isolation, protected/duplicate/stale/current-MFA, save/audit rollback and actual wait-observed source/catalogue race cases in tests/aspire/IdentityPlatform.Administrator.Tests/Features/Applications/.
- [x] T007 [US1] Implement confirmed new-ID/name/future-expiry intent in src/core/IdentityPlatform.Core/Features/Applications/ApplicationClone.cs and immutable command support in src/persistence/IdentityPlatform.Infrastructure/Features/Applications/ApplicationMutationCommand.cs.
- [x] T008 [US1] Implement allowlisted source snapshot and shared native creation in src/persistence/IdentityPlatform.Infrastructure/Features/Applications/; enforce revision=1, credentialRevision=1, enabled=false, fresh secret and current source/catalogue verification after every save, under one original clone authority receipt; accept eligible catalogue edits before snapshot acquisition.
- [x] T009 [US1] Add immutable source-ID/version audit detail and exact persisted verification in src/persistence/IdentityPlatform.Infrastructure/Features/Applications/; generate additive migrations in both existing provider projects and verify populated upgrades.
- [x] T010 [US1] Add clone API route, fresh-MFA write authority and bounded 201 publication in src/backend/IdentityPlatform.Admin.Api/Features/Applications/.
- [x] T011 [US1] Add fixed one-send clone proxy, strict request and destination-bound response validation (new/source ID inequality, revision 1, disabled, exact client ID/name/expiry/profile) in src/backend/IdentityPlatform.Admin.Bff/Features/Applications/.
- [x] T012 [US1] Add reviewed clone form, matching source detail/scope version, one-time output/focus/unknown-outcome behavior in src/frontend/IdentityPlatform.Admin.Ui/Features/Applications/Editor/ and its tests/.
- [ ] T013 [US1] Add genuine clone/new-consent/native sign-in journeys for both providers and Chromium/WebKit in tests/aspire/IdentityPlatform.AppHost.Tests/Features/Samples/.
- [x] T014 [US1] Regenerate authoritative contracts and extend exact native/UI inventories and clone causal witnesses in eng/, scripts/ and tests/; preserve old identities and unchanged floors/deadlines.
- [ ] T015 [US1] Run focused and required exact-head checks, independent architecture/security/API/test/UX reviews and original evidence reconciliation; record clone acceptance and normal squash in specs/025-application-clone-retirement/verification.md and docs/roadmap.md.

## Phase 4 — US2: Permanent retirement (FR-001/006..009/011..014; SC-002/004/005)
Goal: atomic terminal state, new durable approval/job and retained history/identity.
- [ ] T016 [US2] Add both-provider retirement admission, replay/collision, atomicity, current dual-authority/MFA and actual mutation/issuance race tests in tests/aspire/IdentityPlatform.Administrator.Tests/Features/Applications/.
- [ ] T017 [US2] Add non-null Retired default=false concurrency state with invariant retired implies enabled=false, and immutable receipt/audit/job linkage in src/persistence/IdentityPlatform.Infrastructure/Features/Applications/ and both provider migrations; prove populated upgrade/history preservation.
- [ ] T018 [US2] Implement atomic retirement and separately scoped exact replay with a new grant approval in src/persistence/IdentityPlatform.Infrastructure/Features/Applications/; reject every incomplete existing job and preserve all rows on failure.
- [ ] T019 [US2] Bind terminal state into issuer/write snapshots and reject ordinary mutations/clone sources in src/persistence/IdentityPlatform.Infrastructure/Features/Applications/ and existing managed registration/login features, preserving GrantTarget cleanup access.
- [ ] T020 [US2] Add dual-permission fresh-MFA API/BFF retirement admission under one authority epoch, bounded receipt/status contract and exact validators in src/backend/IdentityPlatform.Admin.Api/Features/Applications/ and src/backend/IdentityPlatform.Admin.Bff/Features/Applications/.
- [ ] T021 [US2] Filter retired policies before ordering/offset/lookahead in src/backend/IdentityPlatform.Admin.Api/Features/Applications/ApplicationListReader.cs; add the separately navigable bounded retired-app list and retain authorized detail/job access; verify mixed unmanaged/system rows, page boundaries and historical native ID retired (the separate /retired-applications route must not shadow detail IDs).
- [ ] T022 [US2] Add permanent-retirement confirmation, terminal details/status navigation and offline-token/session explanation in src/frontend/IdentityPlatform.Admin.Ui/Features/Applications/; cover stale/denied/cancelled/unknown outcomes and responsive keyboard/focus behavior.
- [ ] T023 [US2] Prove retained native/history rows, client-ID reservation, scope-reference semantics and retirement persistence through restart/cleanup in tests/aspire/IdentityPlatform.Administrator.Tests/Features/Applications/.

## Phase 5 — US3: Recovery (FR-007/009/010/012..014; SC-003/004/005)
Goal: durable revocation survives browser loss and explicit stopped-job recovery without
ever restoring a retired registration. Depends on US2 admission and terminal read model.
- [ ] T024 [US3] Add worker restart/logout/MFA-age/current-account/permission-loss, exact continuation and retained approval/history tests in tests/aspire/IdentityPlatform.Administrator.Tests/Features/ApplicationGrants/.
- [ ] T025 [US3] Integrate retirement receipt/job observation and existing explicitly approved continuation in src/backend/IdentityPlatform.Admin.Api/Features/ApplicationGrants/, src/backend/IdentityPlatform.Admin.Bff/Features/ApplicationGrants/ and src/frontend/IdentityPlatform.Admin.Ui/Features/Applications/Editor/Grants/.
- [ ] T026 [US3] Add genuine retirement/pending/stopped/recovered journeys across both providers and retained browsers in tests/aspire/IdentityPlatform.Administrator.Tests/Features/Browser/; assert recovery stays reachable after normal-list exclusion, full reload/re-login and lost admission response.

## Phase 6 — Retirement delivery and cross-cutting verification
- [ ] T027 Regenerate retirement contracts, inventories and invariant-specific causal witnesses in eng/, scripts/ and tests/; preserve existing exact identities, assertions, deadlines and coverage floors.
- [ ] T028 Run all required exact-head provider/browser/contract/coverage/mutation/supply checks and independent architecture/security/API/test/UX/Aspire reviews; reconcile original artifacts in specs/025-application-clone-retirement/verification.md.
- [ ] T029 Record reviewed normal squash and completed/remaining scope in specs/025-application-clone-retirement/tasks.md, docs/roadmap.md and existing handoff documentation; preserve Windows/manual, Firefox/A-009, historical and external gates.

## Dependencies and parallel opportunities
T001..004 precede code. US1 tests/contracts precede implementation; T008/009 share audit/
creation files and run sequentially. T010 then T011 then T012/013 integrate the actual flow.
T015 delivers clone before US2. US2 schema/admission/guards precede UI and US3 recovery.
Independent read-only architecture and test reviews can run in parallel for each story;
main agent owns source edits. Provider executions may run independently using established
isolation and resource limits. Do not parallelize overlapping mutations or artifact writes.

## Traceability
FR-001 T006/008/016/019; FR-002 T005..009/012/013; FR-003 T006/008/010..013;
FR-004 T006/008; FR-005 T005/006/010/011; FR-006 T016..020/022;
FR-007 T016..018/024; FR-008 T017/019/023; FR-009 T020..023/025/026;
FR-010 T024..026; FR-011 T016/019/023; FR-012 T011..013/020/022/025/026;
FR-013 T004..006/009/013..017/021/023/024/026..028; FR-014 T015/028/029.
SC-001 T006/013/015; SC-002 T016..023; SC-003 T024..026;
SC-004 T014/015/027/028; SC-005 T015/028/029.
