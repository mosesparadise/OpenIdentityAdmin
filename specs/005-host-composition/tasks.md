# Tasks: Explicit host and feature composition

## Phase 1 — Setup

- [x] T001 Record user decisions, merged PR #7 baseline and HC-001–008 in specs/005-host-composition/spec.md.
- [x] T002 Document lifetime/resource contracts, structure, test strategy and constitution check in specs/005-host-composition/plan.md, research.md, data-model.md, contracts/startup.md and quickstart.md.

## Phase 2 — Foundation

- [x] T003 Analyze spec/plan/tasks and obtain independent architecture/test design dispositions; record in specs/005-host-composition/verification.md (HC-001–008).

## Phase 3 — US1: Explicit composition

Independent acceptance: review every original registration/route against Setup ownership; architecture and existing startup tests pass.

- [x] T004 [US1] Extract web composition, storage Setup and Features/Setup in src/backend/IdentityPlatform.Admin.Api/, IdentityPlatform.Admin.Bff/ and IdentityPlatform.Worker/ (HC-001/002/003).
- [x] T005 [US1] Extract explicit auth context, web composition and existing nested feature Setup registration/mapping in src/backend/IdentityPlatform.AuthServer/ (HC-001–005).
- [x] T006 [US1] Extract one-shot native-host extensions and database/local-role Setup in src/migrations/IdentityPlatform.Migrator/ (HC-001/002/003/006).
- [x] T007 [US1] Extract native distributed-builder composition and database/services/pilot Setup in src/aspire/IdentityPlatform.AppHost/ (HC-001/002/006).

## Phase 4 — US2: Preserve behavior

Independent acceptance: targeted contracts and complete existing provider/browser suites remain clean. Tests are authored before their affected production extraction where feasible; lifetime tests follow the context's API and are evaluated before integration.

- [x] T008 [US2] Extend valid-credential pilot denial and configured JWKS absence in tests/backend/IdentityPlatform.Host.Tests/Features/Issuer/IssuerStartupTests.cs; prove actual composition certificate lifetime through configuration/build/verification failures in that Features/Issuer folder (HC-003/004/005).
- [x] T009 [US2] Strengthen deferred secret recipients and successful-completion dependencies in tests/aspire/IdentityPlatform.AppHost.Tests/Features/PilotCompositionTests.cs (HC-006).
- [ ] T010 [US2] Prove actual --initialize-pilot success/repeat/conflict exit behavior without HTTP in tests/aspire/IdentityPlatform.Issuer.Tests/Features/Protocol/BootstrapProcessTests.cs using the existing bootstrap provider fixture (HC-003/005/006).
- [ ] T011 [US2] Run static/unit/architecture/process checks and both-provider HTTP/database/Chromium CI, retain nine-module floors and zero-skips gate, record results in specs/005-host-composition/verification.md and actual test counts in eng/test-matrix.json (HC-007, SC-002).

## Phase 5 — US3: Reviewable delivery

Independent acceptance: owner can navigate scope, evidence and unfinished work from the roadmap; separate Conventional Commit PR.

- [x] T012 [US3] Document ongoing composition conventions in docs/engineering/host-composition.md and AGENTS.md; update docs/roadmap.md and feature 004 sequencing without marking T026 complete (HC-008, SC-001/003).
- [ ] T013 [US3] Resolve final architecture/test/security/Aspire review findings and publish a separate Conventional Commit PR; record dispositions and PR status in specs/005-host-composition/verification.md (HC-007/008).

## Dependencies and execution

T001→T002→T003 precede source work. T008/T009/T010 test definitions can precede the corresponding T005/T007 extraction. T004–T007 are separate host increments but main owns all edits; use parallel reviewers, not overlapping edits/builds. T008 lifetime cases depend on the small context API from T005. T011 follows all source/test edits, T012 may proceed while CI runs, T013 requires final review and evidence. No partial structural MVP is proposed for merge: all six hosts are the user's requested deliverable. User-story task counts: US1 four, US2 four, US3 two, plus three preparation tasks; total 13. Every HC and SC maps to at least one task.
