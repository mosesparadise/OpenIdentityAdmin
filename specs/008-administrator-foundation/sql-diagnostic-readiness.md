# A-006 SQL diagnostic readiness disposition

13 October 2026. Scope: approved [A-006](../../docs/amendments/A-006-sql-diagnostic-readiness.md), diagnostic preparation only. PR48 remains draft/unmerged at `f78f1962dc6c0c4eb403804b94e2905714b813e8`. T020/T021 remain unchecked. All six findings and human/public-release blockers remain open.

## Outcome and limits

The exact pinned SQL image was absent on the managed local Docker daemon (28.4.0). The single permitted acquisition failed after 10.071 seconds with native exit 1; the image remained absent. The only retained recognized error category is `forbidden-response-text`. This does not establish an HTTP status, the rejecting component, or the cause of any historical SQL failure. No second acquisition, network-policy change, registry-identity change, TLS bypass, image substitution or daemon reconfiguration occurred.

The [safe image-readiness receipt](evidence/a006/image-readiness.json) records the digest and bounded observations. Raw error output is not published. SQL ordinary comparison and SQL witness mutant are both **not_run** because image readiness was not established. No database test, mutation acceptance artifact or full CI run was produced under A-006. No new disposable experiment checkout was needed once readiness failed.

## Diagnostic implementation

Existing fixture paths now emit 23 closed setup labels around host creation/start/readiness, lease provisioning, account enrollment and witness/request/code preparation. The existing authentication readiness wait remains unchanged; no database wait, retry or deadline was introduced. Original operations, catch behavior, assertions and disposal order are preserved. Named constants replace existing touched-method literals without changing their values.

The receipt projector accepts only finite labels, strict code-owned harness messages and allowlisted type tokens. Output remains bounded to 32 checkpoints. It reserves the first observed privilege bypass and latest privilege observation independently of the setup/witness tail, preserving original order. Missing best-effort checkpoints never prove the location of a failure, and token categories do not authenticate exception provenance.

The A-006 runner is SQL-only, inspects the exact image on the managed socket before claiming an attempt, and never pulls implicitly. The conditional mutant requires a same-source A-006 ordinary receipt with exactly two executed/passed cases, every other counter zero, no errors, restored source and no observed bypass. Duplicate JSON keys, prior A-005 receipts, missing image and incomplete or foreign-source evidence fail closed. Separate A-006 artifact paths avoid reusing A-005 attempts. The unchanged strict mutation oracle still owns acceptance; diagnostic projection never substitutes for it.

## Verification actually performed

All checks below are local and scoped; they do not replace the pending full delivery matrix.

| Check | Result |
| --- | --- |
| Administrator project build, pinned .NET SDK 10.0.401, serial build | Passed; zero warnings/errors |
| Native xUnit v3/MTP `Diagnostic=Setup\|Diagnostic=Privilege` controls | 24 executed/passed, zero failed/skipped; 16 prior controls plus 8 new setup controls |
| Native full discovery and portable metadata comparison | All 908 existing identities preserved, exactly 8 reviewed setup rows added; total 916 |
| Python mutation/diagnostic tooling controls | 56 passed |
| Administrator selection controls | 17 passed |
| Repository code-quality gate | Passed; four obsolete legacy allowances retired, none added |
| Staged source diff secret scan and whitespace check | Passed; no secret findings |

Commands: `dotnet build tests/aspire/IdentityPlatform.Administrator.Tests --no-restore --nologo -m:1 -nr:false`; `dotnet test --project tests/aspire/IdentityPlatform.Administrator.Tests --no-build --no-restore --no-progress --pre-enumerate-theories on --filter 'Diagnostic=Setup|Diagnostic=Privilege' --report-trx --report-trx-filename controls.trx`; native discovery via the test executable's `--pre-enumerate-theories on --list-tests json`; `python3 scripts/test_mutations.py`; `python3 scripts/test_administrator_selection.py`; `dotnet run --project eng/IdentityPlatform.CodeQuality --no-build -- --check .`. The local TRX results directory was outside the repository. Discovery is inventory evidence, not execution of all 916 tests.

New controls cover finite labels, undefined enum values, sink failure/cancellation with primary exception preservation, secret canaries, image readiness, strict ordinary receipts and one-shot claims. Review found and corrected mixed-dictionary checkpoint access and privilege evidence eviction by a long setup trace; permanent seeded controls cover both. Ordinary administrator inventory counts become 445/provider and Governance.Failures becomes 61/provider solely from four pure setup rows per provider. All original mutation targets, 55 coverage inputs and nine module floors remain unchanged; no coverage acceptance is claimed.

## Independent review and disposition

Independent architecture/security and test reviewers inspected the source and scoped evidence. Fixture lifecycle/deadline semantics remain unchanged. The existing witness-method size exception was explicitly reviewed with its changed fingerprint, original owner/task and unchanged 15 October expiry; no legacy baseline expansion occurred. AI source review supplies no human acceptance, incident closure or certification.

A-006 concludes with this publication on the separate diagnostic branch, without a PR. No original finding has a demonstrated causal correction. The next useful step is an environment-owner investigation of why the exact pinned image cannot be acquired through the existing permitted path. Once that prerequisite is demonstrably restored, a separately bounded decision can authorize the unrun SQL comparison and conditional mutant. Repeating the pull or tests under concluded A-006 is not authorized; production corrections, full CI and PR48 merge remain blocked.
