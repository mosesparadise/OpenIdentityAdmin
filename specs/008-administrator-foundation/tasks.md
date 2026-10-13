# Tasks: Administrator foundation

**Delivery hold (13 October 2026):** A-004 expired on incomplete required SQL mutation evidence in PR48. T020/T021 remain incomplete and unmerged; no runtime continuation is authorized. [Verification and proposed diagnostics](governance-verification.md). Earlier implementation checkpoints below remain historical observations, not current delivery authority.

Increment 1 was squash-merged in PR #21 as `38535bd`; its verification record and PR contain the final evidence. The three approved increments are implemented through separately reviewed and squash-merged task PRs; later design tasks must finish before their dependent code. No implementation box is checked without recorded evidence.

## Setup and shared design

- [x] T001 Record user sequencing/MFA/structure decisions in specs/008-administrator-foundation/decision-brief.md.
- [x] T002 Inspect existing manager, Migrator, pilot and runner boundaries in specs/008-administrator-foundation/research.md.
- [x] T003 Converge independent architecture/security/test design review in specs/008-administrator-foundation/verification.md.

## Increment 1 — US1 bootstrap and storage

- [x] T004 [US1] Define explicit v1 permission catalogue/bundles in src/core/IdentityPlatform.Core/Features/Identity/Permissions; no wildcard or implicit future grants.
- [x] T005 [US1] Add governance singleton (Id=1, nonnegative long revision), permanent receipt and Pending enrollment (UserId max450) to src/persistence/IdentityPlatform.Infrastructure/Features/Identity/Persistence, following data-model.md.
- [x] T006 [US1] Generate both provider migrations under src/migrations and update src/persistence/IdentityPlatform.Infrastructure/Features/Health/SchemaCatalog.cs; preserve existing data.
- [x] T007 [US1] Add bounded redirected-input parsing (4096 UTF-8 bytes, exact fields, no duplicates) and explicit operation dispatch under src/migrations/IdentityPlatform.Migrator/Features/Bootstrap/Initialize and existing Composition/Setup.
- [x] T008 [US1] Implement native manager bootstrap transaction and safe classified outcomes under src/migrations/IdentityPlatform.Migrator/Features/Bootstrap/Initialize; permanent consumption, disabled pending user, no repair, shared UPDATE lock and atomic audit.
- [x] T009 [P] [US1] Add deterministic parser/configuration tests in the existing tests/backend project and independent permission-bundle oracles in the dedicated provider project, using Features.
- [x] T010 [US1] Create tests/aspire/IdentityPlatform.Administrator.Tests with Features/Bootstrap and Fixtures, under Aspire in IdentityPlatform.slnx.
- [x] T011 [US1] Implement BOOT-01–04 and BOOT-08/09 real-provider/process/manager cases in tests/aspire/IdentityPlatform.Administrator.Tests/Features/Bootstrap.
- [x] T012 [US1] Implement observable race, audit denial, cancellation/crash and lost-result cases BOOT-05–07 in tests/aspire/IdentityPlatform.Administrator.Tests/Features/Bootstrap; no secret-bearing assertion output.
- [x] T013 [US1] Add strict isolated administrator provider selection and seeded evidence controls in scripts, eng/test-matrix.json and .github/workflows/verify.yml; require both new coverage inputs without enlarging issuer invocations.
- [x] T014 [US1] Run required provider/regression/static/mutation/coverage checks, independent source reviews, and record exact results in specs/008-administrator-foundation/verification.md; correct findings before normal squash.
- [x] T015 [US1] Replace planned quickstart commands with verified commands and update docs/roadmap.md; use PR #21 for the authoritative final documentation checks and normal squash record.

## Increment 2 — US2 MFA and restricted recovery

- [x] T016 [US2] Complete and independently review interaction/secret-protection/expiry/attempt/freshness/all-factor-loss design in specs/008-administrator-foundation/mfa-design.md before implementing account journeys.
- [ ] T017 [US2] Implement approved enrollment/TOTP/recovery slices with explicit Setup under src/backend/IdentityPlatform.AuthServer/Features/Accounts and manager-compatible protected factor storage under Infrastructure/Features/Identity.
- [ ] T018 [US2] Prove real browser, cross-node replay/concurrency, failure/audit and secret-safe evidence in tests/aspire/IdentityPlatform.Administrator.Tests; review/fix/verify/squash the second PR.

### Detailed implementation packets

T017 is the implementation umbrella for T022–T026; T018 is the acceptance umbrella for T027–T029. Existing IDs remain stable. Every ID here belongs to feature 008; it does not replace same-numbered tasks in another feature.

