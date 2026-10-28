# Tasks: Native Aspire local entry

## Foundation

- [x] T001 Record user authority, pinned API research, requirements and plan in `docs/approval.md` and `specs/015-aspire-local-entry/` (all FRs).

## US1 — Native launch

- [x] T002 Add the explicit launch profile in `src/aspire/IdentityPlatform.AppHost/Properties/launchSettings.json`; preserve disabled/default/publish behavior and native cleanup (FR-001,005).

## US2 — Dashboard initialization

- [x] T003 Implement fresh secret interaction, ownership/readiness, atomic attempt state and bounded stdin process handling in `src/aspire/IdentityPlatform.AppHost/Features/LocalDemo/`; add permanent command/process controls in AppHost tests (FR-002–004).
- [x] T004 Remove obsolete `scripts/local-demo/*.ps1` and replace their CI/test entry with meaningful AppHost controls; document native IDE/CLI/dashboard steps (FR-001,006).

## US3 — Integrated disposal and delivery

- [x] T005 Preserve both LocalDemo runtime identities while invoking the real native command; verify real bootstrap/enrollment/login/logout, both providers and disposal in `tests/aspire/IdentityPlatform.AppHost.Tests/Features/` (FR-003–005).
- [x] T006 Reconcile required CI, discovery/coverage/mutation and independent reviews; update feature014 Windows obligation, roadmap/handoff and normal squash delivery (all FRs).

- [x] T007 Update SDK selection to stable10.x, current stable NuGet/npm/tool dependencies, locks and vendor evidence; document the OpenApi, TypeScript and Playwright compatibility constraints and selected compatible releases, preserve frozen producer history and run required source/contract/browser/provider checks.

## Dependencies

T001 precedes code. T002 and T003 converge before T004/T005; T006 requires all. Independent source reviews can run in parallel; main owns all edits. Windows walkthrough remains separate and unclaimed.

[Accepted internal delivery](delivery.md): PR #107 squash `b9fdfc7`, all required automation and independent reviews complete. Actual Windows Docker Desktop walkthrough remains feature 014 T014; no human or release gate is closed.
