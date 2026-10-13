# Governance verification diagnostics amendment A-005

Status: **APPROVED for bounded diagnostics only — 13 October 2026 (Europe/London).** The requesting user explicitly approved: “I approve A-005 for bounded governance diagnostics only, keeping PR #48 unmerged and all acceptance/public-release blockers open.” Only the diagnostic scope below is authorized; no correction, delivery or merge is authorized. A-004 expired on incomplete mandatory SQL mutation evidence in PR48. The [verification record](../../specs/008-administrator-foundation/governance-verification.md) retains exact receipts and limits.

## Recommended decision

Authorize one bounded diagnostic increment for GOV-EVID-01 and GOV-PRIV-01, keeping PR48 unmerged and T020/T021 delivery halted. This is permission to improve missing evidence and investigate; it is not acceptance of the failed run or a renewal of governance delivery authority.

The two observed failures are a SQL witness-lock mutant rejected outside its expected bypass assertion, and a SQL runtime-principal test that did not demonstrate its required native ALTER denial. Current receipts cannot distinguish their actual causes. Neither a harmless test defect nor a product trust bypass may be assumed.

## Authorized diagnostic work

1. Extend the existing `scripts/verify_mutations.py` failure path to retain a bounded, secret-safe diagnostic receipt before temporary report cleanup. Include source/run identity, closed phase/ordinal, counters, error-node counts, exact expected-marker/stack match booleans and allowlisted assertion/exception categories. Unknown stays unknown. No raw error text, stack, SQL, identifiers, URLs, credentials or child output. Diagnostic failure must never mask the original failure, delay bounded cleanup, or create `evidence.json` acceptance.
2. Add seeded controls in existing `scripts/test_mutations.py` for malformed/absent reports, canary secret strings, unexpected assertions, failed receipt writes, cancellation and cleanup. Preserve all existing strict verifier checks and issuer7/administrator8/governance6 required targets.
3. Add fixed, nonsecret checkpoint classifications to the existing governance witness test/helpers only as needed to identify the failed stage. Preserve real native wait observations, complete durable-effect assertions, strict marker and original operation/process/test deadlines. Do not turn an unexpected assertion or timeout into a killed-mutant verdict.
4. Improve the existing `Fixtures/Governance/GovernancePrivileges.cs` evidence: distinguish successful DDL, allowlisted native denial, other bounded numeric provider error and cancellation, and attempt the independent administrator-owned schema probe in finally on the failure path. Attempt the probe in guaranteed cleanup while preserving the original ALTER outcome/failure. Use one fresh five-second ceiling covering connection opening and both read-only metadata queries, rather than the already-cancelled ALTER token. This bounds the existing separate probe phase; preserve the five-second ALTER limit and all enclosing test/process deadlines. If the enclosing lifetime cannot accommodate the probe, record not-observed. Record probe failure/cancellation/unknown separately, never as a replacement failure or accepted denial. Add seeded primary-failure-plus-probe-failure/cancellation controls. Record fixed categories/booleans only. Do not broaden accepted SQL codes without documented native evidence and review; never grant the runtime principal additional rights to pass a test.
5. In a disposable checkout of the reviewed diagnostic revision, execute one focused ordinary witness/privilege comparison for each provider and one focused SQL witness mutation experiment. The diagnostic experiment must be explicitly separate from the authoritative six-target suite, preserve its native selection/assertion/deadline rules and write only diagnostic artifacts. If a small orchestration helper is necessary, keep it in `scripts/` under an explicitly diagnostic name; add no project or production endpoint. Record every command, exact source change, observation, failed/cancelled result and source restoration. Do not retry identical experiments to obtain a favorable outcome.
6. Publish a concrete evidence-based disposition and independent architecture/test/security review. Stop there for a separate decision on fixes and renewed governance delivery. New evidence may justify a test/tooling or product correction, but this proposal does not pre-authorize an unknown correction.

## Conditions and limits

- No production runtime logic, schema, permission grant, API/BFF/React activation, onboarding or recovery capability changes. Existing paths and feature/composition conventions remain intact.
- No skipped/quarantined cases, deadline inflation, weakened assertions, incomplete evidence accepted as success, or silent modification of a required mutation inventory. Diagnostic artifacts are never coverage or acceptance inputs.
- If unmodified code demonstrably permits an actual trust/privilege bypass, halt the affected experiment and preserve evidence for a separate remediation decision. Expected deliberate mutant effects remain negative controls, never production acceptance.
- Preserve GOV-EVID-01/GOV-PRIV-01 and all four historical incidents as unresolved until their own evidence-based closure criteria are met. All human/public-release gates remain open. A later passing execution establishes only that execution.
- Existing CI may finish for retained observations. Publish the proposal and any approved diagnostic revision on a separate branch without opening a PR or pushing to PR48, because its existing pull-request workflow would start a full runtime matrix. Do not change CI triggers/selection, use skip flags, or claim CI acceptance. Review source through immutable branch/commit links. Any new execution under this proposal must be explicitly diagnostic and limited to item5; no automatic PR48 squash or downstream feature progression. A later correction/delivery decision must return to a normally reviewed PR and the full unchanged required matrix.
- This authorization expires at delivery of the bounded diagnostic disposition or any expansion beyond these terms. Governance correction/acceptance/merge requires another explicit decision based on that disposition.

## Why this is recommended

The strict verifier correctly refused to count an unrelated failure as security-mutation evidence, but the actual assertion was discarded. The privilege test also withheld the distinction needed for diagnosis. Repeating the same run can produce another generic failure or an unexplained pass. Small reviewed diagnostic changes can distinguish the failure paths while retaining the security boundary and all acceptance gates.

The stricter alternative is to hold all execution pending an external manual investigation. That avoids another controlled experiment but supplies no missing automated receipt by itself.

## Approval wording

> I approve A-005 for the bounded governance diagnostic work only. Keep PR48 unmerged, A-004 expired, T020/T021 delivery halted, and all findings and human/public-release blockers open.

## Independent proposal review

Architecture/security/test reviewers support presenting a bounded diagnostic decision while admitting that both causes remain unknown. Their required clarifications are incorporated: preserve the original failure through receipt/probe failure, use a separately bounded five-second read-only probe, and publish on a separate branch without triggering unapproved full CI. These are AI source reviews of the proposal, not approval or execution evidence.
