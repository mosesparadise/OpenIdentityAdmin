# Host composition verification

## Planning checkpoint

User confirmed all six production entry points, exact web composition/Setup conventions, preserved hierarchy and structure-only behavior; separate PR before feature 004 T026. PR #7 was squash-merged as `9c52c2d` after seven passing checks on head `6b6be34`.

Spec Kit specify/plan/tasks completed before source edits. No extensions.yml hooks configured. Analyze (read-only): eight HC requirements and three SC criteria covered by 13 tasks, zero unmapped tasks, ambiguities, duplicates or critical/constitution conflicts. Requirement mapping: HC-001/002→T004–T007; HC-003→T004–T006/T008/T010; HC-004→T005/T008; HC-005→T005/T008/T010; HC-006→T006/T007/T009/T010; HC-007→T011/T013; HC-008→T012/T013; SC-001→T004–T007/T012; SC-002→T011; SC-003→T012. Requirements checklist: 6 checked, 0 unchecked.

Architecture design review: no blocker. Preserve early bootstrap return before HTTP mapping, local bootstrap safe catch-all, narrow database error boundary, outer certificate ownership, one reusable pilot service-registration path, and deferred Aspire graph expressions. These constraints are included in plan/contracts. Test design review recommended reusing the existing bootstrap provider fixture for actual process success/repeat/conflict tests to avoid another distributed startup.

## Implementation evidence

Local verification: .NET solution build passed with zero warnings/errors. `python3 scripts/verify.py unit` passed all 224 required cases (Core 25, Host 185, Architecture 5, AppHost harness 9), zero skips. The new host cases cover valid-credential Production/Staging pilot denial and actual composition disposal across normal/configuration/build/protection/bootstrap exits. Existing graph tests now inspect both provider profiles, deferred secret recipients and successful-completion dependencies. `python3 scripts/check-static-analysis.py --self-test` passed build/format/import gates and proved CA5350/IDE0055/IDE1006 seeded violations rejected. Initial extraction formatting/import failures were corrected before that pass.

`check_assurance.py`, `check-ai-instructions.py` and `git diff --check` pass. Architecture reviewer requested moving Auth storage registration through Features/Setup; corrected without changing its order/error boundary. Security and Aspire source reviewers found no behavior regression; the plan was corrected to describe the pre-existing LocalRuntimeCredentials type accurately rather than claim a type change. Full source checkpoint CI provider/browser/coverage results are recorded below. All 345 ASVS rows remain not_assessed; T026–T030 and certification gates remain pending.


## Source review and process checkpoint

