# A-005 bounded governance diagnostics

Status: **Bounded A-005 work concluded; SQL diagnosis remains incomplete. No governance delivery or acceptance.** The user approved [A-005](../../docs/amendments/A-005-governance-diagnostics.md) on 13 October 2026 (Europe/London). PR48 remains draft/unmerged, A-004 expired, and GOV-EVID-01/GOV-PRIV-01 plus all four historical findings and human/public-release blockers remain open.

## Source scope and diagnostic controls

The separate `046-governance-diagnostic-proposal` branch adds safe failure observations to the existing mutation verifier, a focused diagnostic runner, fixed witness checkpoints and independent ALTER/probe classifications. No production source, native grant, schema, required mutation target, deadline, workflow trigger or test oracle changes. No new PR is opened and no full CI matrix is started.

`validate_report` remains authoritative and unchanged. Receipts project at most two MiB of XML into bounded counts, allowlisted assertion/exception token observations, marker/stack booleans and finite checkpoints. Missing, malformed, oversized and unknown reports stay unknown. No raw message, stack, SQL, identity, URL, credential or child output is retained in the published receipt. These categories observe text tokens; they are not authenticated exception types or causal proof. No diagnostic tool writes an acceptance `evidence.json`.

ALTER keeps its five-second ceiling and accepted SQL codes 229/4902. Its independent metadata probe has a fresh five-second linked budget covering connection open and both read-only queries. An expired test lifetime prevents the probe. Primary failure and probe failure/cancellation remain separate, and successful DDL or an incomplete probe cannot satisfy denial. Fixed witness checkpoints add no waits, branch predicates or exception rewriting; existing teardown can still mask an earlier failure, which remains a stated limitation rather than an unapproved lifetime refactor.

The eight pure diagnostic cases per provider are explicitly tagged `Diagnostic=Privilege`; they access no database. Native discovery preserves all 892 existing portable identities/types/traits and adds 16, yielding 908. The ordinary profile count is 441/provider and Governance.Failures 57/provider on this diagnostic branch. Existing test partitions, 55 coverage inputs and all security targets remain required for any later delivery; none are accepted by this experiment.

One exact CQ004 exception permits checkpoint instrumentation in the decisive witness method without changing its causal structure or final mutation assertion stack. It belongs to feature 009 T015/A-005 D003, owner Implementation lead, and expires 15 October 2026 or must be reconsidered at diagnostic disposition before correction/delivery. No legacy allowance was added or refreshed.

## Pre-experiment verification and review

- `dotnet build tests/aspire/IdentityPlatform.Administrator.Tests --no-restore --nologo -m:1 -nr:false`: passed, zero warnings/errors.
- `python3 scripts/test_mutations.py`: 48 passed, including 10 new receipt/orchestration controls. The direct runner now executes all module test classes; strict failure/skip handling remains.
- `python3 scripts/test_administrator_selection.py`: 17 passed.
- `dotnet test --project tests/aspire/IdentityPlatform.Administrator.Tests --no-build --no-restore --no-progress --pre-enumerate-theories on --filter 'Diagnostic=Privilege' --report-trx --report-trx-filename controls.trx --results-directory /workspace/work/a005-seeded`: 16 passed, zero skipped/errors/incomplete counters, including combined primary/probe failure/cancellation and denial/probe-cancellation controls.
- `dotnet run --project eng/IdentityPlatform.CodeQuality --no-build -- --check /workspace/work/OpenIdentityAdmin`:passed with the single explicit diagnostic exception above;1830 unchanged legacy findings retained.
- Full native discovery and portable metadata comparison: all 892 originals preserved; only 16 reviewed pure diagnostic additions.

Independent architecture/security and test reviewers reviewed actual source read-only. Findings fixed before experiment: direct Python execution omitted the new test class; orchestration final receipt writing could mask primary failure/cancellation. Seeded regressions cover both. Both reviewers support only the bounded diagnostic execution, not product correctness, causal closure, acceptance or merge.

