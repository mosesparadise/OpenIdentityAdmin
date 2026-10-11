# Tasks: First-party login and S256 code flow

Current status: T023–T032 and bounded T018 are merged; T032/PR #15 squash is `f1b1a4f`, with final 17-check/1,017-case acceptance in run 38163339126. T021 is next: the [manual execution plan](manual-acceptance.md), [assessment instructions](../../docs/operations/pilot-manual-assessment.md) and [blank results template](manual-assessment-results.md) prepare the human handoff. No manual results are recorded; T021 and full feature/manual acceptance remain open. Start with the [current acceptance matrix](acceptance-matrix.md).

Input: [spec](spec.md), [plan](plan.md), [research](research.md), [data model](data-model.md), [contracts](contracts/issuer.md). Tests are mandatory. An implementation checkbox requires its stated evidence; planned test identifiers are not passes.

## Phase 1 — Specification and design

- [x] T001 Specify LOGIN-001–009 and SC-001–005 in `specs/004-first-party-login/spec.md`; review `checklists/requirements.md` under existing approval.
- [x] T002 Research native protocol/browser boundaries and define `plan.md`, `research.md`, `data-model.md`, `contracts/issuer.md`, `quickstart.md` in this feature directory.
- [x] T003 Resolve independent architecture/security/test/UX design review and record read-only Spec Kit analysis in `specs/004-first-party-login/verification.md` before code. Covers every LOGIN requirement.

## Phase 2 — Foundations

- [x] T004 Add bounded pilot-manifest/session/interaction/audit models and user enable flag in `src/persistence/IdentityPlatform.Infrastructure/Features/Identity/Persistence/`; configure in `Persistence/PlatformDbContext.cs`. LOGIN-003/004/005/008.
- [x] T005 Generate both provider platform migrations under `src/migrations/IdentityPlatform.Migrations.PostgreSql/Platform/` and `src/migrations/IdentityPlatform.Migrations.SqlServer/Platform/`; update `src/persistence/IdentityPlatform.Infrastructure/Features/Health/SchemaCatalog.cs`, real-provider upgrade/idempotence/runtime-role tests. LOGIN-001/009.
- [x] T006 Add the provider-only project `tests/aspire/IdentityPlatform.Issuer.Tests`, solution folder, locked dependencies and explicit trusted browser provisioning with a positive trusted navigation and unrelated-untrusted-certificate rejection. Preserve MTP/coverage/no-skips in `eng/test-matrix.json`, `scripts/verify.py` and `.github/workflows/verify.yml`. LOGIN-009.

## Phase 3 — US1: Explicit initialization and accurate issuer

Independent acceptance: INIT tests on both actual providers plus startup policy unit tests; no login shortcut substitutes for later stories.

- [x] T007 [US1] Add enablement/configuration/production-denial tests in `tests/backend/IdentityPlatform.Host.Tests/Features/Pilot/`; initialization/idempotence/credential/callback/extra-permission/renamed-user/disabled/partial-record/concurrent-initialization/rollback tests in `tests/aspire/IdentityPlatform.Issuer.Tests/Features/Protocol/InitializationTests.cs`. LOGIN-001/002/008.
- [x] T008 [US1] Implement explicit manager-owned bootstrap and audit in `src/backend/IdentityPlatform.AuthServer/Features/Pilot/`; wire command/flag safely through host Composition extensions. LOGIN-001/008.
- [x] T009 [US1] Configure native endpoints/credentials/request cache/S256/capabilities and named cookies in `src/backend/IdentityPlatform.AuthServer/Features/Protocol/`; enforce early body/rate/deadline/log restrictions. Add override/reload/log-positive-control tests under Host.Tests Features. LOGIN-002/003/006/008.
- [x] T010 [US1] Extend `src/aspire/IdentityPlatform.AppHost/Composition/` with explicit pilot bootstrap/issuer dependencies and private synthetic parameters; add real metadata/JWKS/health-only isolation tests in Issuer.Tests `Features/Protocol/MetadataTests.cs`. LOGIN-001/002/009.

