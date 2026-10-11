# Issuer lifecycle verification — restart in progress

The user confirmed the dedicated lifecycle test project and three sequential PRs on 11 October 2026. PR #17 implements restart only; it is not accepted until both-provider evidence, required checks and source/evidence reviews pass. Distribution, rotation, T021 human assessment, M01-F and public-release gates remain open.

Spec Kit template resolution, setup-plan/setup-tasks and prerequisites ran. No extension hooks exist. The requirements checklist and independent design review are complete. The prior accepted checkpoint is PR #16 squash `559380e952059383741a7bc75c259b5bb39ed1e1`; its 13 checks/901 ordinary cases do not establish lifecycle behavior.

## Implementation and review in progress

The new native xUnit v3/MTP project reuses explicit fixture source links, with 13 intended restart cases per provider. Existing AppHost harness tests add process-generation and accumulated-cleanup controls. Each provider receives its own <=660-second lifecycle invocation under the existing child-process coverage collector and unchanged 20-minute CI job. Actual aggregate timing is still required.

Independent architecture/security/test source reviews identified and corrected: cancellation that could abandon stopped callbacks; replay snapshots taken too late; missing session-inventory and completed one-shot generation checks; unauthenticated rather than authenticated wrong-client controls; generic denials masking native binding failures; broad continuation/wrapper failure oracles; incomplete JWKS private-field guards; contradictory TRX counters; and missing lifecycle catalogue/manifest checks. Reviewer acceptance remains pending final source and executed evidence.

## Experiments

- Initial compilation exposed missing Aspire testing namespace and formatting/static-analysis violations; fixed without disabling rules.
- First PostgreSQL run failed: Aspire reports a deliberately stopped .NET project as `Finished`, while the harness waited only for `Exited`. The fixture was invalidated, remaining cases failed closed, and teardown attempted all resources. MTP also refused duplicate test/cleanup result identities. This is failed evidence, not a pass. Terminal handling now accepts explicit `Finished`/`Exited` while independently requiring OS process exit.
- Second PostgreSQL experiment: pending. SQL Server/full regression/coverage: not yet run for this implementation.
- Local Python checks: lifecycle evidence selection (4 tests), existing evidence parser (8), browser matrix (8) passed. These do not replace provider execution.

Final evidence will record actual discovered/executed identities, unchanged native-code timing margins, both provider/job durations, teardown, exact source/merge trees, coverage inputs and reviewer dispositions before acceptance.
