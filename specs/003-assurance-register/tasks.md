# Tasks: Assurance Register Baseline

## Phase 1 — Setup

- [x] T001 Define REG-001–006 scope, source research, schema and verification plan in specs/003-assurance-register/spec.md, plan.md, research.md, data-model.md and quickstart.md.
- [x] T002 Review spec quality and cross-artifact consistency with independent architecture/test reviewers; record findings in specs/003-assurance-register/verification.md before affected implementation.

## Phase 2 — Foundation

- [x] T003 Import the unchanged 345-control official source and pinned provenance in docs/assurance/asvs-5.0.0.csv and catalogue.json; preserve CC BY-SA attribution in docs/third-party/README.md (REG-001).

## Phase 3 — US1: complete, truthful control inventory

Independent test: exact official inventory, conservative baseline and valid routing for every row.

- [x] T004 [US1] Create docs/assurance/roles.json and threats.json with initial boundary threats, role responsibilities and explicit pending named ownership (REG-002/005).
- [x] T005 [US1] Create exactly one docs/assurance/controls.csv row for every official control: applicability pending, result not_assessed, triage/rationale/role/task/requirement/threat/component/reassessment fields present; no fabricated evidence (REG-001/002).

## Phase 4 — US2: fail-closed register integrity

Independent test: real baseline succeeds; each seeded integrity/unsupported-transition case fails.

- [x] T006 [US2] Add positive and negative cases to scripts/test_assurance.py before implementing the gate, covering REG-003/004 and the data-model constraints; record the initial expected failure.
- [x] T007 [US2] Implement strict offline catalogue/schema/identity/reference/date/status validation in scripts/check_assurance.py; reject expired or incomplete exceptions and unsupported passes/exclusions, validate every local evidence path is contained and exists (REG-003/004).
- [x] T008 [US2] Wire baseline validation and no-skip regressions into .github/workflows/verify.yml with existing pinned checkout and five-minute deadline (REG-006).

## Phase 5 — US3: actionable release gaps

Independent test: review that release tensions, ownership gaps and remaining assessment work are discoverable without a compliance claim.

- [x] T009 [US3] Write docs/assurance/README.md with immutable source, state maintenance, full initial counts, L3 design dispositions, ownership gaps, evidence handling and independent/external gates (REG-005).
- [x] T010 [US3] Update docs/roadmap.md, docs/README.md and README.md with bounded status/links; record actual PR #2 squash and self-approval restriction in specs/002-issuer-foundation/tasks.md and verification-quality.md (REG-005).

## Phase 6 — Verification and convergence

- [x] T011 Execute scripts/check_assurance.py and scripts/test_assurance.py, existing tooling regressions and source/format checks; obtain architecture/test/compliance implementation review, resolve findings and record commands/results/limits in specs/003-assurance-register/verification.md (REG-001–006).
- [x] T012 Run Spec Kit convergence, publish the reviewable feature PR and inspect all required CI results; record the exact head and pending broader assurance work in specs/003-assurance-register/verification.md (REG-006).

## Dependencies and parallel opportunities

T001–002 precede affected code. T003 precedes T004–005; T005 fixtures support T006, then T007 and T008. T009 can be drafted after schema agreement while gate testing proceeds. T010 follows actual #2 merge (already completed), T011 follows feature code/docs and T012 follows local verification. Independent architecture, test and compliance reviews can inspect the same stable files concurrently. Main agent owns edits.

## Implementation strategy

US1 delivers the useful complete inventory first. US2 guards it; US3 explains what remains and how owners maintain it. All three are required for this increment. No new product endpoint, deployment or certification action is included. M01-C remains ongoing after this bounded baseline; first-party login follows as a separate feature. Do not mark implementation work complete from checked requirements-quality checklists.
