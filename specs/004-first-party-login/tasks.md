# Tasks: First-party login and S256 code flow

Input: [spec](spec.md), [plan](plan.md), [research](research.md), [data model](data-model.md), [contracts](contracts/issuer.md). Tests are mandatory. An implementation checkbox requires its stated evidence; planned test identifiers are not passes.

## Phase 1 — Specification and design

- [x] T001 Specify LOGIN-001–009 and SC-001–005 in `specs/004-first-party-login/spec.md`; review `checklists/requirements.md` under existing approval.
- [x] T002 Research native protocol/browser boundaries and define `plan.md`, `research.md`, `data-model.md`, `contracts/issuer.md`, `quickstart.md` in this feature directory.
- [x] T003 Resolve independent architecture/security/test/UX design review and record read-only Spec Kit analysis in `specs/004-first-party-login/verification.md` before code. Covers every LOGIN requirement.

## Phase 2 — Foundations

- [ ] T004 Add bounded pilot-manifest/session/interaction/audit models and user enable flag in `src/persistence/IdentityPlatform.Infrastructure/Features/Identity/Persistence/`; configure in `Persistence/PlatformDbContext.cs`. LOGIN-003/004/005/008.
- [ ] T005 Generate both provider platform migrations under `src/migrations/IdentityPlatform.Migrations.PostgreSql/Platform/` and `src/migrations/IdentityPlatform.Migrations.SqlServer/Platform/`; update `src/persistence/IdentityPlatform.Infrastructure/Features/Health/SchemaCatalog.cs`, real-provider upgrade/idempotence/runtime-role tests. LOGIN-001/009.
- [ ] T006 Add the provider-only project `tests/aspire/IdentityPlatform.Issuer.Tests`, solution folder, locked dependencies and explicit trusted browser provisioning with a positive trusted navigation and unrelated-untrusted-certificate rejection. Preserve MTP/coverage/no-skips in `eng/test-matrix.json`, `scripts/verify.py` and `.github/workflows/verify.yml`. LOGIN-009.

## Phase 3 — US1: Explicit initialization and accurate issuer

Independent acceptance: INIT tests on both actual providers plus startup policy unit tests; no login shortcut substitutes for later stories.

- [ ] T007 [US1] Add enablement/configuration/production-denial tests in `tests/backend/IdentityPlatform.Host.Tests/Features/Pilot/`; initialization/idempotence/credential/callback/extra-permission/renamed-user/disabled/partial-record/concurrent-initialization/rollback tests in `tests/aspire/IdentityPlatform.Issuer.Tests/Features/Protocol/InitializationTests.cs`. LOGIN-001/002/008.
- [ ] T008 [US1] Implement explicit manager-owned bootstrap and audit in `src/backend/IdentityPlatform.AuthServer/Features/Pilot/`; wire command/flag safely in `Program.cs`. LOGIN-001/008.
- [ ] T009 [US1] Configure native endpoints/credentials/request cache/S256/capabilities and named cookies in `src/backend/IdentityPlatform.AuthServer/Features/Protocol/`; enforce early body/rate/deadline/log restrictions. Add override/reload/log-positive-control tests under Host.Tests Features. LOGIN-002/003/006/008.
- [ ] T010 [US1] Extend `src/aspire/IdentityPlatform.AppHost/Program.cs` with explicit pilot bootstrap/issuer dependencies and private synthetic parameters; add real metadata/JWKS/health-only isolation tests in Issuer.Tests `Features/Protocol/MetadataTests.cs`. LOGIN-001/002/009.

## Phase 4 — US2: Bound themed login

Independent acceptance: real browser form plus durable state transitions; no mocked Identity authentication. LOGIN tests reference the contract's five-minute interaction/15-minute session policy.

