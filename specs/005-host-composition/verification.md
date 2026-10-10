# Host composition verification

## Planning checkpoint

User confirmed all six production entry points, exact web composition/Setup conventions, preserved hierarchy and structure-only behavior; separate PR before feature 004 T026. PR #7 was squash-merged as `9c52c2d` after seven passing checks on head `6b6be34`.

Spec Kit specify/plan/tasks completed before source edits. No extensions.yml hooks configured. Analyze (read-only): eight HC requirements and three SC criteria covered by 13 tasks, zero unmapped tasks, ambiguities, duplicates or critical/constitution conflicts. Requirement mapping: HC-001/002→T004–T007; HC-003→T004–T006/T008/T010; HC-004→T005/T008; HC-005→T005/T008/T010; HC-006→T006/T007/T009/T010; HC-007→T011/T013; HC-008→T012/T013; SC-001→T004–T007/T012; SC-002→T011; SC-003→T012. Requirements checklist: 6 checked, 0 unchecked.

Architecture design review: no blocker. Preserve early bootstrap return before HTTP mapping, local bootstrap safe catch-all, narrow database error boundary, outer certificate ownership, one reusable pilot service-registration path, and deferred Aspire graph expressions. These constraints are included in plan/contracts. Test design review recommended reusing the existing bootstrap provider fixture for actual process success/repeat/conflict tests to avoid another distributed startup.

## Implementation evidence

Local verification: .NET solution build passed with zero warnings/errors. `python3 scripts/verify.py unit` passed all 224 required cases (Core 25, Host 185, Architecture 5, AppHost harness 9), zero skips. The new host cases cover valid-credential Production/Staging pilot denial and actual composition disposal across normal/configuration/build/protection/bootstrap exits. Existing graph tests now inspect both provider profiles, deferred secret recipients and successful-completion dependencies. `python3 scripts/check-static-analysis.py --self-test` passed build/format/import gates and proved CA5350/IDE0055/IDE1006 seeded violations rejected. Initial extraction formatting/import failures were corrected before that pass.

`check_assurance.py`, `check-ai-instructions.py` and `git diff --check` pass. Architecture reviewer requested moving Auth storage registration through Features/Setup; corrected without changing its order/error boundary. Security and Aspire source reviewers found no behavior regression; the plan was corrected to describe the pre-existing LocalRuntimeCredentials type accurately rather than claim a type change. Final CI provider/browser/coverage evidence remains pending. All 345 ASVS rows remain not_assessed; T026–T030 and certification gates remain pending.
