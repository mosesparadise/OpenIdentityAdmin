# A-005 bounded governance diagnostics

Status: **Approved diagnostic work in progress; no governance delivery or acceptance.** The user approved [A-005](../../docs/amendments/A-005-governance-diagnostics.md) on 13 October 2026 (Europe/London). PR48 remains draft/unmerged, A-004 expired, and GOV-EVID-01/GOV-PRIV-01 plus all four historical findings and human/public-release blockers remain open.

## Source scope and diagnostic controls

The separate `046-governance-diagnostic-proposal` branch adds safe failure observations to the existing mutation verifier, a focused diagnostic runner, fixed witness checkpoints and independent ALTER/probe classifications. No production source, native grant, schema, required mutation target, deadline, workflow trigger or test oracle changes. No new PR is opened and no full CI matrix is started.

`validate_report` remains authoritative and unchanged. Receipts project at most two MiB of XML into bounded counts, allowlisted assertion/exception token observations, marker/stack booleans and finite checkpoints. Missing, malformed, oversized and unknown reports stay unknown. No raw message, stack, SQL, identity, URL, credential or child output is retained in the published receipt. These categories observe text tokens; they are not authenticated exception types or causal proof. No diagnostic tool writes an acceptance `evidence.json`.

ALTER keeps its five-second ceiling and accepted SQL codes229/4902. Its independent metadata probe has a fresh five-second linked budget covering connection open and both read-only queries. An expired test lifetime prevents the probe. Primary failure and probe failure/cancellation remain separate, and successful DDL or an incomplete probe cannot satisfy denial. Fixed witness checkpoints add no waits, branch predicates or exception rewriting; existing teardown can still mask an earlier failure, which remains a stated limitation rather than an unapproved lifetime refactor.

The eight pure diagnostic cases per provider are explicitly tagged `Diagnostic=Privilege`; they access no database. Native discovery preserves all892 existing portable identities/types/traits and adds16, yielding908. The ordinary profile count is441/provider and Governance.Failures57/provider on this diagnostic branch. Existing test partitions,55 coverage inputs and all security targets remain required for any later delivery; none are accepted by this experiment.

One exact CQ004 exception permits checkpoint instrumentation in the decisive witness method without changing its causal structure or final mutation assertion stack. It belongs to feature009 T015/A-005 D003, owner Implementation lead, and expires15October2026 or must be reconsidered at diagnostic disposition before correction/delivery. No legacy allowance was added or refreshed.

## Pre-experiment verification and review

- `dotnet build tests/aspire/IdentityPlatform.Administrator.Tests --no-restore --nologo -m:1 -nr:false`: passed, zero warnings/errors.
- `python3 scripts/test_mutations.py`:48 passed, including10 new receipt/orchestration controls. The direct runner now executes all module test classes; strict failure/skip handling remains.
- `python3 scripts/test_administrator_selection.py`:17 passed.
- `dotnet test --project tests/aspire/IdentityPlatform.Administrator.Tests --no-build --no-restore --no-progress --pre-enumerate-theories on --filter 'Diagnostic=Privilege' --report-trx --report-trx-filename controls.trx --results-directory /workspace/work/a005-seeded`:16 passed, zero skipped/errors/incomplete counters, including combined primary/probe failure/cancellation and denial/probe-cancellation controls.
- `dotnet run --project eng/IdentityPlatform.CodeQuality --no-build -- --check /workspace/work/OpenIdentityAdmin`:passed with the single explicit diagnostic exception above;1830 unchanged legacy findings retained.
- Full native discovery and portable metadata comparison: all892 originals preserved; only16 reviewed pure diagnostic additions.

Independent architecture/security and test reviewers reviewed actual source read-only. Findings fixed before experiment: direct Python execution omitted the new test class; orchestration final receipt writing could mask primary failure/cancellation. Seeded regressions cover both. Both reviewers support only the bounded diagnostic execution, not product correctness, causal closure, acceptance or merge.

## Authorized execution and stop rules

Run the following once each in the same clean disposable checkout of the published diagnostic revision, with the pinned SDK/environment, after review:

```sh
python3 scripts/diagnostic_governance.py ordinary PostgreSql --disposable-checkout
python3 scripts/diagnostic_governance.py ordinary SqlServer --disposable-checkout
python3 scripts/diagnostic_governance.py mutant SqlServer --disposable-checkout
```

Each ordinary invocation selects exactly the decisive witness and runtime-principal privilege methods. The mutant invocation selects only the reviewed omitted-witness-lock method and exact substitution, retaining strict identity/metadata/marker/stack checks. Command bounds remain180seconds for build/execution and60seconds for discovery. Exclusive attempt files prevent identical local retries. Both same-source ordinary receipts must exist before the SQL mutant. A classified unmodified ALTER success, visible probe column or actual witness bypass marker halts subsequent experiments and requires a remediation decision. Unknown remains unknown; an ordinary failure is never accepted as a passing baseline.

The existing source-restoration finally and explicit SHA256 comparison apply even when failure or cancellation occurs. Source restoration does not claim restored build/test acceptance. Disposable builds are not publishable product outputs. Raw temporary reports are removed after their safe observations are retained; all observations, incomplete experiments and limitations will be recorded below. After this disposition, stop for a separate correction/delivery decision.

## Results

Not run yet. Publication and source review precede all database experiments.
