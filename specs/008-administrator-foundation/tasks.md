# Tasks: Administrator foundation

Only increment1 is ready for detailed implementation review. User-approved delivery is three sequential PRs; later design tasks must finish before their dependent code. No implementation box is checked without recorded evidence.

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
- [ ] T014 [US1] Run required provider/regression/static/mutation/coverage checks, independent source reviews, and record exact results in specs/008-administrator-foundation/verification.md; correct findings before normal squash.
- [ ] T015 [US1] Replace planned quickstart commands with verified commands and update docs/roadmap.md; exact-head checks and live PR merge record complete increment1 delivery.

## Increment 2 — US2 MFA and restricted recovery

- [ ] T016 [US2] Complete interaction/secret-protection/expiry/attempt/freshness/all-factor-loss design in specs/008-administrator-foundation before implementing account journeys.
- [ ] T017 [US2] Implement approved enrollment/TOTP/recovery slices with explicit Setup under src/backend/IdentityPlatform.AuthServer/Features/Accounts and manager-compatible protected factor storage under Infrastructure/Features/Identity.
- [ ] T018 [US2] Prove real browser, cross-node replay/concurrency, failure/audit and secret-safe evidence in tests/aspire/IdentityPlatform.Administrator.Tests; review/fix/verify/squash the second PR.

## Increment 3 — US3 current authority and governance

- [ ] T019 [US3] Complete usable-administrator predicate and supported recovery-route contract in specs/008-administrator-foundation; preserve lockout defenses and incomplete-enrollment exclusion.
- [ ] T020 [US3] Implement current permission and governed mutation policies under Core/Infrastructure Features/Identity with explicit actor/permission/recent-MFA/concurrency checks.
- [ ] T021 [US3] Prove both-provider distributed last-admin and stale/current-authority scenarios in tests/aspire/IdentityPlatform.Administrator.Tests; review/fix/verify/squash the third PR.

## Dependencies and execution

T001–T003 -> T004–T008 -> T010–T012 -> T013–T015. T009 can be authored alongside database fixture work once its contracts exist. Increment2 starts after increment1 merge; increment3 follows increment2. Independent source reviewers may work in parallel, but the main agent integrates edits. T016–T021 are explicit future work packets, not a claim that their detailed plans or acceptance exist already. M02-B–D, M05 passkeys, M01-F vendor clearance and human/external assurance remain separate.
