# Tasks: Issuer lifecycle continuity

Status: **restart delivered in squash-merged PR #17 (19 checks / 1,045 cases); distribution delivered in squash-merged PR #18 (21 checks / 1,097 cases); T016 design/configuration seam complete; T017/T018 source acceptance complete in run 114 (25 checks / 1,149 cases); final PR #19 delivery gate below**. Approved backlog: M01-E / ISS-003. On 11 October 2026 the user selected the dedicated lifecycle test project and three sequential PRs, beginning with restart continuity. Paths below now reflect those choices.

## Phase 1 — Design and clarification

- [x] T001 Write feature 006 `spec.md`, initial `plan.md`, `research.md`, `data-model.md` and `quickstart.md` from approved M01-E and actual source research; preserve all three lifecycle stories and external gates.
- [x] T002 Resolve the user's project/solution-folder and PR-sequencing choices in `plan.md`; finalize source-sharing ownership and exact runner/coverage design before code. Complete `checklists/requirements.md` and Spec Kit analysis.
- [x] T003 Independently review the concrete design with architecture, security and test/Aspire reviewers; record findings/dispositions in `verification.md`. Source research is not a runtime pass.

## Phase 2 — Restart foundation (PR #17)

- [x] T004 Add `tests/aspire/IdentityPlatform.Issuer.Lifecycle.Tests/IdentityPlatform.Issuer.Lifecycle.Tests.csproj`, its package lock and matching Aspire test solution grouping after T002. Reuse native MTP and explicit source-linked fixture support under `Fixtures/`; do not inherit an executable test project or copy browser test cases. LIFE-009.
- [x] T005 Separate reusable issuer environment support from old xUnit collection declarations in the existing `tests/aspire/IdentityPlatform.Issuer.Tests/Fixtures/` and link only the required environment/RP/form/protection sources into the chosen project. Preserve original suite identities/behavior and credential lifetime ownership. LIFE-001/009.
- [x] T006 Add `Fixtures/IssuerProcessLifecycle.cs` and extend shared `LifecycleEvidence.cs` for accumulated process generations, explicit stop/start, stable issuer/store proof, failure-path capture and bounded cleanup. Test stale-health/no-op/reused-PID and orphaned-child predicates plus concurrent accumulation with meaningful controls. LIFE-001/008/009.
- [x] T007 Wire the lifecycle invocation into `eng/test-matrix.json`, `scripts/verify.py`, exact provider discovery/name gates and `.github/workflows/verify.yml`/coverage evidence as required by the selected design. Keep every existing case and the unchanged 660s invocation deadline; test omitted/skipped/duplicate/wrong-provider evidence rejection. LIFE-009.

## Phase 3 — US1 restart acceptance (PR #17)

- [x] T008 Implement `Features/Restart/SessionContinuityTests.cs`: original eligible cookie via controlled silent flow, pending original antiforgery form, completed continuation and browser-binding negatives; prohibit automatic credential fallback. LIFE-001/002/004/008, RST-01/02/03.
- [x] T009 Implement `Features/Restart/CodeContinuityTests.cs`: exact unredeemed code survival, wrong-proof control, previously redeemed but still-unexpired native replay denial/revocation, original-code actual expiry and valid recovery. Capture native identity/expiry without emitting secrets. LIFE-003/005, RST-04/05/08.
- [x] T010 Implement `Features/Restart/ProtectionContinuityTests.cs`: old token bytes validated using fresh post-restart public JWKS and new tokens validated with retained public keys; tamper/wrong-key controls; populated-ring missing-wrapper failure and safe recovery. LIFE-004/008, RST-06/09.
- [x] T011 Implement `Features/Restart/EligibilityContinuityTests.cs`: preexisting session expiry/revocation and representative account restriction, including a committed restriction while auth is stopped; both positive controls and explicit fresh recovery. LIFE-005, RST-07.
- [x] T012 Run focused actual-provider experiments and full normal required gates, record failures before fixes, case identities/counts, timing margin, teardown, source/merge trees and independent source/evidence reviews in `verification.md`. Update `docs/roadmap.md`, `docs/engineering/issuer-flow-backlog.md` and `docs/operations/issuer-protection.md`. LIFE-009/010. Only US1 is accepted at the corrected source checkpoint. Final PR delivery is the gate below.

**PR #17 delivery gate completed:** final head `3870ffaba5bb8f202fa1cff7ded89d7a78af0903`, final run `38178327657` (all 19 checks), squash `a6b4dfbde660162197e188715a4ba4056cd14772`. Tested merge and squash have tree `97df4e105983271cd6f10644596824d277417860`. No unresolved review findings. [PR #17](https://github.com/mosesparadise/OpenIdentityAdmin/pull/17) records exact evidence.