## Phase 4 — US2: Bound themed login

Independent acceptance: real browser form plus durable state transitions; no mocked Identity authentication. LOGIN tests reference the contract's five-minute interaction/15-minute session policy.

- [x] T011 [US2] Add expiry/one-use/concurrent completion/session-replacement/current-eligibility/eight-interaction-limit tests under Issuer.Tests `Features/Protocol/{Interaction,PasswordEntry,RequestBinding,InteractionRace}Tests.cs` and pure freshness/input tests under Host.Tests `Features/Accounts/`. LOGIN-003/004/005/008.
- [x] T012 [US2] Implement bound durable interaction/session/password/audit transitions and Razor feature-root discovery under AuthServer `Features/Accounts/Login/` and `Features/Accounts/Shared/`. LOGIN-003/004/008.
- [x] T013 [P] [US2] Implement canonical tokens/theme assets at `src/frontend/IdentityPlatform.DesignTokens/`, linked publish assets, accessible Razor shell/form/errors and strict CSP under AuthServer `Features/Accounts/`. LOGIN-007.
- [x] T014 [US2] Implement Minimal authorization decisions under AuthServer `Features/Protocol/Authorize/`: original cached request, exact browser/completed-session binding, prompt/max_age rules and one-use consume. LOGIN-002/004/005/006.
- [x] T015 [US2] Add real form/antiforgery/browser-binding/tabs/freshness/generic-error/no-password-echo/theme/keyboard/reflow/CSP/browser-storage tests in Issuer.Tests `Features/Browser/`; create and inspect deterministic visual baselines, record manual gaps. LOGIN-003/004/007/009.

## Phase 5 — US3: Eligible single-use exchange

Independent acceptance: HTTP protocol cases start from codes obtained through the actual protected form; independent public-key token validation. Exactly one concurrent winner, no retry.

- [x] T016 [US3] Implement shared persisted eligibility/claims policy and Minimal exchange in AuthServer `Features/Protocol/Policy/` and `Features/Protocol/Token/`; retain native principal/tracking/bindings. LOGIN-005/006.
- [x] T017 [US3] Implement HTTPS server-held state/nonce/S256 test RP and independent JWT validation under Issuer.Tests `Fixtures/`; reject wrong issuer/audience/type/signature/nonce without private issuer keys. LOGIN-002/006/009.
- [x] T018 [US3] Add protocol denial/expiry/replay/single-winner/response-loss and after-code current-state mutation matrix in Issuer.Tests `Features/Protocol/`; assert stored-code scopes revalidated even when exchange scope absent. LOGIN-002/005/006/009.
- [x] T019 [US3] Add real outage/delayed-command/cancellation/request-bound/log-secrecy and audit-write-failure rollback tests under Issuer.Tests `Features/Protocol/OperationsTests.cs`; record native consumption semantics, safe readiness/liveness and audit atomicity. LOGIN-008/009.

## Phase 6 — Convergence and reviewable delivery

- [x] T020 Run meaningful critical-decision mutation/negative controls for S256, interaction/session binding, eligibility and replay; record before/after results in `specs/004-first-party-login/verification.md`. LOGIN-009, SC-002/003.
- [ ] T021 ([manual handoff and remaining subtasks](manual-acceptance.md)) Run full static/assurance/supply-chain/unit/both-provider/browser/combined-coverage gates; retain unchanged per-module90/80 floors and exact no-skips reports. Record browser/provider matrix and independent reviews in `verification.md`. All LOGIN/SC requirements.
- [x] T022 Update `docs/roadmap.md`, `docs/engineering/issuer-flow-backlog.md`, `docs/operations/first-party-pilot.md`, `docs/assurance/controls.csv` references and this feature's quickstart/evidence; preserve unbuilt M01-E/API/BFF/manual/certification tasks explicitly. Publish one coherent PR for review; do not merge the next PR without authorization. All LOGIN/SC requirements.

