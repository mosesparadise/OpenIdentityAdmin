# Tasks: Durable administrator session foundation

All tasks below belong to feature 010, not feature 008 or 009's identically numbered tasks. A checked task means its stated implementation or evidence is recorded, not whole-feature acceptance. Both-provider and exact-head acceptance tasks remain open separately.

## Phase 1 — Scope and dependency preparation

- [x] T001 Resolve current delivery/authority and B1–E dependencies in `research.md` and `plan.md`; preserve PR #50 and release blockers. Requirements: FR-008, SC-005.
- [x] T002 Specify stories, bounded data/operations and requirement-quality review in `spec.md`, `data-model.md`, `contracts/ticket-store.md` and `checklists/`; record independent dispositions in `verification.md`. Requirements: FR-001–011.
- [x] T003 Deliver the planning packet and current roadmap/handoff through required prose checks and normal squash; record exact head, review and CI in `verification.md`. Requirements: SC-005 (planning only).

## Phase 2 — Implementation prerequisites

- [x] T004 Recheck remote main, PR #50, the expired shared allowance and current scope before source changes; record executable-delivery dependency in `verification.md`. Do not renew the exception or repeat its existing refactor. Requirements: SC-005.
- [x] T005 Inventory actual affected source, native identities, fixture ownership, shared key-protection extraction and all coverage consumers in an implementation section of `plan.md`; independently review the exact implementation design before coding. Requirements: FR-003, FR-007, FR-010.

## Phase 3 — US1: Protected durable state

- [x] T006 [US1] Add `AdminSessionTicket`/`AdminSessionAudit` and mappings under `src/persistence/IdentityPlatform.Infrastructure/Features/Sessions/Persistence/`: 64-character canonical digest; nonempty bindings at most 400 characters; 64 KiB native payload/96 KiB protected-envelope limits; positive 64-bit revision; UTC timestamps and documented indexes. Requirements: FR-001–003, FR-007, FR-009.
- [x] T007 [US1] Generate additive migrations/snapshots under both existing provider `Sessions/` directories and update `src/persistence/IdentityPlatform.Infrastructure/Features/Health/SchemaCatalog.cs`; preserve native OpenIddict tables and session-only grants. Requirements: FR-003, FR-007.
- [x] T008 [US1] Implement separate BFF ring/application/purpose ownership in `src/backend/IdentityPlatform.Admin.Bff/Features/Sessions/`; extract only stable shared protection mechanics into `Infrastructure/Features/Protection/` if needed, retaining existing issuer behavior and tests. Requirements: FR-002, FR-010.
- [x] T009 [US1] Implement native serialization, canonical named scheme/single identity and exact `sub`/`sid` binding, store-owned lifetime properties, authenticated envelope, fresh 32-byte handle, hashed lookup and atomic create/load/audit in `Admin.Bff/Features/Sessions/` plus narrow Infrastructure persistence. No public routes or ephemeral fallback. Requirements: FR-001–004, FR-006, FR-009–010.
- [x] T010 [US1] Add bounded serialization, scheme/identity/claim-binding mismatch (including replacement tickets), conflicting native lifetime, tamper/wrong-purpose/key-loss/rotation controls in `tests/backend/IdentityPlatform.Host.Tests/Features/BffSessions/` and no-private-log controls in `tests/aspire/IdentityPlatform.AppHost.Tests/Features/BffSessions/`; preserve all issuer protection regressions. Requirements: FR-001–004, FR-006, FR-009–010; SC-003.
- [ ] T011 [US1] Prove migration/repeated upgrade/session-only grants in `tests/persistence/IdentityPlatform.Persistence.Tests/Features/NativePersistenceTests.cs` and independent-instance/restart state in `tests/aspire/IdentityPlatform.AppHost.Tests/Features/BffSessions/` on both real providers. Requirements: FR-003, FR-007; SC-001.

## Phase 4 — US2: Expiry, concurrency and local revocation

- [x] T012 [US2] Implement an injected-clock policy in `Admin.Bff/Features/Sessions/` for strictly positive shorter lifetimes, maximum 30-minute idle/eight-hour absolute, authenticated immutable original idle duration, immutable upstream/absolute cap, expiry at equality and rollback denial; reads never renew. Requirements: FR-004, FR-006.
- [x] T013 [US2] Implement expected-revision renewal and idempotent current-row revocation with atomic audit in session persistence/orchestration; no upsert, revision wraparound or automatic conflict retry. Requirements: FR-005, FR-009.
- [ ] T014 [US2] Add exact-before/at/after deadline, retained-shorter-idle-policy and clock-rollback controls under `Host.Tests/Features/BffSessions/`; native two-instance renewal/revoke interleavings, no-resurrection and failed-audit rollback under `AppHost.Tests/Features/BffSessions/` for both providers. Requirements: FR-004–006, FR-009; SC-001–002.
- [ ] T015 [US2] Add cancellation, database/key unavailability and unknown-commit-outcome evidence under `AppHost.Tests/Features/BffSessions/`; no successful result or implicit retry after failure. Requirements: FR-006, FR-011.

