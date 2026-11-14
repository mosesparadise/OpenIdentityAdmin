# Tasks: Scope and audience catalogue

User-selected batch: M03-A–M03-C including UI, dependencies and tests. No task is
accepted from planning alone. Feature022 and all preserved evidence remain complete.

## Setup and foundations

- [x] T001 Record selected scope/claims, approved constraints, source research and implementation packet in `specs/023-scope-audience-catalogue/`; complete independent architecture/security/test review and cross-artifact analysis. FR-001–013.
- [x] T002 Define bounded catalogue intents, projections, identifiers, versions and finite email policy in `src/core/IdentityPlatform.Core/Features/Catalogue/`; add meaningful boundary controls in `tests/core/IdentityPlatform.Core.Tests/Features/Catalogue/`. C01/C07.
- [x] T003 Add catalogue model and additive provider migrations through `src/persistence/IdentityPlatform.Infrastructure/Features/Catalogue/`, PlatformDbContext and both `src/migrations/IdentityPlatform.Migrations.*` projects; prove fresh/populated upgrades, histories, constraints and least privilege in `tests/persistence/IdentityPlatform.Persistence.Tests/Features/`. C05.

## US1 — Catalogue management (M03-A)

- [x] T004 [US1] Implement native-manager catalogue reads/writes, current authority, governance ordering, exact atomic audit, deletion admission/status and current/retained scope/audience reference checks in Infrastructure `Features/Catalogue/`; add real-provider fault/permission/concurrency cases in `tests/aspire/IdentityPlatform.Administrator.Tests/Features/Catalogue/`. Include the bounded durable deletion processor in `src/backend/IdentityPlatform.Worker/Features/` and existing Aspire composition; verify restart, expired/revoked approval, stale/reference conflict and atomic terminal audit. C01–04.
- [x] T005 [US1] Publish finite versioned catalogue Admin API endpoints and strict contracts in `src/backend/IdentityPlatform.Admin.Api/Features/Catalogue/`; preserve one-use owned commit and cleanup-before-publication. Test real HTTP/status/denial/preconditions/deadlines. Include202 admission and permission-bound job status. C01–04.
- [x] T006 [US1] Publish fixed catalogue BFF projections/native antiforgery in `src/backend/IdentityPlatform.Admin.Bff/Features/Catalogue/`; regenerate `eng/contracts/` and browser client, test original session/CSRF/one-send/JSON/contract compatibility. C01/C02/C13.
- [x] T007 [US1] Implement Scopes/Audiences navigation and list/detail/create/edit/delete-job status in `src/frontend/IdentityPlatform.Admin.Ui/Features/Catalogue/`; include all states, light/dark/reflow/keyboard/axe and real retained-engine/provider journeys. C13.

## US2 — Assignment and claim policy (M03-B)

- [x] T008 [US2] Add explicit versioned native application scope assignment through Infrastructure/API/BFF `Features/Applications/` and existing React editor; verify unsupported/system/disabled policy, stale versions, delete races and old editor preservation. C04/C06/C07.
- [x] T009 [US2] Add bounded current catalogue policy snapshots and additive managed-interaction bindings in Infrastructure `Features/Identity/ManagedApplications/`; preserve legacy rows, native permission/resource authority and both-provider upgrades. C05/C07/C08.
- [ ] T010 [US2] Integrate requested/granted scope decisions and explicit consent disclosure in AuthServer `Features/Protocol/ManagedApplications/` and managed Razor pages; recheck after locks and invalidate stale cached/consented/code decisions. Add real cross-process policy/cache/race evidence. C07/C08.
- [ ] T011 [US2] Extend managed native principal/prepared/issuance invariants for exact scopes, one audience and consented email ID-token destinations; preserve replay/consumption/commit/publication budgets. Add independent crypto/native-state tests on both providers. C07–09.

## US3 — Real token/UserInfo validation (M03-C)

- [ ] T012 [US3] Add native authenticated GET/POST UserInfo and current-state projection under AuthServer `Features/Protocol/UserInfo/`, including current native token/authorization/account/client/policy checks, exactsubject, bounded responses and safe native errors. C10/C11.
- [ ] T013 [US3] Update exact known UserInfo discovery compatibility in BFF `Features/Authentication/Protocol/` while preserving both no-send guards; test real fixed login/logout and hostile metadata. C12.
- [ ] T014 [US3] Add full managed UI→scope assignment→native token→UserInfo scenarios in `tests/aspire/IdentityPlatform.Administrator.Tests/Features/ManagedApplications/` and catalogue fixtures, including missing/unconfirmed/changed email, scope revocation and unrelated/system/machine negatives. C09–13.

## Integrated acceptance and delivery

- [ ] T015 Bind actual discovery/selection, unchanged existing identities, new coverage and behavioral mutations in `scripts/`, `.github/workflows/`, `eng/` and `verification.md`; execute required both-provider/browser/full final-head checks and reconcile original reports/manifests. C14.
- [ ] T016 Complete independent architecture/security/API/UX/test/provider reviews; update `quickstart.md`, `verification.md`, `docs/roadmap.md` and `docs/operations/new-task-handoff.md`, then normal reviewed squash after exact-head acceptance. FR-001–013.

Dependencies: T001→T002→T003→T004→T005→T006→T007. T008 follows T004–T006;
T009 follows T003/T008; T010 follows T009; T011 follows T010; T012 follows T011;
T013 follows T012; T014 follows all three stories; T015→T016 closes the batch.
Independent design/security/test reviews can run in parallel; main agent owns source
integration. UI tests may be prepared alongside endpoint work after contracts settle.
No runtime task is declared independently parallel before its prerequisites exist.

Deliver coherent reviewed increments, preserving old application behavior at each
boundary. Catalogue-only progress does not complete the selected M03-B/C tasks.

## Accepted T004–T007 increment

The user-selected T005–T007 and required T004 prerequisites are delivered in PR128.
[Final acceptance](acceptance.md) records the exact source, both-provider/browser/full-CI
results, unchanged coverage floors, behavioral mutation controls and independent reviews.
T001–T007 are complete for internal development. T008 and T009 were subsequently delivered in PR130 and PR132; T010–T016 remain open.
The T015/T016 obligations were applied to this increment and remain required for the
later assignment/claims/UserInfo work. No historical/manual/public-release gate closes.

## Accepted T008 increment

Explicit application scope assignment is delivered in PR130. [Assignment acceptance](assignment-acceptance.md) records full exact-head CI, both providers and retained browsers, independent reviews and the explicit temporary sign-in hold. T009 policy bindings were subsequently delivered in PR132; no consent, extended issuance, UserInfo or historical/manual gate is declared complete.

## Accepted T009 increment

Managed current-policy snapshots and additive interaction bindings are delivered in PR132. [Policy acceptance](policy-acceptance.md) records both-provider full CI, original report/coverage/mutation reconciliation and independent reviews. T010 consent is next; nonempty assignments still block issuance. T010–T016, M03-B/C completion and all historical/manual/public-release gates remain open.

## Active T010–T012 batch

[Consented issuance](consented-issuance.md) records implementation and pending full validation, including required T013 compatibility. Tasks remain unchecked until both-provider/browser evidence, unchanged coverage floors, causal mutations and final independent review pass.
