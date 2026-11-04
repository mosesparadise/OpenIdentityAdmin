# Tasks: Managed application lifecycle

**Status**: Implementation has not started. This packet completes the handoff's planning work;
checkboxes below track delivery work, not document creation. The single user scope preference
in [spec.md](spec.md) remains pending. Do not infer an answer from elapsed time.

## Phase 1 — Resolve the bounded scope

- [ ] T001 Record the grant-revocation scope answer and close independent requirements review in `specs/020-application-lifecycle/spec.md`, `review.md` and `checklists/lifecycle.md`; expand affected design/tasks before code if durable bulk revocation is selected.

## Phase 2 — Shared prerequisites

- [ ] T002 Add lifecycle state and finite linked audit-reason model in `src/persistence/IdentityPlatform.Infrastructure/Features/Applications/`; generate additive migrations in both provider `Platform/` directories; prove fresh/populated upgrade and unchanged prior data/history in `tests/persistence/IdentityPlatform.Persistence.Tests/Features/ProviderCompatibility/`. Cover rollback/mixed-version operating limits.
- [ ] T003 Add finite lifecycle input/state DTOs and immutable command construction in `src/core/IdentityPlatform.Core/Features/Applications/` and Infrastructure `ApplicationMutationCommand.cs`; retain exact version, confirmation, reason and unknown-field rejection. Reject reason case/whitespace aliases on both providers and independently prove the database rejects codes outside the closed reason enum.
- [ ] T004 Replace implicit action fallbacks with closed dispatch in `ApplicationMutationWriter.cs` and `src/backend/IdentityPlatform.Admin.Api/Features/Applications/ApiMutationAuthority.cs`; bind lifecycle permission and original fresh-MFA deadline, preserve existing action behavior and reject unknown actions.

## Phase 3 — US1: Disable

Goal: one authorized, audited state transition stops genuine further issuance.

- [ ] T005 [US1] Implement disable through `ApplicationMutationOperation.cs`, `ApplicationMutationWriter.cs`, `ApplicationWriteState.cs` and the linked reason model; fresh persisted state/audit/reason checks precede the one commit and actual cleanup. Add rollback, same-state, protected-target, wrong-permission and stale-version cases in `tests/aspire/IdentityPlatform.Administrator.Tests/Features/Applications/`.
- [ ] T006 [US1] Enforce Enabled throughout `ManagedRegistration.cs` and review every managed cache/human/consumption/issuance boundary; extend `tests/aspire/IdentityPlatform.Administrator.Tests/Features/ManagedApplications/` for fresh admission, each pending human phase, code issuance/exchange and durable-consumption-before-denial. While the target is disabled, a second independent registration must still complete a genuine fresh password/MFA/consent/exchange journey; verify no lifecycle mutation changed its policy, credentials or existing grants.
- [ ] T007 [US1] Add versioned disable route/projection and protected fixed BFF proxy in both hosts' `Features/Applications/`; update authoritative contract metadata, `tests/contracts/IdentityPlatform.Api.Contracts.Tests/`, host endpoint tests and generated `src/frontend/IdentityPlatform.Admin.Contracts/` outputs. Prove matching real API/BFF parsing and record the coordinated/quiesced switch required by strict unmapped-member rejection; do not claim untested mixed-version compatibility.
- [ ] T008 [US1] Add status, reason/confirmation and effect text to `src/frontend/IdentityPlatform.Admin.Ui/Features/Applications/Editor/`, with generated input and current-authority lifecycle handling; add meaningful UI state/transport tests without automatic resubmission.

Independent acceptance: actual UI disable plus genuine independent issuer denial on both providers.

## Phase 4 — US2: Re-enable

Goal: a new sign-in can succeed, while all pre-transition authority remains unusable.

- [ ] T009 [US2] Add explicit enable action through the same command/authority/transaction and host/contract paths; advance only aggregate revision, retain reason/audit atomicity and preserve credentials. Cover current same-state and stale requests in `tests/aspire/IdentityPlatform.Administrator.Tests/Features/Applications/`.
- [ ] T010 [US2] Prove old cached request, every pending human phase and old code still fail after re-enable, while a fresh real journey succeeds, in `tests/aspire/IdentityPlatform.Administrator.Tests/Features/ManagedApplications/`; prove expired credentials remain expired and edit/replacement preserve Disabled.
- [ ] T011 [US2] Integrate re-enable confirmation, expiry distinction and accessible focus/recovery in `src/frontend/IdentityPlatform.Admin.Ui/Features/Applications/Editor/`; update generated browser contract assertions and UI tests.

Independent acceptance: real disabled → enabled fresh-flow success, with old artifacts denied and no credential renewal.

## Phase 5 — US3: Contention and recovery

Goal: failures and races have one explainable outcome and the UI recovers deliberately.

