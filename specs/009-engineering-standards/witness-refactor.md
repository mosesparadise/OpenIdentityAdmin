# Witness test exception retirement

14 October 2026 (Europe/London). Status: preparation and review in progress.

The user instructed “Proceed with your recommendation” after the recommendation to resolve the witness-test size exception before 15 October. This is a bounded child of feature 009 T015, tracked as T020, using the approved engineering standards and normal reviewed squash delivery. Main was verified at `5f4fce7fe0c7761e6eab7308f2cd38908937af51`; prior worktrees and the diagnostic handoff branch are preserved. A-004 remains expired and A-005/A-006 concluded.

## Scope and invariants

The sole expiring CQ004 allowance covers `WitnessOrderingTests.Given_FinalWitnessObservation_When_MandatoryLockoutRacesNativeCommit_Then_WitnessLockSerializesTheCommit`. Extract its native witness/proof preparation and its final committed-effect checks into cohesive fixture operations. Keep the gate, owned service scope, cancellation lifetime, task handles, race ordering, failure-propagating finally and final bypass assertion visible in the original test. Remove the allowance instead of renewing it or expanding legacy debt.

| Requirement | Preserved boundary | Verification |
| --- | --- | --- |
| ENG-02/04/05/07 | Same named theory and independent expectations; meaningful documented helpers below 40 logical lines; exact exception removed | Native discovery comparison, syntax-aware quality gate and independent architecture/test review |
| GOV-02/06/07, ADMIN-008/009 | Fixed 512 ordinary users and exactly actor/witness candidates; four prior native failures; fifth failure starts only after owned native commit admission; wait before release | Both-provider witness ordering cases; unchanged fixture, wait and deadline code |
| GOV-08/12, ADMIN-006/009 | Actual accepted commit, completed lockout writer, exact security/authority/audit/replay effects and empty final usable set | Existing final assertions and both-provider omitted-witness mutation baseline/kill/restored controls |
| ENG-09, NFR-08/11/14 | All selected required checks, mutation obligations and coverage floors remain enforced | Full exact-head CI and independent evidence review before squash |

No production code, test identity, mutation manifest, deadline, security assertion or CI policy change is planned. Existing production and historical evidence are preserved. No new tests that merely mirror extraction are needed; the native behavioral and mutation controls are the regression proof.

## Evidence

Local locked solution build and refactored administrator build pass with zero warnings/errors. Formatting, syntax-aware quality/self-tests and 76 Python controls (3 quality, 17 administrator selection, 56 mutation-verifier) pass. Native baseline/final discovery matches all 1,021 portable identities and metadata. The sole exact CQ004 exception is removed; all 1,826 legacy entries are unchanged. Independent architecture/test and security source reviews pass, including resource lifetime, cancellation/drain and exact assertion ordering.

The first local PostgreSQL witness-class baseline hit the unchanged 180-second execution limit before producing a report; supervised process cleanup returned and no mutant ran. This failed attempt supplies no behavioral acceptance and is retained separately. Current runtime/mutation verification and full exact-head CI, artifact and coverage review remain required. Historical PR #48 results are not substituted for this refactor's verification.

All six historical findings and all 48 unexecuted human assessment rows remain open. Whole-increment acceptance, management activation and public-release/external assurance gates are unchanged. This refactor makes no incident-cause or production-readiness claim.
