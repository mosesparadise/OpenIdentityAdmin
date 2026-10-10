# Tasks: Reproducible Platform Foundation

## Phase 1: Setup

- [x] T001 Record approval and install Spec Kit/AI instructions in docs/approval.md, .specify/, .agents/skills/ and .codex/agents/ (FND-006,007).
- [x] T002 Ratify the eighteen approved principles in .specify/memory/constitution.md (FND-006).
- [x] T003 Create global.json, Directory.Build.props, Directory.Packages.props, .gitignore and IdentityPlatform.slnx (FND-006).

## Phase 2: Foundational configuration

- [x] T004 Write allow/deny/boundary tests in tests/IdentityPlatform.Core.Tests/Features/Configuration/ (FND-005,008).
- [x] T005 Implement provider and HTTPS issuer validation in src/IdentityPlatform.Core/Features/Configuration/ (FND-005,008).
- [x] T006 Create project references in src/ and tests/ according to plan.md (FND-006).

## Phase 3: User Story 1 — Isolated environment

- [x] T007 [US1] Write profile liveness/readiness, DB/schema failure, cancellation and isolation tests in tests/IdentityPlatform.AppHost.Tests/ (FND-001,004).
- [x] T008 [US1] Implement health/telemetry defaults and health-only route inventory, telemetry redaction/no blanket retries and host composition in src/IdentityPlatform.ServiceDefaults/ and host Program.cs files (FND-004).
- [x] T009 [US1] Implement provider resources, separate migration/runtime credentials, migrator dependencies and health in src/IdentityPlatform.AppHost/ (FND-001).
- [x] T010 [US1] Execute both profiles and failure/teardown checks; record verification.md (FND-001,004).

## Phase 4: User Story 2 — Storage

- [x] T011 [US2] Write native-manager, initialization/idempotency, separate-principal DDL denial, schema compatibility and case-sensitive identifier tests in tests/IdentityPlatform.Persistence.Tests/Features/ (FND-002,003).
- [x] T012 [US2] Implement native contexts and provider registration in src/IdentityPlatform.Infrastructure/ (FND-002,003).
- [x] T013 [US2] Generate both context migrations in src/IdentityPlatform.Migrations.PostgreSql/ and src/IdentityPlatform.Migrations.SqlServer/ (FND-002).
- [x] T014 [US2] Implement bounded migrations/safe errors in src/IdentityPlatform.Migrator/Features/Database/Upgrade/ (FND-002).
- [x] T015 [US2] Run both real-provider suites and record verification.md (FND-002,003).

## Phase 5: User Story 3 — Engineering evidence

- [x] T016 [US3] Add dependency/controller/runtime-migration checks in tests/IdentityPlatform.Architecture.Tests/ (FND-006,008).
- [x] T017 [US3] Validate AI files and evaluate bounded fixtures; record docs/engineering/ai-evaluation.md (FND-007).
- [x] T018 [US3] Restore/lock dependencies, enforce per-module coverage with reviewed exclusions/sanitized retained reports, and add .github/workflows/verify.yml and verification scripts (FND-006).
- [x] T019 [US3] Obtain architecture/test/security review and resolve findings in verification.md (FND-006,007,008).

## Phase 6: Convergence

- [x] T020 Reconcile acceptance evidence and update README.md, quickstart.md, tasks.md and verification.md without claiming issuer/R1 completion.

## Dependencies and execution

Setup precedes configuration/projects. Tests precede implementation where possible; missing-type compilation failures are recorded separately from behavioral failures. T010 must execute after T014: T011–014 storage enables full AppHost readiness. Static reviews may run in parallel; main owns edits. Complete only tasks with evidence. A-001 permits implementation milestones to proceed; the unresolved Basic OP dependency remains a public-release certification gate.

## Phase 7: Convergence

Assessment: 8 foundation requirements, 5 success criteria and 12 story acceptance scenarios; 8 named plan decisions; 18 constitution principles screened for this infrastructure scope. Four partial findings (three HIGH, one MEDIUM); no out-of-scope feature implementation found. Product-only protocol/browser/release criteria remain in their approved later slices.

- [x] T021 Execute and resolve the complete real SQL Server persistence/Aspire matrix in CI or an authorized environment; preserve failure evidence until it passes per FND-001/002/003, SC-001/002/003, US1/AC1, US2/AC1-4 and Constitution 6 (partial, HIGH; extends T010/T015).
- [x] T022 Complete cancellation and strict process/container cleanup evidence in tests/IdentityPlatform.AppHost.Tests/Features/ and rerun affected provider profiles per FND-001/004, US1/AC4 and plan: bounded isolated composition (partial, HIGH; extends T007/T010).
- [x] T023 Execute .github/workflows/verify.yml, resolve actual failures and meet eng/coverage-policy.json for every handwritten module, including child processes; validate dependency/secret/license checks per FND-006, US3/AC1 and Constitution 10/11/13 (partial, HIGH; extends T018/T019).
- [x] T024 Reconcile final results and independent review dispositions in verification.md, docs/engineering/ai-evaluation.md, README.md and quickstart.md; preserve remaining requirements explicitly per FND-006/007, SC-004/005 and Constitution 1/12/18 (partial, MEDIUM; extends T019/T020).

Completion record: T001–T024 are complete for the foundation scope. Clean CI run 38016602794 verified implementation commit 55a6f766a821a587a3b802d975470e22e790aba1 on both real providers, with all required modules meeting coverage floors. Verification and review records preserve earlier failures and their dispositions. This does not complete issuer acceptance or the full product.
