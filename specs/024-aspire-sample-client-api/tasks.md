# Tasks: Interactive sample client and API

Scope: approved manual registration/protected dashboard setup and offline JWT API.
Tests are mandatory; each task includes negative and failure evidence from
[contracts/sample.md](contracts/sample.md). No runtime task is accepted yet; [local evidence](verification.md) distinguishes implementation from full delivery.

## Setup and foundations
- [x] T001 Verify remote baseline and source boundaries in research.md.
- [x] T002 Record both selected recommendations in spec.md and docs/approval.md.
- [x] T003 Complete plan.md, data-model.md, contracts/sample.md, quickstart.md and independent design/requirements review.
- [ ] T004 Add sample projects under src/samples/ and native state adapter under IdentityPlatform.Sample.Client/Features/State/; prove128record/32768payload/8192property/5minute bounds, detached copies, cancellation, CAS and real native concurrent redemption in tests/backend/IdentityPlatform.Host.Tests/Features/Samples/.
- [ ] T005 Add server-ticket/browser-attempt ownership in Sample.Client/Features/Sessions/ with64sessions/128attempts/64KiB tickets, original expiry cap, terminal logout/no-revival and native-cookie tests.

## US1 — Run and configure
- [ ] T006 [US1] Add exact4096byte one-shot clientId/clientSecret parsing/private tmpfs file in Sample.Client/Features/Configuration/; add dashboard stdin/run/container authorization in src/aspire/IdentityPlatform.AppHost/Features/LocalDemo/ with AppHost command tests.
- [ ] T007 [US1] Compose Development-only sample resource stages in scripts/local-demo/Dockerfile and AppHost; add waiting/ready UI, ordinary trust, independent credentials, restart/partial-failure cleanup and tests.

## US2 — Sign in and current profile
- [ ] T008 [US2] Add native code/S256/basic/no-PAR registration, metadata/transport bounds and callback/browser binding in Sample.Client/Features/Authentication/; test wrong metadata, nonce, state, replay and capacity.
- [ ] T009 [US2] Add protected POST login/logout/current-profile, finite encoded presentation and strict UserInfo sub/email projection in Sample.Client/Features/Presentation/ and ResourceAccess/; test CSRF, expiry, logout/publication races and two-browser isolation.

## US3 — Protected resource
- [ ] T010 [US3] Add native public-key bearer validation and exact orders.read policy in Sample.Api/Features/Validation/ and Orders/; prove wrong issuer/audience/type/signature/scope, expiry and discovery/key failures.
- [ ] T011 [US3] Add fixed sample API call and finite result/denial UI in Sample.Client/Features/ResourceAccess/ with original deadline/session ownership and no automatic retry.

## Integration and delivery
- [ ] T012 Add real sample registration/Allow/Cancel/native callback/UserInfo/API/logout journeys in tests/aspire/IdentityPlatform.AppHost.Tests/Features/Samples/ on both providers and retained browsers; preserve prior identities.
- [ ] T013 Bind new modules/cases/mutations into eng/coverage-policy.json, scripts/ and .github/workflows/verify.yml; retain unchanged floors and existing obligations; run required exact-head CI and reconcile originals.
- [ ] T014 Complete independent source/evidence reviews, resolve findings, update verification.md/quickstart.md/docs/roadmap.md/docs/operations/new-task-handoff.md and normal reviewed squash.

Dependencies: T001→T002→T003→T004→T005; T006 follows T003 and is independently
reviewable while state tests run; T007 follows T004–T006; T008 follows T004–T007;
T009 follows T008; T010 can follow T003 independently with its own host tests;
T011 follows T009/T010; T012–T014 converge in order. Main owns edits. Reviewers may
review independent source boundaries in parallel. No deferred security hardening;
no production/machine/refresh activation or historical diagnostic renewal.