- [ ] T011 [US2] Add expiry/one-use/concurrent completion/session-replacement/current-eligibility/eight-interaction-limit tests under Issuer.Tests `Features/Protocol/{Interaction,PasswordEntry,RequestBinding,InteractionRace}Tests.cs` and pure freshness/input tests under Host.Tests `Features/Accounts/`. LOGIN-003/004/005/008.
- [ ] T012 [US2] Implement bound durable interaction/session/password/audit transitions and Razor feature-root discovery under AuthServer `Features/Accounts/Login/` and `Features/Accounts/Shared/`. LOGIN-003/004/008.
- [ ] T013 [P] [US2] Implement canonical tokens/theme assets at `src/frontend/IdentityPlatform.DesignTokens/`, linked publish assets, accessible Razor shell/form/errors and strict CSP under AuthServer `Features/Accounts/`. LOGIN-007.
- [ ] T014 [US2] Implement Minimal authorization decisions under AuthServer `Features/Protocol/Authorize/`: original cached request, exact browser/completed-session binding, prompt/max_age rules and one-use consume. LOGIN-002/004/005/006.
- [ ] T015 [US2] Add real form/antiforgery/browser-binding/tabs/freshness/generic-error/no-password-echo/theme/keyboard/reflow/CSP/browser-storage tests in Issuer.Tests `Features/Browser/`; create and inspect deterministic visual baselines, record manual gaps. LOGIN-003/004/007/009.

## Phase 5 — US3: Eligible single-use exchange

Independent acceptance: HTTP protocol cases start from codes obtained through the actual protected form; independent public-key token validation. Exactly one concurrent winner, no retry.

- [ ] T016 [US3] Implement shared persisted eligibility/claims policy and Minimal exchange in AuthServer `Features/Protocol/Policy/` and `Features/Protocol/Token/`; retain native principal/tracking/bindings. LOGIN-005/006.
- [ ] T017 [US3] Implement HTTPS server-held state/nonce/S256 test RP and independent JWT validation under Issuer.Tests `Fixtures/`; reject wrong issuer/audience/type/signature/nonce without private issuer keys. LOGIN-002/006/009.
- [ ] T018 [US3] Add protocol denial/expiry/replay/single-winner/response-loss and after-code current-state mutation matrix in Issuer.Tests `Features/Protocol/`; assert stored-code scopes revalidated even when exchange scope absent. LOGIN-002/005/006/009.
- [ ] T019 [US3] Add real outage/delayed-command/cancellation/request-bound/log-secrecy and audit-write-failure rollback tests under Issuer.Tests `Features/Protocol/OperationsTests.cs`; record native consumption semantics, safe readiness/liveness and audit atomicity. LOGIN-008/009.

## Phase 6 — Convergence and reviewable delivery

- [ ] T020 Run meaningful critical-decision mutation/negative controls for S256, interaction/session binding, eligibility and replay; record before/after results in `specs/004-first-party-login/verification.md`. LOGIN-009, SC-002/003.
- [ ] T021 Run full static/assurance/supply-chain/unit/both-provider/browser/combined-coverage gates; retain unchanged per-module90/80 floors and exact no-skips reports. Record browser/provider matrix and independent reviews in `verification.md`. All LOGIN/SC requirements.
- [ ] T022 Update `docs/roadmap.md`, `docs/engineering/issuer-flow-backlog.md`, `docs/operations/first-party-pilot.md`, `docs/assurance/controls.csv` references and this feature's quickstart/evidence; preserve unbuilt M01-E/API/BFF/manual/certification tasks explicitly. Publish one coherent PR for review; do not merge the next PR without authorization. All LOGIN/SC requirements.

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

Assessment: nine LOGIN requirements, five SC criteria, twelve story acceptance scenarios, eight plan decision areas, all22 existing tasks and all18 constitution principles. Seven partial findings (six high, one medium); no new scope or acceptance relaxation. Draft status means missing evidence is not an asserted control pass.