## Dependencies and parallel opportunities

T001→T002→T003 precedes implementation. T004→T005 and T006 establish state/harness. US1 T007→T008/T009→T010 enables genuine US2/US3 acceptance. US2 T011→T012→T014, while T013 can proceed once shared asset paths are fixed; T015 follows T014/T016/T017 integration; create the T017 fixture before browser acceptance. US3 T016/T017→T015/T018/T019 depends on US2 for real authentication. T020→T021→T022 closes the feature. Independent reviewers may work in parallel; the lead integrates changes.

Incremental strategy: validate explicit bootstrap/metadata first, then protected login, then native exchange/deny matrix. Intermediate commits do not change acceptance or target-branch squash policy. No story is accepted solely from happy-path or unit tests.

## Traceability and control routing

| Requirement | Tasks | Planned evidence groups | Official ASVS 5.0.0 / threats |
| --- | --- | --- | --- |
| LOGIN-001 | T004/005/007/008/010 | INIT | Chapter V6/V13/V16; THR-06/13/16 |
| LOGIN-002 | T007/009/010/014/017/018 | META, PKCE | V10.4.6; THR-09/10/11 |
| LOGIN-003 | T004/009/011/012/015 | LOGIN, CSRF, SESSION | V3.5.1; chapters V6/V7; THR-03/06/07 |
| LOGIN-004 | T004/011/012/014/015 | BIND, FRESH, REPLAY | V10.1.2 (RP correlation subset), V3.5.1; THR-03/07/10 |
| LOGIN-005 | T004/011/014/016/018 | CURRENT | Chapters V7/V8/V10; THR-07/08/10 |
| LOGIN-006 | T009/014/016/017/018 | CLAIM, CODE, CONCURRENT | V10.4.2/3/6; THR-09/10 |
| LOGIN-007 | T013/015 | UI, A11Y, VISUAL | Chapter V3; THR-03 |
| LOGIN-008 | T004/007/008/009/011/012/019 | AUDIT, LOG, FAULT | Chapter V16; THR-02/13/16 |
| LOGIN-009 | T005/006/010/015/017/018/019/020/021/022 | Complete verification record | NFR-11/14; THR-15 |

Chapter routing does not assert every chapter control applies or passes. Individual register rows remain not_assessed until qualified exact control evidence is reviewed. The native second-redemption test must check linked token revocation as required by V10.4.2, without claiming offline JWT immediate revocation.

## Phase 7: Convergence

The original convergence identified seven partial findings. T023–T028 and T030 now have merged evidence. T029 reconciled that evidence and named the additional verification gaps T031/T032. T031 and T032 are now accepted below; manual/full-feature obligations remain open. No acceptance criterion is relaxed.

