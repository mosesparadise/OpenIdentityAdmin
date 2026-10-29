# Tasks: Protected application list

Implementation is present. T011–T013 retain hosted acceptance obligations; checked implementation tasks below have the scoped local results recorded in [verification](verification.md).

## Design and prerequisites

- [x] T001 Record current authorization and feature specification/research/plan/contracts in specs/018-application-list/ and docs/approval.md.
- [x] T002 Complete independent design/security/test review and requirement traceability before affected implementation.
- [x] T003 Retain exact callback access token/original expiry in protected BFF ticket through existing atomic establishment; add receipt/binding/expiry/old-ticket regressions in BrowserSessions and matching Host/Administrator tests. FR-004/005.
- [x] T004 Add explicit API read ownership and reuse current source/session/profile/grant policy under declared lock order; prove rollback-only modes and metadata-only factor checks on both providers. FR-003.

## US1/US2 — Real protected list

- [x] T005 Add native API discovery/asymmetric/token+authorization validation, strict claims and original bounded deadline under Admin.Api/Features/Authentication; direct real-token and fault/revocation tests. FR-003/005.
- [x] T006 Add bounded ordinal native application summary query and explicit versioned API endpoint under Features/Applications; both-provider search/page/projection/input tests. FR-001/002/006.
- [x] T007 Add exact BFF management route, protected credential reader, fixed HTTPS transport and safe projection; header/redirect/body/expiry/deadline/substitution tests. FR-004/005/006.
- [x] T008 Extend authoritative contracts and regenerate browser types; compatibility and exact schema/metadata tests. FR-006.
- [x] T009 Add API runtime stage and native Aspire local-demo composition, real HTTPS fixtures and two-instance authority enforcement tests. FR-003/008.

## US3 — Visible Applications journey

- [x] T010 Add Applications navigation, generated client, search/paging and finite accessible UI states under frontend Features/Applications; preserve session/logout/theme behavior. FR-001/002/007.
- [ ] T011 Exercise real sign-in/list/search/page/logout and permission/expiry/dependency/stale-response states on both providers and retained browsers; extend both native local demos. FR-001–008.
- [ ] T012 Update native/Node discovery inventories, coverage inputs and critical mutation controls; run required checks and resolve independent source/test/UX/evidence findings. FR-001–008.
- [ ] T013 Record actual results/limits, update quickstarts/roadmap/handoff, and normal squash only after exact final-head checks and review. FR-008.

Dependencies: T002 precedes affected code; T003/T004 enableT005/T007; T005/T006/T007/T008/T009 enableT010/T011; T012/T013 require all earlier criteria. Root owns integration; independent reviewers do not replace human acceptance. No refresh, create/edit or production activation.