- [x] T022 [US2] Refactor shared account composition and explicit administrator/pilot Razor/scheme isolation under AuthServer/Features/Accounts and existing Composition/Setup; install independent request/database/security protection.
- [x] T023 [US2] Add authentication/factor/interaction/session/admission state and protected native manager/store adapters under Infrastructure/Features/Identity; generate both migrations and SchemaCatalog readiness updates.
- [x] T024 [US2] Implement authoritative budgets, complete lock order, replay/watermark, atomic audit, retained-capacity admission and bounded AuthServer-hosted cleanup. Accepted in PR #29 (`5d54e57`); see [ceremony security](ceremony-security.md).
- [x] T025 [US2] Implement protected Enrollment/Mfa/login Razor journeys, destination-generation proof continuity and authoritative account-session validation. Accepted in PR #30 (`78d77c3`); see [enrollment/login](enrollment-login.md).
- [x] T026 [US2] Implement restricted Recovery, Security factor replacement/code regeneration and local logout; preserve all-factor-loss denial and one-time code delivery. Accepted in PR #31 (`c10f9d2`); see [recovery/security](recovery-security.md).
- [x] T027 [US2] Extend strict Administrator discovery into explicit story/provider/browser partitions in scripts and eng; retain 660-second invocation bounds and require all new coverage inputs. [T027 reconciliation](evidence-gates.md) accepted in PR #33 (`ab8b62d`), with all 58 exact-head checks and nine coverage floors passing.
- [ ] T028 [US2] Implement MFA-01–13 independent cases from mfa-design.md in the approved Administrator suite; verify native managers, real HTTP/browser, both-provider observed races/failures and safe evidence. Automated scope accepted in PR #34 (`fafc98d`); [behavioral reconciliation](mfa-evidence.md) records all 58 exact-head checks. Human password-manager/screen-reader observations remain unexecuted and keep this overall item open.
- [ ] T029 [US2] Converge architecture/security/test/UX/performance reviews, fix findings, run exact-head full CI, update operational instructions/verification and normally squash the second implementation PR. [Operational convergence](operations-evidence.md) provides the scoped telemetry fix and human handoff; PR #35 was squash-merged as `8792f85` after final head `eb2cd0a` passed all 62 checks. A-003 expired at merge. ADM-OPS-01, the separate post-merge ADM-EVID-01 failure and actual manual observations still block full acceptance; see the [investigation](startup-investigation.md).