- [ ] T012 [US3] Add independent-root, provider-observed lock-wait tests in `tests/aspire/IdentityPlatform.Administrator.Tests/Features/Applications/` and `Features/ManagedApplications/`: duplicate transitions, disable versus edit/replacement, opposite state transitions, and both issuance/exchange commit orders. Include a sealed response whose issuance commits before disable but arrives afterward; classify it as prior issuance, not an enforcement failure. Keep exact loser-state/audit assertions and five-second enforcement observation.
- [ ] T013 [US3] Extend actual audit INSERT denial, post-save failure, current permission/MFA expiry after waits, cancellation/deadline/clock rollback, commit ambiguity and publication cases in the same application/managed fixtures; independently inspect persisted state/reason/audit and require no retries. Attribute isolated MFA-expiry 403 to the component authority seam; genuine HTTPS must prove its earlier native bearer/continuation deadline without lengthening that lifetime.
- [ ] T014 [US3] Extend BFF/direct HTTP and UI cases in `tests/aspire/IdentityPlatform.Administrator.Tests/Features/Applications/`, `Features/Browser/` and `src/frontend/IdentityPlatform.Admin.Ui/`: origin/CSRF/generation, malformed preconditions/body, lost permission, stale screen, uncertain result, logout/navigation/expiry and late-response suppression.

Independent acceptance: winner/loser persistence is exact; a lost response produces a deliberate reload, not a duplicate action.

## Phase 6 — Integrated delivery

- [ ] T015 Extend retained Chromium/WebKit stories and both native Aspire demos in `tests/aspire/IdentityPlatform.Administrator.Tests/Features/Browser/` and `tests/aspire/IdentityPlatform.AppHost.Tests/`: create → sign in → disable → denial → enable → fresh success. Retain keyboard/focus, responsive light/dark and accessibility checks plus explicit manual deferrals.
- [ ] T016 Update actual discovery/inventories in `eng/administrator-test-matrix.json`, `eng/test-matrix.json` and affected contract/Node inventories; place new managed cases in existing Admission/Issuance partitions, preserve every prior case, and add decisive lifecycle/current-authority/revision/audit mutation controls without lowering module coverage floors.
- [ ] T017 Complete independent architecture/security/API/UX/test reviews, exact-head applicable hosted verification and source/tested-merge reconciliation in `specs/020-application-lifecycle/verification.md`; update `docs/roadmap.md`, this task list and `docs/operations/new-task-handoff.md`, then deliver through reviewed squash. Document actual scope and evidence limits.

## Dependencies and execution strategy

T001 precedes affected implementation. T002–T004 establish shared storage/contracts/authority,
then T005/T006 establish disable safety before T007/T008 expose it. US2 reuses those foundations;
T009 precedes T010/T011. T012–T014 cover the integrated actions. T015–T017 are required before
feature acceptance, not optional hardening. Do not ship an exposed disable button before issuer
enforcement exists. Start with US1 as the first demonstrable increment, then finish US2/US3.

Root owns edits and integration. Architecture/security and test/UX review can run in parallel on
the same fixed candidate. Within US1, independent contract and issuer review can overlap after
T005/T006; within US2, UI and stale-artifact review can overlap after T009; within US3, UI recovery
review and database race review can overlap. Serialize .NET builds and provider runtimes as
required by the environment; no parallel marker promises independent writes to shared files.

## Requirement traceability

| Requirement | Tasks | Meaningful acceptance |
| --- | --- | --- |
| FR-001 | T002, T005, T007–T011 | Managed state visible; system/native-only targets protected |
| FR-002 | T004, T005, T009, T013, T014 | Exact current lifecycle permission and MFA before/after waits |
| FR-003 | T003, T005, T007, T009, T012, T014 | Confirmation/reason/version, same-state and stale failures |
| FR-004 | T002, T005, T009, T012, T013 | State/version/audit/reason atomicity and exact loser outcomes |
| FR-005 | T002, T005, T009, T010 | Upgrade preservation; lifecycle versus edit/credential independence |
| FR-006 | T006, T012, T013 | Real current issuer denial and fail-closed publication |
| FR-007 | T006, T009, T010 | No stale artifact resurrection; fresh journey succeeds |
| FR-008 | T012, T013 | Independent roots, observed wait and both commit orders |
| FR-009 | T004, T007, T013, T014 | Direct API/BFF protection and original budgets |
| FR-010 | T001, T008, T011, T015 | Accurate effect text; no false revocation/audience proof |
| FR-011 | T008, T011, T014, T015 | UI states, keyboard/focus/themes and late-response suppression |
| FR-012 | T002, T005, T006, T010, T012, T013 | Both-provider schema/runtime evidence |
| FR-013 | T007, T009, T015, T016, T017 | Contracts, exact identities, coverage, mutation, native demos |
| FR-014 | T002, T017 | Runbook/recovery/evidence and external gates |
| SC-001 | T006, T010, T015 | Full visible journey on both providers |
| SC-002 | T005, T009, T012–T014 | Exact positive/negative persisted outcomes |
| SC-003 | T012, T013, T015 | Five-second bound and honest prior issuance distinction |
| SC-004 | T014–T017 | Retained browser and candidate-bound verification |
| SC-005 | T015, T017 | Ordinary Aspire and accurate manual/external limits |

This table maps planned work; it is not a claim that new tests exist or pass.