PR [#8](https://github.com/mosesparadise/OpenIdentityAdmin/pull/8), `refactor(host): extract composition and feature setup`, was opened separately from T026 on source commit `837ab475a801184c5ef6e2975b9ef5ff3bdb5065`. Its [CI run](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38088371465) is the authoritative source verification checkpoint; all seven checks passed: static analysis, assurance registers, supply chain, unit matrix, PostgreSQL, SQL Server and per-module coverage.

The final source passes local `check-static-analysis.py --self-test`. The exact-source CI unit matrix also passes 224/224, zero skips. Local `test_assurance.py` (27), `test_verify_evidence.py` (8), `test_coverage_gate.py` (3) and `test_supply_chain.py` (8) all pass; these are harness checks, not additional product test cases.

The PostgreSQL bootstrap executable scenario passes on the actual production output: fresh success, identical repetition and conflicting redirect configuration return 0/0/2; manager-owned records and complete scalar/audit snapshots retain the expected state; the occupied configured HTTP port and fully drained output prove the command did not start a listener. The first harness version launched the referenced AuthServer DLL from the issuer test output and failed before startup because Aspire's SQL client dependency set differed from AuthServer's deps file. The corrected launcher uses the actual project with `dotnet run --no-build --no-launch-profile`, the matching assembly configuration, and inherited coverage variables. No production fallback or dependency version changed. The same executable scenario also passed on SQL Server in the full matrix (13.000s; PostgreSQL 12.509s).

Independent source dispositions:

| Reviewer | Finding and disposition |
| --- | --- |
| Architecture | Move Auth storage registration into Features/Setup; corrected and re-reviewed. Correct nonexistent PilotConfigurationTests reference to PilotPolicyTests; corrected. Final bounded source verdict pass. |
| Test strategy | Captured child-output string assertions and unexpected-exception assertions could print sensitive diagnostics; replaced with Boolean checks and fixed messages. Production-output launcher and bounded process-tree cleanup reviewed. Final source findings closed. |
| Security | Credential lifetime, startup guards, no-HTTP bootstrap, middleware and secret boundaries preserved. Existing LocalRuntimeCredentials record has no new serialization/logging path; plan corrected rather than claim an unrelated type change. Final bounded source verdict pass. |
| Aspire | Resource names/images, deferred expressions, secret recipients, runtime roles and migration/bootstrap completion dependencies preserved; one-shot Migrator remains bounded. Bounded source verdict pass. |

Reviewers performed independent read-only inspections; build/runtime/coverage evidence is reported separately. No AI verdict is a certification or independent security assessment.

## Completed source checkpoint

Run `38088371465` passed 602 .NET cases with zero skips: unit 224 (Core 25, Host 185, Architecture 5, AppHost harness 9), PostgreSQL 189 and SQL Server 189. Each provider includes Persistence 3, AppHost 4 and Issuer 182; each issuer suite contains all six required Chromium cases. The SQL Server TRX and PostgreSQL TRX confirm all 12 browser cases and both actual bootstrap process cases passed. No deadline increases, skipped cases or blanket retries were introduced.

| Module | Line coverage | Branch coverage |
| --- | ---: | ---: |
| Core | 100.0% | 100.0% |
| Infrastructure | 99.0% | 100.0% |
| ServiceDefaults | 100.0% | 100.0% |
| Migrator | 98.2% | 90.0% |
| AuthServer | 94.7% | 98.4% |
| Admin.Api | 100.0% | 100.0% |
| Admin.Bff | 100.0% | 100.0% |
| Worker | 100.0% | 100.0% |
| AppHost | 98.8% | 100.0% |

All nine modules satisfy the unchanged 90% line / 80% branch floors, including handwritten composition. These percentages describe commit `837ab47`, not a later commit. PostgreSQL issuer duration was 7m37.466s; SQL Server was 10m31.901s against the unchanged 660-second runner deadline. This is harness timing, not a product performance benchmark. T026 must budget fault coverage against the roughly 28 seconds of remaining SQL Server headroom while retaining every required case.

## Convergence regression and delivery

Final requirement review identified the specified disabled-pilot/explicit-initialization edge as lacking a direct executable assertion (HC-003, US2/AC1). Spec Kit converge appended T014 before implementation. `IssuerStartupTests` now supplies valid credentials with `Pilot:Enabled=false` and `--initialize-pilot`, proving fixed pilot-configuration diagnostics, exit 2, no listener and redacted fully drained output. Independent test review confirmed this reaches the intended disabled-pilot guard rather than the non-Development or later bootstrap-failure path.

After this test-only addition, the local solution build passed with zero warnings/errors, the full unit matrix passed 225/225 with zero skips (Host now 186), and static analysis/format/import verification plus all three seeded enforcement checks passed again. Assurance-register, AI-instruction integrity and diff-whitespace checks also passed. The expected final CI count is 603: 225 + 189 + 189. Production source is unchanged from the completed checkpoint above; the final-head CI result is tracked on PR #8 and must be checked before merging. T010/T011/T013 are satisfied by recorded process, full-matrix and review evidence; T014 is satisfied by the new process regression and local execution.

All 14 feature tasks are complete. PR #8 is the separately reviewable composition change; it is not merged. Feature 004 T026–T030 and public-release assurance/certification gates remain pending. No control assessment or product-wide test-completeness claim is implied.