T023 was squash-merged in [PR #27](https://github.com/mosesparadise/OpenIdentityAdmin/pull/27) as `824220b`; [verification](verification.md#t023-factor-storage--12-october-2026) records exact-head acceptance. T023 pulls forward only T027's necessary Bootstrap/Factors story partitions and coverage inputs. Browser/story acceptance for complete MFA was still pending at that checkpoint. T027 is now accepted; T028 retains behavioral/human acceptance obligations. See [storage design and review dispositions](factor-storage.md).

Dependencies: T016 -> T022/T023 -> T024 -> T025/T026; T027 enables isolated T028 evidence; T025/T026/T028 -> T029. Author tests with each behavior rather than deferring all tests until the end. T019–T021 remain subsequent third-increment work.

## Increment 3 — US3 current authority and governance

- [x] T019 [US3] Complete usable-administrator predicate and supported recovery-route contract in [governance-design.md](governance-design.md); preserve lockout defenses and incomplete-enrollment exclusion. Independent architecture/security/test design reviews converged with GOV-01–12 planned acceptance families; its documentation PR supplies final checks/normal squash. Runtime T020/T021 is now authorized only within explicitly approved A-004; historical findings and prior acceptance obligations remain unresolved.
- [ ] T020 [US3] Implement current permission and governed mutation policies under Core/Infrastructure Features/Identity with explicit actor/permission/recent-MFA/concurrency checks.
- [ ] T021 [US3] Prove both-provider distributed last-admin and stale/current-authority scenarios in tests/aspire/IdentityPlatform.Administrator.Tests; review/fix/verify/squash the third PR.

## Dependencies and execution

T001–T003 -> T004–T008 -> T010–T012 -> T013–T015. T009 can be authored alongside database fixture work once its contracts exist. Increment2 starts after increment1 merge; increment3 follows increment2. Independent source reviewers may work in parallel, but the main agent integrates edits. T016 is a reviewed design checkpoint; T022 composition and T023 protected storage are verified; T024–T026 have scoped runtime acceptance; T027 is accepted; T017/T018 and T028/T029 remain unchecked until overall increment evidence, including human observations, is reconciled. M02-B–D, M05 passkeys, M01-F vendor clearance and human/external assurance remain separate.

## Diagnostic follow-up after scoped T029 delivery

These tasks preserve prior IDs and acceptance obligations. T030 is a bounded evidence record, not incident closure or a renewal of A-003. Diagnostic work is separate from blocked subsequent product feature implementation.

- [x] T030 Reconcile original startup logs/pinned Aspire semantics, retain the separate post-merge Firefox failure, execute discriminating disposable PostgreSQL controls, remove the temporary probe, and independently review the causal limits in [startup-investigation.md](startup-investigation.md) and [worker-experiment.md](worker-experiment.md).
- [x] T031 Add bounded sanitized startup-transition evidence and adversarial controls in the existing AppHost test project; exercise the unchanged SQL Development profile in controlled CI, retain every outcome, and investigate decisive state/exit/phase evidence for ADM-OPS-01. A green run alone cannot close it. Implementation and 20 diagnostic controls are recorded in [the T031 packet](startup-investigation.md#t031-diagnostic-implementation--13-october-2026); PR #37 (`1a043c0`) passed all 62 exact-head checks in run `38363216011`; this scoped diagnostic delivery is complete, while ADM-OPS-01 remains unresolved.
- [x] T032 Add safe route-completion/phase evidence and incomplete/secret-document controls in the existing Administrator browser tests, preserving one absolute five-second observation budget; compare fixed isolated/overlapping executions for ADM-EVID-01. See [the T032 implementation packet](history-diagnostics.md). Establish a causal disposition and matching regression before incident closure; diagnostic delivery alone cannot supply it. Scoped delivery merged in PR #38 (`fbd8ca9`) after all 62 exact-head checks in run `38368154130`; ADM-EVID-01 remains unresolved.

T031/T032 require independently reviewed diagnostic changes and exact-head checks. Neither task may weaken deadlines, turn timeouts into acceptance, add blanket retries, or use expired A-003 to accept subsequent feature work. The implementation lead owns these investigations; human assessment and external assurance remain with their named operators/assessors.

## Approved bounded investigation and acceptance batch — 13 October 2026

- [x] T033 Complete the bounded Worker cohort and reviewed both-provider configuration-failure/database-loss comparison in [worker-availability-investigation.md](worker-availability-investigation.md). PR #44 squash `65031d7` passed all 62 final-head checks in run `38390215923`. Preserve the failed prior SQL/WebKit evidence run and all three unresolved findings; diagnostic delivery supplies no historical causal closure.
- [x] T034 Complete the independently reviewed [Firefox evidence reconciliation](firefox-history-investigation.md): seven complete retrospective comparisons, one incomplete cancellation and prescribed T033 regressions. PR #45 squash `b990745` passed required documentation checks and final acceptance in run `38392155746`. No new browser experiment or causal closure is claimed; ADM-EVID-01 remains acceptance/release-blocking.
- [x] T035 Prepare the [acceptance reconciliation](acceptance-reconciliation.md), user-owned [Windows/WSL2 handoff](../../docs/operations/administrator-manual-handoff.md) and independently reviewed [A-004 proposal](../../docs/amendments/A-004-governance-sequencing.md). PR #46 was normally squash-merged as `9e72770` after all 62 final-head jobs passed in run `38395691033` and independent review reconciled all four mutation artifacts. Its initial required mutation timeout is retained; reviewed phase diagnostics and 35 verifier controls preserve all budgets and acceptance oracles. All 48 human rows remain not_run; all four incident findings and whole-increment/release gates remain open. A-004 is inactive until explicitly approved; T020/T021 remain blocked.

The existing eleven specification-quality checklist items pass; checklist state is not implementation acceptance. No Spec Kit extension hooks are configured. T033–T035 follow the user's approved recommendation; they do not renew expired A-003. Relevant controls: ADMIN-011, FR-21–23, NFR-08/11/14. Main agent owns implementation and evidence; independent architecture/Aspire/test/security review applies by boundary. Human observations cannot be supplied by automated or AI source review.

## A-004-authorized governance execution — 13 October 2026

The user explicitly approved A-004 after T035 delivery. T020/T021 remain unchecked while the [concrete implementation/test packet](governance-implementation.md), published at `e5f43f6` before coding, is being implemented and verified. The approved feature hierarchy, test partitions and isolated test driver are in place; exact-head full delivery evidence remains pending. The user approved feature-local Governance folders, three test partitions and the dedicated test-only driver. Record approval without treating it as implemented behavior or closure of the four historical findings. Preserve full verification and recurrence expiry. Earlier inactive-proposal statements describe the T035 checkpoint; the current authority is [A-004](../../docs/amendments/A-004-governance-sequencing.md).