- [x] T023 ([execution plan](bootstrap-acceptance.md)) Complete persisted partial-record/descriptor/account-state conflicts, first-creation concurrency and audit-failure rollback in `Features/Protocol/BootstrapAcceptanceTests.cs`; verify neither provider repairs or partially initializes state, per LOGIN-001/008, US1/AC1–2 and T007/008 (partial, HIGH).
- [x] T024 ([execution plan](interaction-acceptance.md)) Complete current eligibility at password entry, five-attempt lockout, cached-request overlay, delayed max_age, independent tabs and completion/consumption race evidence under Issuer.Tests `Features/Protocol/{Interaction,PasswordEntry,RequestBinding,InteractionRace}Tests.cs`; execute added freshness/capacity/concurrent-completion cases on both providers, per LOGIN-003/004/005, US2/AC1–3 and T011–015 (partial, HIGH).
- [x] T025 ([execution plan](exchange-acceptance.md)) Complete account deletion, role addition, authorization revocation, unrelated-cookie subject isolation and lost-response exchange tests under Issuer.Tests `Features/Protocol/`; execute actual60s expiry and negative RP issuer/audience/type/signature/nonce controls, per LOGIN-005/006/009, US3/AC1–4 and T017/018 (partial, HIGH).
- [ ] T026 Complete delayed-command/cancellation/rate/concurrency/log-sentinel tests, actual configuration-reload diagnostic guards and audit-failure consumption/bootstrap tests under Issuer.Tests `Features/Protocol/OperationsTests.cs` and Host.Tests `Features/Pilot/`; record native consumption outcomes and measured bounds, per LOGIN-008/009, SC-005 and T009/019 (partial, HIGH). Plan fixture isolation and execution budget using the slower observed feature 005 SQL Server issuer baseline of 10m32s under the unchanged 660s deadline (about 28s headroom; another run took 8m33s, prior T025 baseline 10m05s); added fault coverage must retain every required case.
- [ ] T027 Complete trusted/untrusted Chromium TLS controls, both-provider login/theme/error/system/freshness browser journeys, review deterministic visual baselines and wire Firefox/WebKit both-provider nightly/dependency execution in `.github/workflows/verify.yml` and Issuer.Tests `Features/Browser/`, per LOGIN-007/009, SC-004, plan browser cadence and T006/015/021 (partial, HIGH).
- [ ] T028 Run seeded critical-decision mutations and exact-final-head static/assurance/supply-chain/unit/both-provider/browser/combined90/80 coverage; obtain evidence-based specialist dispositions in `verification.md`, per LOGIN-009, SC-001/002/003 and T020/021 (partial, HIGH).
- [ ] T029 Reconcile requirement-to-test evidence, control-register references, roadmap/runbook/quickstart and PR readiness after T023–028 and T030; retain M01-E and all manual/external gates, per SC-003, constitution principles1/12/17/18 and T022 (partial, MEDIUM).

## Review checkpoint and next delivery

PR #4's seven review findings are tracked in [verification](verification.md#pr-4-review-follow-up): atomic session-replacement intent, safe incident diagnostics, truthful wordmark semantics, failure-safe secrecy assertions and the completed2920ae1 CI checkpoint. The user's subsequent fix/approve/squash instruction authorizes merging the bounded Development pilot once these fixes pass; it does not mark this feature complete. Continue with **T023** immediately afterward, then T024–T029. Existing unchecked criteria remain acceptance obligations.


T023 completion: [run38067841270](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38067841270) passed all seven gates for `7341d16b6fed3ef782827a9cc891ce4e9eebcf2a`, including49 new bootstrap cases on each provider and490 total .NET cases, with zero skips. See [verification](verification.md#t023-completed-ci-checkpoint). The next item is T024; T024–T029 and full feature acceptance remain open.


## Phase 8 — Discovered acceptance work

- [ ] T030 Resolve the concurrent browser-capacity gap in LoginInteractions.CreateAsync: two requests presenting seven active interactions can both allocate another. Implement and test an atomic eight-live-interaction budget consistent with LOGIN-004/plan/issuer contract, or obtain explicit approval for a concrete contract amendment. Cover simultaneous admission, completion/expiry capacity recovery, browser binding and both providers. Existing sequential capacity evidence is insufficient for the concurrent cap (partial, MEDIUM; discovered during T024 security review). Full M01-D acceptance remains blocked on this item.

Current delivery: PR #5 squash-merged as `d4664a3` after final-head490-case CI and zero open findings. T024 is complete in [PR #6](https://github.com/mosesparadise/OpenIdentityAdmin/pull/6): [run38073502300](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38073502300) passed all seven gates and578 .NET cases with zero skips, including all44 new cases on each provider. PR #6 squash-merged as `5aa330c` after final-head run38082495623 passed all seven gates and its chronology review finding was corrected. T025 is complete in [PR #7](https://github.com/mosesparadise/OpenIdentityAdmin/pull/7): source `fc990e7` passed all seven gates in [run38083498265](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38083498265), including 592 .NET cases with zero skips. PR #7 subsequently squash-merged as `9c52c2d` after all seven final-head checks passed. The user-requested feature 005 refactor precedes T026; T026–T030 remain outstanding, including the concurrent-capacity obligation.

## Sequencing update — host composition

PR #7 completed T025 and was squash-merged as `9c52c2d`. The user requested [feature 005 host composition](../005-host-composition/tasks.md) as the next separate PR before T026. T026–T030 remain pending; this structural work does not satisfy their acceptance.
