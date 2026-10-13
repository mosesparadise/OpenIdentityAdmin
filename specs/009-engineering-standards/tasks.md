# Tasks: Engineering standards

## Governance and foundation
- [x] T001 Record approved standards in .specify/memory/constitution.md and user decisions in specs/009-engineering-standards/spec.md.
- [x] T002 Propagate principal-engineer rules to AGENTS.md, .agents/skills/identity-*/SKILL.md, .codex/agents and docs/engineering/code-quality.md.

## US1 — Proportional CI
- [x] T003 [US1] Implement conservative change selection and final job-result validation in scripts/ci_scope.py.
- [x] T004 [US1] Prove docs/mixed/unknown/deleted/renamed/required-outcome controls in scripts/test_ci_scope.py.
- [x] T005 [US1] Wire lightweight checks, full-test conditions and final acceptance into .github/workflows/verify.yml; preserve full manual/nightly runs and secret checks.

## US2 — Enforceable code quality
- [x] T006 [US2] Implement syntax-aware scenario/documentation/size/literal checks and seeded controls in eng/IdentityPlatform.CodeQuality under Engineering in IdentityPlatform.slnx.
- [x] T007 [US2] Inventory legacy debt and reviewed exceptions under eng/code-quality; enforce changed findings and baseline expansion, link remediation to T013–T016.
- [x] T008 [US2] Integrate the tool into scripts/check-static-analysis.py and scripts/check-ai-instructions.py; verify with clean/rejected controls and document limits.
- [x] T009 [US2] Independently review, fix, run required gates, update specs/009-engineering-standards/verification.md and squash the adoption PR.

## US3 — OpenAPI configuration
- [x] T010 [US3] Move metadata to validated appsettings-backed options in backend Features/ApiContracts; preserve supported versions and security gates.
- [x] T011 [US3] Add configuration and actual hostile-document presence regressions in tests/contracts/IdentityPlatform.Api.Contracts.Tests; use Given-When-Then and meaningful XML.
- [x] T012 [US3] Review/fix/verify and squash the OpenAPI configuration PR; record evidence in verification.md.

## Existing-code retrofit — separate reviewed PRs alongside MFA
- [ ] T013 [US2] Rename existing tests and all discovery/mutation/traceability consumers under tests, scripts and eng, preserving scenario semantics; retire associated legacy findings.
- [ ] T014 [US2] Document/refactor existing handwritten production code under src in cohesive feature increments; remove unexplained literals and retire findings.
- [ ] T015 [US2] Document/refactor shared test fixtures under tests, preserve independent expected values and process/security assertions; retire findings.
- [ ] T016 [US2] Extend or verify equivalent naming/documentation/literal controls for Python/TypeScript under scripts and frontend; retain language-appropriate idioms.

## Dependencies and continuation
T001–T002 -> T003–T008 -> T009 -> T010–T012. Independent review may run in parallel; main owns integration. T013–T016 proceed in separately reviewed PRs alongside M02-A increment 2, whose design prerequisite remains specs/008-administrator-foundation/T016. No initial baseline is a claim of retrofit completion.

## Approved focused retrofit batch — 13 October 2026

The user selected these three engineering cleanups while feature 008 T020/T021 remain blocked. Deliver sequential reviewed PRs with normal squash merges. These are bounded children of T013–T015; the parent tasks stay open.

- [x] T017 [US2] Rename and document the configuration scenarios in Core/Host tests; share MigratorConfigurationTests process execution without changing deadlines, environment cases or assertions. Compare native pre-enumerated inventories through an explicit rename map, execute Core/Host tests and full required CI, retire only resolved allowances, independently review and squash. Contributes to T013/T015.
- [ ] T018 [US2] Document Core/Features/Identity/Permissions/AdministratorPermissions and name its bundle/assignment identifiers; preserve every ordinal bundle and grant, verify existing independent permission tests and full required CI, retire resolved findings, independently review and squash. Contributes to T014.
- [ ] T019 [US2] Document/refactor AppHost.Tests/Features/LifecycleEvidence.cs into cohesive helpers in the existing hierarchy; preserve process-generation identity, terminal-state rules, deadlines, cleanup and sanitized failures. Verify lifecycle controls and both-provider CI, retire resolved findings, independently review and squash. Contributes to T015.

Controls: ENG-02/03/04/05/07/09 and NFR-08/11/14. No runtime capability, incident closure, new project or amendment is introduced. ADM-OPS-01 and ADM-EVID-01 remain unresolved and acceptance/release-blocking.