## Phase 4 — US2 two-instance behavior (PR #18; delivered)

- [x] T013 Write `contracts/distribution.md` and a detailed implementation plan for canonical HTTPS routing, node provenance, shared store/DP material, explicit topology and trust boundaries before changing AppHost/test composition. Confirm structural changes; independently review. LIFE-006/008.
- [x] T014 Implement `Features/Distribution/` and required `Fixtures/` topology support: cross-node original form/antiforgery/cookie/continuation/code, concurrent code winner/replay consequences, committed eligibility and surviving-node cases. Use actual processes with provable dispatch, no generic mutation retries. LIFE-002/003/005/006/008.
- [x] T015 Execute both providers, full required regression/coverage and independent reviews; update `verification.md`, roadmap/backlog/runbook. Accepted source checkpoint: `12ae110`, run `38181828768`, 21 checks / 1,097 ordinary passes with no failures/skips. M01-E remains partial while overlap is pending. LIFE-009/010. Squash delivery is the separate gate below.

**PR #18 delivery gate completed:** final head `71afb88b7d5352559ce0f01a833be75c84768818`, run `38183025677` (all 21 checks / 1,097 ordinary passes, no failures/skips), squash `58ebfb1afce2698f8d3edd86f9e30548fa8dda53`. Head, tested merge and squash share tree `546649ad6cac9a130a439ce30856f9b4d7ce8233`. All review findings resolved; normal expected-head squash merge performed. [PR #18](https://github.com/mosesparadise/OpenIdentityAdmin/pull/18) records final evidence.

## Phase 5 — US3 staged key overlap (PR #19; source acceptance complete; final delivery gate below)

- [x] T016 Write `contracts/key-lifecycle.md` and detailed plan for backward-compatible active/staged/retiring configuration, signing prepublication, explicit selection, retained decryption, per-artifact lifetime/skew/cache retirement bounds and operator rollback/rollout; independently review native library behavior before implementation. LIFE-007/008.
- [x] T017 Implement only the reviewed credential/protocol/composition changes under existing production `Features/Issuer/` and `Features/Protocol/` plus `Features/KeyRotation/` tests. Prove publish→activate→overlap→safe-retire and premature-removal negatives on both real provider topologies. Native crypto/validation remains library-owned. LIFE-004/007/008.
- [x] T018 Run complete required gates and independent security/architecture/test reviews; update operator runbook and traceable evidence, then squash-merge. Close M01-E only when every LIFE/ISS-003 obligation has evidence; retain manual/full-product/backup-restore/external gates. LIFE-009/010.

**PR #19 delivery gate:** run `38210425980` at source `69b8e8d` establishes source acceptance. Diagnostic-only assertion refinements and acceptance documentation require all final-head checks before normal expected-head squash delivery. The [PR](https://github.com/mosesparadise/OpenIdentityAdmin/pull/19) records final checks, reviewed head and merge; no unchecked provider or protection bypass is permitted.

## Dependencies and parallel review

T001 → T002 → T003 precedes implementation. T004/005 establish the agreed fixture ownership; T006 and T007 establish lifecycle/proof and evidence selection; T008–T011 use those foundations; T012 closes US1. Then T013 → T014 → T015 and T016 → T017 → T018. The user confirmed separate sequential PRs; distribution begins only after restart is merged, and rotation after distribution is merged. Read-only architecture/security/test reviews may run in parallel; the lead owns all edits and resolves concrete findings.

## Traceability

| Requirements | Tasks / cases | Evidence boundary |
| --- | --- | --- |
| LIFE-001/002 | T005/006/008, RST-01/02/03/09 | Same-origin actual process and old browser state, not new login or environment. |
| LIFE-003/005 | T009/011/014, RST-04/05/07/08 | Native code and committed eligibility across restart/nodes, not offline revocation. |
| LIFE-004 | T008/010/017, RST-01/06 | Public validation with unchanged old tokens and retained/fresh public key sets. |
| LIFE-006 | T013–015 | Actual two-process dispatch, shared state and concurrency. |
| LIFE-007 | T016–018 | Staging/selection/overlap/retirement, distinct from DP wrapping only. |
| LIFE-008/009/010 | T003–018 | Fail closed, both providers, exact selection/coverage/cleanup, operator and review evidence. |

No checked task or reviewer opinion establishes a future executed test. Product/control claims remain limited to the evidence actually collected.