## Authorized execution and stop rules

Run the following once each in the same clean disposable checkout of the published diagnostic revision, with the pinned SDK/environment, after review:

```sh
python3 scripts/diagnostic_governance.py ordinary PostgreSql --disposable-checkout
python3 scripts/diagnostic_governance.py ordinary SqlServer --disposable-checkout
python3 scripts/diagnostic_governance.py mutant SqlServer --disposable-checkout
```

Each ordinary invocation selects exactly the decisive witness and runtime-principal privilege methods. The mutant invocation selects only the reviewed omitted-witness-lock method and exact substitution, retaining strict identity/metadata/marker/stack checks. Command bounds remain 180 seconds for build/execution and 60 seconds for discovery. Exclusive attempt files prevent identical local retries. Both same-source ordinary receipts must exist before the SQL mutant. A classified unmodified ALTER success, visible probe column or actual witness bypass marker halts subsequent experiments and requires a remediation decision. Unknown remains unknown; an ordinary failure is never accepted as a passing baseline.

The existing source-restoration finally and explicit SHA256 comparison apply even when failure or cancellation occurs. Source restoration does not claim restored build/test acceptance. Disposable builds are not publishable product outputs. Raw temporary reports are removed after their safe observations are retained; all observations, incomplete experiments and limitations will be recorded below. After this disposition, stop for a separate correction/delivery decision.

## Results

Diagnostic source: `efa2aea3e30fef658f2c74e78d80425c7aa08b6e`, tree `bf224adf3cbd73061cec5fa9bdeee381a03460a9`. Published before execution on the separate branch with no PR. All three commands ran once in clean detached checkout `/workspace/work/a005-experiment` after locked restore.

| Experiment | Native outcome | Safe observations | Interpretation |
| --- | --- | --- | --- |
| PostgreSQL ordinary | 2 passed, 0 failed, 0 skipped; execution 53.268s; verifier result 53.274s | ObserverFirst, completed native commit/writer/security/authority/usable/oracle checks; ALTER Denied, probe Absent | Both selected checks passed in this execution only. |
| SQL Server ordinary | 0 passed, 2 failed, 0 skipped; execution 64.063s | Only Setup checkpoint; 2 error nodes; unknown token category; no ALTER/probe observation; marker false | No post-Setup observations were retained. The actual exception location and cause are not identified. This does not establish privilege denial or reproduce its historical failure boundary. |
| SQL Server omitted-witness mutant | 0 passed, 1 failed, 0 skipped; execution 33.280s | Only Setup checkpoint; 1 error node; unknown category; strict marker false | Not a demonstrated kill. Failed ordinary SQL comparison and missing marker prevent any mutation acceptance. |

Checkpoint emission is best-effort, and Prepared follows scenario creation plus witness/request/code preparation. Only Setup being present does not prove the exception occurred in host startup or any particular preparation step; an output failure could also remove later observations. The actual SQL failure location remains unknown.

Every listed incomplete/error run counter other than the actual failed cases was zero, and there were no run-summary ErrorInfo nodes. Stack-match true merely means the reviewed witness method occurs somewhere in the stack; it does not locate the root cause. The ordinary failure receipt has marker/stack false because no mutation item was passed to ordinary strict validation; the diagnostic summary separately projects the reviewed witness item. That distinction is expected, not a new failure.

The SQL mutant substitution hash was `cd2c9112455ba5fcc0935bf778810603633ff2d7697839177110c1fc469ad22d`; original/restored `CeremonyLocks.cs` hash was `537cf7de77ce6444661f1dde017ffafe95c9be552a1356001efb2bea8804df1b`. Independent byte-for-byte comparison against HEAD succeeded and the disposable checkout is clean. All three processes ended. No acceptance `evidence.json` exists. No restored-build/test acceptance is claimed; the disposable checkout's binaries are not publishable.

