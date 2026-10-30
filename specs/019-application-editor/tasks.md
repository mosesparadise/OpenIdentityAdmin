# Tasks: Application creation and editing

## Setup and design

- [x] T001 Record the user's three selected choices and current main in specs/019-application-editor/spec.md, research.md and docs/approval.md. FR-002–004.
- [x] T002 Complete independent architecture/security/test design review and traceability in specs/019-application-editor/verification.md; resolve native caching, managed authentication purpose and publication boundaries before integration. FR-001–012.

## Foundational prerequisites

- [x] T003 Characterize real native authorization caching, cached subtype/restoration, cached-request/code redemption transaction boundaries and the required managed publication seams in tests/aspire/IdentityPlatform.Administrator.Tests/Features/ManagedApplications/. FR-002–004/012.
- [x] T004 Implement bounded profile/input/DTO/version contracts in src/core/IdentityPlatform.Core/Features/Applications/ and permanent Host tests: clientId1–100 UTF-16/no whitespace or controls, displayName1–200/no controls, redirects1–10/exact HTTPS/max2048, required future whole-second UTC expiry. FR-001/002/007/010.
- [x] T005 Add ApplicationPolicy and exact managed authentication/consent model under src/persistence/IdentityPlatform.Infrastructure/Features/Applications/ and Features/Identity/ManagedApplications/; append both provider Platform migrations/descriptors and exact upgrade witnesses. FR-006/008/012.
- [ ] T006 Add owned current mutation authority and native-manager/audit transaction in src/persistence/IdentityPlatform.Infrastructure/Features/Applications/ and Admin.Api/Features/Applications/; preserve ApiRead rollback-only. FR-005/006/008.
- [ ] T007 Add genuine JSON antiforgery binding/token acquisition and fixed bounded mutation transport in src/backend/IdentityPlatform.Admin.Bff/Features/Applications/ with Host/provider/browser negatives. FR-009.

## US1 — Create a usable registration

- [ ] T008 [US1] Prove real both-provider create/duplicate/permission/audit-failure/expiry/system-binding behavior in tests/aspire/IdentityPlatform.Administrator.Tests/Features/Applications/ before accepting creation. FR-001/005/006/008/010.
- [ ] T009 [US1] Add sanitized details and create endpoints in src/backend/IdentityPlatform.Admin.Api/Features/Applications/ plus their exact BFF projections; generated native secret appears once only. FR-001/005/006/010/011.
- [ ] T010 [US1] Add a separate managed-client native profile/admission/cache/decision flow in src/backend/IdentityPlatform.AuthServer/Features/Protocol/ManagedApplications/, retaining Owned storage and fixed administrator/pilot guards. FR-002–004.
- [ ] T011 [US1] Implement managed-purpose password/MFA and explicit-consent state transitions under Infrastructure/Features/Identity/ManagedApplications/ and AuthServer/Features/Accounts/ManagedApplications/; create no administrator session/cookie or BFF grant. FR-004/008/009.
- [ ] T012 [US1] Implement managed native code/token generation, durable consumption, current policy/source checks and committed response publication in AuthServer/Features/Protocol/ManagedApplications/; preserve native crypto/replay/formatters. FR-002–006/010.
- [ ] T013 [US1] Add actual created-client cache→fresh password/MFA→consent→code/token positive and denial/replay/drift tests, including later issuance/audit failure followed by original cached-request and code replay, on both providers in tests/aspire/IdentityPlatform.Administrator.Tests/Features/ManagedApplications/. FR-002–004/012.
- [ ] T014 [US1] Add create/details/one-time-output UI in src/frontend/IdentityPlatform.Admin.Ui/Features/Applications/ and genuine browser/native Aspire journey coverage. FR-001/009–012.

## US2 — Safe edits and credential recovery

- [ ] T015 [US2] Prove both-provider same-version edits/replacements and native-save/audit rollback in tests/aspire/IdentityPlatform.Administrator.Tests/Features/Applications/. FR-005–008/010.
- [ ] T016 [US2] Implement If-Match PUT and explicit secret replacement in Admin.Api/Features/Applications/ and matching BFF endpoints; PUT cannot modify credential expiry, and replacement requires current credential permission/recent MFA. FR-005–011.
- [ ] T017 [US2] Add editor/conflict/replacement forms in Admin.Ui/Features/Applications/ and verify new issuer configuration works while old code/configuration/secret is rejected. FR-001/007/010/012.

## US3 — Clear, accessible, bounded failure states

- [ ] T018 [US3] Implement field errors, stale edit, ambiguous result, forbidden/expired, no-auto-retry and sensitive-output dismissal states under Admin.Ui/Features/Applications/; verify exact Node identities and bounded memory handling. FR-009/010/012.
- [ ] T019 [US3] Run retained Chromium/WebKit create/edit/consent/secrecy/keyboard/themes/reflow/accessibility journeys on both providers under Administrator.Tests/Features/Browser/ and both native Aspire demos. FR-001–012.

## Contracts and delivery

- [ ] T020 Generate authoritative Admin API/BFF contracts and TypeScript client; extend contract inventory/compatibility/ETag/CSRF tests in tests/contracts/ and frontend Contracts. FR-007/009/011.
- [ ] T021 Preserve exact prior native/Node inventories, add selected scenario lanes/coverage inputs and meaningful write/CSRF/audit/concurrency/credential/consent/claim mutation controls in eng/ and scripts/. FR-012.
- [ ] T022 Resolve independent final source/security/test/UX reviews; run required exact-head full verification and reconcile failures without weaker gates in specs/019-application-editor/verification.md. FR-001–012.
- [ ] T023 Record actual evidence and limits, update quickstarts/docs/roadmap.md/docs/operations/new-task-handoff.md, and complete normal reviewed squash. FR-012.

Dependencies: T001→T002; T003 precedes managed native integration; T004/T005/T006/T007 enable actual writes. T010/T011/T012 form the genuine issuer prerequisite for US1; no stored-row-only acceptance. US2 builds on the same aggregate authority/version and US1 native binding. T020 is maintained with each endpoint, not postponed until UI consumption. T019/T021/T022/T023 require all affected stories. Independent design reviews, contract checks and provider observations can run in parallel; root owns source edits/integration. No production deployment, refresh activation or external release is included.