- [x] T023 ([execution plan](bootstrap-acceptance.md)) Complete persisted partial-record/descriptor/account-state conflicts, first-creation concurrency and audit-failure rollback in `Features/Protocol/BootstrapAcceptanceTests.cs`; verify neither provider repairs or partially initializes state, per LOGIN-001/008, US1/AC1–2 and T007/008 (partial, HIGH).
- [x] T024 ([execution plan](interaction-acceptance.md)) Complete current eligibility at password entry, five-attempt lockout, cached-request overlay, delayed max_age, independent tabs and completion/consumption race evidence under Issuer.Tests `Features/Protocol/{Interaction,PasswordEntry,RequestBinding,InteractionRace}Tests.cs`; execute added freshness/capacity/concurrent-completion cases on both providers, per LOGIN-003/004/005, US2/AC1–3 and T011–015 (partial, HIGH).
- [x] T025 ([execution plan](exchange-acceptance.md)) Complete account deletion, role addition, authorization revocation, unrelated-cookie subject isolation and lost-response exchange tests under Issuer.Tests `Features/Protocol/`; execute actual60s expiry and negative RP issuer/audience/type/signature/nonce controls, per LOGIN-005/006/009, US3/AC1–4 and T017/018 (partial, HIGH).
- [x] T026 ([execution plan](operations-acceptance.md)) Complete delayed-command/cancellation/rate/concurrency/log-sentinel tests, actual configuration-reload diagnostic guards and audit-failure consumption/bootstrap tests under Issuer.Tests `Features/Protocol/OperationsTests.cs` and Host.Tests `Features/Pilot/`; record native consumption outcomes and measured bounds, per LOGIN-008/009, SC-005 and T009/019 (partial, HIGH). Plan fixture isolation and execution budget using the slower observed feature 005 SQL Server issuer baseline of 10m32s under the unchanged 660s deadline (about 28s headroom; another run took 8m33s, prior T025 baseline 10m05s); added fault coverage must retain every required case.
- [x] T027 ([execution plan](browser-acceptance.md)) Complete trusted/untrusted browser TLS controls, both-provider login/theme/error/system/freshness and antiforgery/tab journeys, correct cross-site pending-form cookie replacement, review deterministic visual baselines and wire Firefox/WebKit both-provider nightly/dependency execution in `.github/workflows/verify.yml` and Issuer.Tests `Features/Browser/`, per LOGIN-003/004/007/009, SC-004, plan browser cadence and T006/015/021 (partial, HIGH).
- [x] T028 ([execution plan](critical-decision-verification.md)) Run seeded critical-decision mutations and exact-final-head static/assurance/supply-chain/unit/both-provider/browser/combined90/80 coverage; obtain evidence-based specialist dispositions in `verification.md`, per LOGIN-009, SC-001/002/003 and T020/021 (partial, HIGH). Corrected-source acceptance: run 38139630540, all 17 checks/793 ordinary cases/zero failures or skips; final documentation-head verification and squash delivery are recorded on [PR #11](https://github.com/mosesparadise/OpenIdentityAdmin/pull/11).
- [x] T029 ([execution plan](evidence-reconciliation.md)) Reconcile requirement-to-test evidence, control-register references, roadmap/runbook/quickstart and PR readiness after T023–028 and T030; retain M01-E and all manual/external gates, per SC-003, constitution principles1/12/17/18 and T022 (partial, MEDIUM).

- [x] T030 ([execution plan](capacity-acceptance.md)) Resolve the concurrent browser-capacity gap in LoginInteractions.CreateAsync: two requests presenting seven active interactions can both allocate another. Implement and test an atomic eight-live-interaction budget consistent with LOGIN-004/plan/issuer contract, or obtain explicit approval for a concrete contract amendment. Cover simultaneous admission, completion/expiry capacity recovery, browser binding and both providers. The original sequential-only evidence did not establish the concurrent cap (MEDIUM finding from T024). PR #12 closes this bounded item; after accepted T031/T032, manual obligations still block full M01-D acceptance.

## Acceptance follow-up tasks

- [x] T031 ([execution plan](transport-acceptance.md); accepted source, final delivery on PR #14) Complete the specified HTTP transport acceptance matrix in Issuer.Tests `Features/Protocol/`: missing/empty Basic credentials or secret, body-only client credentials, conflicting Basic/body identity, unsupported grants, wrong protocol methods/content types, and forwarding-header non-influence. Invalid requests must receive contract-appropriate denials without code/tokens; spoofed forwarding headers must not change canonical issuer, callback or TLS decisions, but need not make an otherwise valid request fail. Use both actual providers, genuine eligible controls, recovery and safe diagnostics. Configuration/metadata tests are not substitutes. Closes the remaining transport portion of T018 and supports LOGIN-002/003/006/009, US3 AC2 and SC-001. Write the bounded execution plan before implementation.
- [x] T032 ([execution plan](authorization-state-acceptance.md); verified source, final delivery on PR #15) Prove committed current eligibility at the authorization HTTP boundary: establish a genuine session, commit representative account/session and application/scope restrictions, then attempt new/resumed authorization including `prompt=none`. Assert no code or subject substitution, correct protocol outcomes and genuine recovery on both providers. Password-entry/redemption tests do not prove this boundary. Supports LOGIN-005/009, T011/014/018 and SC-001; write its execution plan before implementation.

T018 is complete for its denial/expiry/replay/response-loss and **after-code** current-state matrix, with T031 closing its remaining transport obligations. T032 separately proves changed eligibility at the **authorization** boundary; its representative committed-state matrix is now verified for LOGIN-005/009 and SC-001. T021 remains open for full-feature convergence after the remaining qualified manual obligations. T015 closes the automated/visual work and the requirement to record manual gaps; it does not claim manual screen-reader/password-manager or complete WCAG acceptance. T004–T017/T019 were reconciled individually against existing implementation and executed groups in the [acceptance matrix](acceptance-matrix.md). T022/T029 reconciliation and delivery completed in PR #13. T031 final documentation-head CI and squash delivery completed in PR #14 (`44a0e90`).

## Current delivery and history

T030 squash-merged as `fca82fb` in PR #12 and T029 as `237afa5` in PR #13. T031/PR #14 squash-merged as `44a0e90` after final source `d8bce059484d2ab84a23801972554ecee2214bde` passed all 17 checks/943 cases in run 38159757185. T032/PR #15 squash-merged as `f1b1a4f` after final head `4237252` passed all 17 checks/1,017 cases in run 38163339126. T021 and qualified manual obligations are next; handoff preparation does not complete their execution. Full M01-D, M01-E/M01-F, manual and external requirements remain open.

| Delivered work | PR / squash | Evidence boundary |
| --- | --- | --- |
| Initial pilot | #4 | Bounded Development pilot; subsequent convergence required |
| T023 bootstrap | #5 / `d4664a3` | Conflict/concurrency/rollback acceptance |
| T024 interaction | #6 / `5aa330c` | Credential/request/session transitions; T030 later closes concurrent capacity |
| T025 exchange | #7 / `9c52c2d` | Current redemption state, expiry, replay and response loss |
| Feature 005 composition | #8 / `64d8ceb` | Structural refactor; no security acceptance substitution |
| T026 operations | #9 / `7d1a9bc` | Fault bounds, logging and audit rollback |
| Initial T027 browser work | #10 / `3310d4b` | Merged under A-002; historical checks did not close its contrast finding |
| T027 correction and T028 mutations | #11 / `bdd45e5` | Reproduced theme defect corrected; final 17-check/793-case acceptance |
| T030 atomic capacity | #12 / `fca82fb` | Accepted 17-check/845-case checkpoint |
| T029 evidence reconciliation | #13 / `237afa5` | Final 17-check/845-case acceptance; gaps retained |
| T031 transport | #14 / `44a0e90` | Final 17-check/943-case acceptance; T018 closes |
| T032 authorization state | #15 / `f1b1a4f` | Final 17-check/1,017-case acceptance; representative committed-state matrix closes |

Detailed failed/superseded checkpoints and review dispositions remain in [verification.md](verification.md); their old pending statuses describe their dates, not the current state. A-002 is expired, with its reproduced finding corrected through the existing closure criteria. A-001 and all certification release gates remain unchanged. Each next task must be independently reviewed, verified and squash-merged before the following one begins.


T032 source `8b8f12ac6095593b392738eb5592a3255ffe3a6d` passed all 17 checks and 1,017 ordinary cases with zero failures/skips in [run 38162030027](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38162030027). Final documentation-head verification and normal squash delivery are recorded on [PR #15](https://github.com/mosesparadise/OpenIdentityAdmin/pull/15); this source checkpoint does not claim its own future merge. T021/full M01-D, qualified manual password-manager/screen-reader/accessibility and timed usability, M01-E/M01-F, Admin BFF/React and the wider R1 product remain open. A-001 retains actual certification as a public-release gate; all 345 ASVS controls remain pending/not_assessed.