## Phase 5 — US3: Bounded operations and isolation

- [ ] T016 [US3] Implement at-most-100-row eligible ticket cleanup with deletion-time predicates and no audit cascade under `Infrastructure/Features/Sessions/`; prove live renewal/cleanup and deletion/stale-renewal races on both providers. Requirements: FR-005–006, FR-011.
- [x] T017 [US3] Keep production registration internal/dormant and demonstrate no login/callback/proxy/docs/management route changes using `tests/shared/ProductionEndpointInventory.cs` and `Host.Tests/Features/BffSessions/`. Requirements: FR-008; SC-004.
- [x] T018 [US3] Add operations instructions in `docs/operations/bff-session-store.md` for separate keys/credentials, explicit upgrade, safe diagnostics, rollback, retained decryptors and snapshot invalidation; distinguish pending audit retention/export and remote logout. Requirements: FR-007, FR-009–011.

## Phase 6 — Implementation acceptance and next product work

- [x] T019 Reconcile all new discovery identities/counts in `eng/test-matrix.json`, verification selectors and mandatory coverage consumers; add seeded rejection controls for any changed selector. Preserve old counts/IDs, floors and deadlines. Requirements: SC-001, SC-005.
- [ ] T020 Run focused provider/protection/failure evidence and all required exact-head CI, reconcile artifacts and independent source/security/test reviews in `verification.md`; keep failed attempts and actual limits. Requirements: all FR/SC.
- [ ] T021 After every required gate passes, normally squash the implementation PR and update `docs/roadmap.md`, `docs/operations/new-task-handoff.md` and this task list. Mark only B1 complete; retain M02-B/C/D/E gaps. Requirements: SC-005.
- [ ] T022 Prepare the next B2 issuer/client specification and reviewed integration packet: fixed manager-backed admin client/scope/audience, distinct current-MFA admission, pinned native code/S256/PAR/`private_key_jwt` support, denial/replay evidence and later refresh/end-session dependencies. Update `docs/roadmap.md`; no pilot relaxation or inferred activation. Requirements: FR-008 and roadmap M02-B.

## Dependencies and parallel opportunities

T001–003 deliver planning. T004–005 precede source implementation; T006–009 precede durable-state acceptance. T012 can be designed alongside provider test scaffolding after the model is fixed. T013 depends on the store and lifetime policy; T016 depends on renewal/revocation semantics. T010 and migration test preparation can be reviewed independently, but independent files do not justify racing schema/production edits. T019–021 integrate all stories. T022 prepares B2 after the store interface is settled; B2 activation cannot borrow B1 acceptance.

Independent architecture/test and security review may run in parallel with each other. The main agent owns all edits and integration. The first implementation PR should deliver US1–US3 together as a usable internal store, not an isolated helper marked as a complete session feature.

## Convergence review — 15 October 2026

US1–US3 source is implemented and independently reviewed, but B1 is not accepted.
T011, T014–T016 and T020–T021 remain open because the latest both-provider
execution and required CI/coverage are incomplete. T007 records generated
migrations only; T011 still requires their native upgrade/grant evidence.
T019 records strict discovery/count reconciliation and seven selector controls;
coverage floors and existing selection boundaries are unchanged.

Existing foundation migration/grant cases already exercise all registered
Schemas and repeated upgrade; they are reused rather than copied into a new
BffSessions persistence directory. The new AppHost cases supply store-specific
behavior. This path refinement does not remove either provider obligation.

- [ ] T023 Resolve implementation acceptance prerequisites: provision enough Docker storage for the unchanged native suites, and integrate the independently accepted witness-allowance retirement without renewing its expiry. Then complete existing T011/T014–T016/T020 evidence once against the actual candidate. This is an operational/dependency task, not authorization to resume paused WebKit diagnostics or waive gates. Requirements: SC-001, SC-005.

No new feature scope or duplicate implementation is required by this convergence
review. T022 remains the next independent product-design step; browser activation
and a merge cannot borrow the passing unit/source-review evidence.

- [x] T024 Correct first-hosted-candidate integration failures: reconcile the issuer selector's independent AppHost count (8→15) and remove the four IDE0005 imports; preserve the failed run and 114 passing prerequisite controls in `verification.md`. Requirements: SC-005. This completes correction evidence only; T020/T021 remain open.

- [x] T025 Correct the test-owned native lock observer to find its unique marker after a possible SQL parameter declaration, preserving the blocked-request predicate and every deadline. Record independent source review and the incomplete SQL evidence in `verification.md`; native acceptance remains in T020, not this checkbox. Requirements: FR-004, FR-006, SC-001.
