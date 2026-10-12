# Tasks: Management contract foundation

User confirmed both decisions on12October2026. Existing approved v0.2 scope applies. Full authenticated documentation remains deferred; these tasks deliver the bounded foundation only.

## Phase1 — Scope and design

- [x] T001 Record user decisions in specs/007-api-contract-foundation/spec.md and decision-proposal.md.
- [x] T002 Complete plan, data-model, contracts/foundation.md, quickstart and acceptance design in specs/007-api-contract-foundation/.
- [x] T003 Independently review design and run Spec Kit consistency analysis; record findings in specs/007-api-contract-foundation/verification.md.

## Phase2 — Compatible setup

- [ ] T004 Validate/pin NuGet graph in Directory.Packages.props and host package locks; keep Microsoft.OpenApi within compatible2.x constraints.
- [ ] T005 Add tests/contracts/IdentityPlatform.Api.Contracts.Tests and solution grouping in IdentityPlatform.slnx; create private src/frontend/IdentityPlatform.Admin.Contracts toolchain with locked compatible Node/TypeScript dependencies.

## Phase3 — US1 explicit surfaces

- [ ] T006 [US1] Add version/generation fixture tests in tests/contracts/IdentityPlatform.Api.Contracts.Tests/Features/Versioning and Features/Generation, using actual production registration and distinct synthetic operations.
- [ ] T007 [US1] Implement Features/ApiContracts/Setup.cs and generation metadata in both existing Admin hosts; preserve Composition/rootSetup and health/protocol separation.
- [ ] T008 [US1] Add deterministic actual-entry-point export and no-external-effect checks in scripts/ and dedicated contract tests; record exact version rejection responses.
- [ ] T009 [US1] Extend actual-host/real-provider tests to require ready controls and exact404 documentation absence; test Development/Staging/Production without a docs enable switch.

## Phase4 — US2 compatibility and consumers

- [ ] T010 [US2] Add fixture schemas with page default25/max100, nullable/composed/date/enum/error/precondition/security metadata under tests/contracts/IdentityPlatform.Api.Contracts.Tests/Fixtures.
- [ ] T011 [US2] Implement pinned validation/diff plus semantic security/precondition checks and seeded positive/negative mutations in src/frontend/IdentityPlatform.Admin.Contracts/tests and scripts/.
- [ ] T012 [US2] Generate actual BFF types separately from synthetic fixtures; compile positive/negative consumers and require typed HTTP cookie/URL/antiforgery behavior in src/frontend/IdentityPlatform.Admin.Contracts/tests.

## Phase5 — US3 read-only renderers

- [ ] T013 [US3] Add Features/Documentation/Setup.cs in existing Admin hosts without shipped mapping; configure one document set, local pinned assets, read-only behavior, CSP and shared theme.
- [ ] T014 [US3] Add trusted-HTTPS browser fixture and Scalar/Swagger semantics/security/theme tests in tests/contracts/IdentityPlatform.Api.Contracts.Tests/Features/Documentation; cover hostile description and no external network/storage leakage.

## Phase6 — Evidence and delivery

- [ ] T015 Extend .github/workflows/verify.yml, scripts/ and eng/ evidence discovery/count/coverage and npm/viewer supply-chain checks; isolate new execution from issuer deadlines.
- [ ] T016 Run focused and full required both-provider/browser/coverage checks; fix failures and reviewer findings; record actual evidence in specs/007-api-contract-foundation/verification.md.
- [ ] T017 Update quickstart/research/roadmap and PR20 title/body for final behavior; verify exact head/checks/comments and squash normally, recording delivery in verification.md.

## Dependencies and execution

T001–003 precede code; T004–005 precede production/test integration. US1 establishes registration/export; US2 depends on its nonempty fixtures; US3 consumes those same documents and components. T015 integration can be prepared alongside independent frontend tests but must include every final case before T016. T017 requires all foundation acceptance and exact-head review gates.

Independent read-only architecture/API/security/test reviews may run in parallel; main owns edits/integration. Each story's independent test is specified in spec.md and test-strategy.md. No unchecked deferred M02/M04 product acceptance is hidden in these completed-foundation tasks.