Read-only prerequisite follow-up: the local Docker daemon was 28.4.0. `docker image inspect` for the exact repository-pinned SQL image digest `4402d880dd4c34bfa7d8705e56a86cd6c88da80a1f6bbbe741f999e76264a090` returned “No such image”. No image pull or retry was performed. Missing image availability is a concrete current prerequisite gap; it does not by itself establish the cause of these failures or explain the original GitHub CI incidents. The fixture's existing safe catch paths can collapse setup errors into generic failures; source inspection is not runtime causal evidence.

## Retained diagnostic receipts

These files contain only the reviewed bounded schema. They are diagnostic observations, not acceptance inputs. Raw temporary TRX reports were removed after projection. The narrow classifier records unknown instead of guessing causes.

| Receipt | SHA256 |
| --- | --- |
| [Execution phase receipts](evidence/a005/execution-phases.json) | `c6cd1e359497a50f26cd733db8616c482987e11e6bf6f230da2c9c5dcc12b45d` |
| [PostgreSQL ordinary](evidence/a005/ordinary-PostgreSql-diagnostic.json) | `7e9b80faa8859dda09c27651e775da4aa3cc91967ac1d92e15f9fa77e0db6546` |
| [SQL ordinary](evidence/a005/ordinary-SqlServer-diagnostic.json) | `77a3dc6189d59a75f096f14a2973ed97227b51ed88dd7978025f317718104e93` |
| [SQL ordinary failure](evidence/a005/ordinary-SqlServer-failure-diagnostic.json) | `1eaa1164270bbe0b092d9fb0b913bfcf591599712bc45ec28c75284b5e64df2f` |
| [SQL mutant](evidence/a005/mutant-SqlServer-diagnostic.json) | `3e3101af35454a9c7fd563a5537cd763823ebc3d022c39da0947183696b345e9` |
| [SQL mutant failure](evidence/a005/mutant-SqlServer-failure-diagnostic.json) | `699bedfba86c83058615763236b0f7aeb5bc2550b5c3e69942b64cee4ff29bee` |

## Disposition and recommendation

A-005's authorized bounded work is complete, but neither original SQL finding has a demonstrated cause or correction. PostgreSQL's local passing result does not establish SQL parity. The SQL failures with no retained post-Setup observations and missing image availability remain an investigation prerequisite, not evidence that the product is safe or unsafe. No findings or human/public-release blockers are closed. PR48 remains draft/unmerged and T020/T021 unchecked.

Recommend [proposed A-006](../../docs/amendments/A-006-sql-diagnostic-readiness.md): establish the exact pinned image prerequisite through the existing permitted network path, preserve safe fixture-setup phases, then run one SQL ordinary comparison and only after it passes one SQL witness diagnostic. No production correction or merge is preauthorized. A-005 expires with this disposition; no further runtime execution is taken under it.

Additional source hygiene: the published diagnostic commit passed a Git-range gitleaks scan. A separate broad directory scan flagged eight generic-key patterns only in ignored installed Playwright JavaScript under bin; those dependency files are not part of the published commit. No dependency-wide secret-clearance claim is made.

Final independent architecture/security review verified all six receipt hashes, clean disposable checkout and restored source hash. It required the wording above to distinguish missing post-Setup observations from a proven failure location; that correction is incorporated. No runtime causal closure, product acceptance or merge was endorsed.

Final independent test review also reconciled all six receipt hashes and the native counts/timings. The source checkpoint bound to the experiment is clean and its target source matches the published revision. The reviewer supports the documented limitations and proposed A-006 only; this is AI evidence review, not acceptance or authorization.

## Subsequent A-006 checkpoint

The user subsequently approved A-006. Its [bounded readiness disposition](sql-diagnostic-readiness.md) records the single failed pinned-image acquisition and reviewed diagnostic instrumentation. No SQL experiment ran under A-006; its authorization has concluded. The A-005 results above remain unchanged and do not establish either historical incident cause.
