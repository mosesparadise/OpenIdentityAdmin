# Tasks: Disposable local administrator demo

Input: [spec](spec.md), [plan](plan.md), [research](research.md), [contract](contracts/local-demo.md).
Tests are mandatory under repository instructions. Main owns edits; independent reviews may run in parallel.

## Foundation

- [x] T001 Record user choices, bounded authority and design in `docs/approval.md` and `specs/014-local-b3-demo/` (FR-001–008).
- [x] T002 Review pinned APIs and choose supported generic BFF pipeline; preserve native ownership, private storage and bootstrap stdin (FR-003–006).
- [x] T003 Implement strict Development-only local profile policy under `src/core/IdentityPlatform.Core/Features/Configuration/` and negative Host tests (FR-001, FR-003).
- [x] T004 Implement bounded private material staging under `src/persistence/IdentityPlatform.Infrastructure/Features/Protection/` with unsafe-path, partial-failure and role tests in `tests/backend/IdentityPlatform.Host.Tests/Features/LocalDemo/` (FR-005, NFR-002).

## US1 — Start and enroll

- [x] T005 Add Aspire container graph/material/TLS/least-privilege wiring in `src/aspire/IdentityPlatform.AppHost/Features/LocalDemo/`, using existing provider adapters (FR-001,002,004,005).
- [x] T006 Add pinned runtime Dockerfile and Windows stdin initialization helper under `scripts/local-demo/`; verify ownership, bounded inputs, cancellation/EOF and no mutation retries (FR-006,007, NFR-002).
- [ ] T007 Verify resource graph, both-provider migration and genuine pending enrollment through runtime hosts; record raw evidence in `specs/014-local-b3-demo/verification.md` (FR-002,004,006).

## US2 — Accepted B3 flows

- [x] T008 Add explicit AuthServer local activation under `src/backend/IdentityPlatform.AuthServer/Features/LocalDemo/`, retaining native account/protocol/logout implementations (FR-003).
- [x] T009 Add generic BFF composition with consumer/key lifetime ownership under `src/backend/IdentityPlatform.Admin.Bff/Features/LocalDemo/`; preserve default contract fixture wrappers and health behavior (FR-003,005).
- [ ] T010 Verify default/invalid/non-Development, health/unknown route isolation and genuine owned native callback/login/logout in Host and both-provider retained-browser tests (FR-003,004, NFR-001).

## US3 — Dispose and hand off

- [ ] T011 Verify owned resource removal, no persistent volumes, new-run invalidation and whole-graph restart guidance after key loss (FR-007).
- [ ] T012 Write executable Windows quickstart in `specs/014-local-b3-demo/quickstart.md`, update `README.md`, `docs/roadmap.md` and `docs/operations/new-task-handoff.md` with evidence and limits (FR-008).
- [ ] T013 Complete independent architecture/security/test/Aspire reviews and exact-head required CI, inventory/coverage/mutation reconciliation in `verification.md`; perform only normal reviewed squash when accepted (NFR-001).

## Dependencies and strategy

T001–004 precede runtime/material work. T005–006 and T008–009 converge before T007/T010. T011 requires the actual graph; T012–013 require observed evidence. Checked implementation tasks mean the candidate source and focused checks exist; they do not imply merged or hosted acceptance. T007/T010–013 retain integrated execution and delivery obligations. Independent source reviews can run concurrently after changes stabilize; tests retain both providers and only the existing A-009 deferrals.
