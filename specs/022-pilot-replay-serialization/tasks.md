# Tasks: Pilot replay serialization

**Latest validation:** CI552 at `413a3d4` passes123 jobs but fails SQL Distribution
and final acceptance; coverage aggregation is skipped. All earlier regression
producer jobs now succeed. A separate failed diagnostic observes native token-read
blocking before the replay's gate UPDATE. The corrected test requires exact native
lock-resource ownership under the winner's exclusive gate transaction and known
issuer process identities. Its focused hosted execution and a clean final-head
full run remain required. See `verification.md`; no task is accepted from a failed
run. The older local startup limitation below does not describe hosted SQL readiness.

**Current batch:** the user selected T008, SQL Server validation and T009. Branch132
adds five pilot mutation witnesses per provider, mandatory hosted mutation jobs and
updated native inventory at `71ab8e319ab183b8af21b0405f4a2ac565a93894`. T008/T009
remain open until actual both-provider/full-CI evidence and integrated review pass.
The prior implementation is preserved; local SQL startup is not repeated without
a verified changed condition. See the current section in `verification.md`.

T001 is the planning checkpoint. T002 retains a PostgreSQL old-source failure.
T003–T004 are implemented on branch130; T005–T007 are implemented on
`131-pilot-replay-coordination` at `6860a6b7b155d931b8d7e7757992ddceb1b428a4`.
PostgreSQL now demonstrates the corrected initial-replay invariant, including both
real process directions. These implementation tasks remain unchecked until their
required SQL Server verification succeeds. SQL still exits before readiness; it is
not a replay/schema test result. See `verification.md` for reports and limits.

Next is T008/T009 integrated acceptance plus the outstanding SQL cells. Preserve
existing source/evidence; do not repeat completed implementation or reopen expired
diagnostic approvals. Native inventory reconciliation is already included; new
mutation controls, coverage/full CI and final delivery acceptance remain open.

## Setup

- [x] T001 Review `spec.md`, `research.md`, `plan.md`, `data-model.md` and `contracts/exchange.md`; record architecture/security and test dispositions in `verification.md`. Preserve approved exactly-one-success behavior and all existing gates. PRS-001–006.

## US1 — Serialized concurrent exchanges

- [x] T002 [US1] Add the controlled initial-replay oracle under `tests/aspire/IdentityPlatform.Administrator.Tests/Features/ProtocolAdmission/`; retain complete baseline source/report hashes and demonstrate its old-source failure without a second replay. Preserve the existing later-cleanup/guard regressions in separate cases. C01/C08.
- [ ] T003 [US1] Add `PilotProtocolGate` model/configuration under `src/persistence/IdentityPlatform.Infrastructure/Features/Identity/Persistence/`, new migrations in both `src/migrations/IdentityPlatform.Migrations.*` projects and fresh/upgrade/least-privilege tests under `tests/persistence/IdentityPlatform.Persistence.Tests/Features/`. Preserve histories and native data; never activate or repair the pilot from HTTP. C09.
- [ ] T004 [US1] Implement feature-local database gate ownership and fresh native validation under `src/backend/IdentityPlatform.AuthServer/Features/Protocol/PilotReplay/`; wire both host entry paths without weakening `ProtocolStorageOwner.BeginChild()` or other profile selection. C01/C07/C10.

## US2 — Durable consumption and bounded publication

- [ ] T005 [US2] Complete coordinator separately committed native consumption/replay, generation in the same transaction/context that holds the gate, and captured publication through commit and actual cleanup in `Features/Protocol/PilotReplay/`, reusing `ProtocolTransport` primitives. C03/C04/C05/C06.
- [ ] T006 [US2] Add provider-backed faults and real gate-loss/late-continuation evidence in `tests/aspire/IdentityPlatform.Administrator.Tests/Features/ProtocolAdmission/`; cover original deadlines, missing state, rollback/commit ambiguity, cancellation and cleanup. Retain native bindings and unrelated success. C03–C08/C11/C12.

## Cross-instance acceptance and delivery

- [ ] T007 Add ordered two-process evidence under `tests/aspire/IdentityPlatform.Issuer.Lifecycle.Tests/Features/Distribution/`; prove distinct process/transport identities, real database contention, both directions and no late issuance before any additional replay. C02.
- [ ] T008 Reconcile native discovery/selection, meaningful gate/publication mutation controls, original coverage inputs and existing full regression inventory in `eng/`, `scripts/` and `verification.md`; run all required exact-head CI with unchanged floors. C01–C12.
- [ ] T009 Complete independent architecture/security, test/evidence and provider/Aspire reviews; document coordinated upgrade and current gaps in `quickstart.md`, `verification.md`, `docs/roadmap.md` and `docs/operations/new-task-handoff.md`; normally squash only after exact-head checks pass. PRS-001–006.

Dependencies: T001→T002→T003→T004→T005→T006→T007→T008→T009.
Architecture/test reviews may run independently; local provider runtimes remain
serial. Baseline regression preparation can be reviewed independently from migration
design. No partial runtime delivery closes the defect; US1 and US2 are one integrated
security correction even though their test boundaries are independently observable.
