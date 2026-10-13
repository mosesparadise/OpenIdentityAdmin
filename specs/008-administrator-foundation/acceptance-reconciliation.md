# T035 — administrator acceptance and human handoff

Status: evidence reconciliation and human handoff prepared; document delivery does not complete human or whole-increment acceptance. This record reconciles evidence; it supplies no new runtime sequencing authority.

## Accepted scope and remaining obligations

| Scope | Actual state | Remaining requirement / owner |
| --- | --- | --- |
| Bootstrap, permission storage and protected factor storage | Delivered through reviewed both-provider PRs #21/#27 | Wider account lifecycle remains separate |
| Ceremony protections, enrollment/login and restricted recovery/security | Scoped runtime delivery in PRs #29–31 | Whole MFA acceptance still open |
| Strict native evidence partitions and automated MFA behavior | T027 accepted in PR #33; T028 automated scope accepted in PR #34 | Human portion of T028 remains open |
| Operations/telemetry handoff | Scoped T029 delivery in PR #35 | A-003 expired; overall T029 remains open |
| Worker incident ADM-OPS-01 | Original failure retained; T033 diagnostic delivery PR #44, squash `65031d7`; cause unresolved | Implementation lead owns new evidence; independent architecture/test review required for causal closure |
| Firefox incident ADM-EVID-01 | Original failure retained; T034 reconciliation PR #45, squash `b990745`; cause unresolved | Implementation lead owns new evidence; independent test/architecture review required for causal closure |
| SQL/WebKit certificate-evidence finding | Run `38386343247` failed; reviewed diagnostics in PR #44 and final green run `38390215923` do not establish cause | Implementation lead owns follow-up; architecture/test/security review required for closure |
| Human administrator assessment | User volunteered on 13 October 2026; 48 proposed case/cell rows not_run | User performs observations; exact tools/matrix confirmation and independent results reviewer remain pending |
| Governance T019 | Reviewed design in PR #39 | T020/T021 runtime implementation remains blocked unless existing gates close or a concrete amendment is approved |
| Admin BFF/API and React | Not yet delivered/activated | M02-B–E; governed internal policy is not endpoint acceptance |
| Public release and assurance | Blocked | Outstanding incident/human/product requirements, independent assurance and certification remain mandatory |

Existing T017/T018/T028/T029 umbrella tasks remain unchecked. The scoped deliverables above do not establish whole-increment acceptance. The separate relying-party pilot's human assessment, authenticated API documentation/vendor clearance, legacy engineering debt and broader protocol/account lifecycle remain visible in the roadmap.

## Human handoff

The requesting user is the assessor. The [prepared Windows/WSL2 handoff](../../docs/operations/administrator-manual-handoff.md) proposes PostgreSQL/SQL Server × Edge/Firefox, using an isolated Edge vault or an assessor-selected independent vault and NVDA. All 48 case/cell rows are not_run. Exact versions, selected independent password manager, agreed tools and independent reviewer are not invented.

Provision a fresh disposable environment/account for each browser/provider cell so once-only bootstrap/enrollment can be observed. Coordinate enrollment autofill with the first enrollment, and capture keyboard/screen-reader/visual observations during the applicable journeys. Repeat with another freshly provisioned environment where an irreversible journey variant requires it; do not reset security rows. The existing operations runbook and manual assessment procedure remain authoritative.

The assessor returns safe observed outcomes and defect/retest lineage. Template generation, browser automation and AI review cannot mark human rows passed. Failed or blocked prerequisites remain failures/gaps; no certificate bypass or secret-bearing evidence is permitted.

## Decision after bounded investigations

If no historical cause is established, more identical passing runs or unrelated cleanup will not close these gates. The [proposed A-004](../../docs/amendments/A-004-governance-sequencing.md) would permit only the internal T020/T021 governance work under explicit conditions while preserving all three incident findings and human/whole-increment/public-release blockers. Its expiry and recurrence rules are part of the decision. The stricter alternative is to keep runtime development blocked until all existing acceptance prerequisites close.

A-004 is a proposal, not approval. No T020/T021 production source, new schema or governed mutation implementation is included in T035. Only the user's explicit approval of the concrete final amendment can change this sequencing boundary.

## Verification record

T033 was normally squash-merged in PR #44 (`65031d7`) after exact head `15c6e0c` passed all 62 jobs in run `38390215923`, 49 coverage inputs and nine module floors. Independent evidence review verified both AppHost eight-case suites, both Firefox account/diagnostic suites, and all 30 SQL/WebKit issuer cases. T034 was normally squash-merged in PR #45 (`b990745`) after final head `99c75ff` passed the documentation, secret, assurance and final acceptance checks in run `38392155746`; expensive suites were intentionally omitted by the unchanged docs-only policy, not counted as product passes.

Independent architecture/security review supports proposing the explicit three-finding A-004 only with final required CI and converged review. The handoff has 48 unique unexecuted rows and valid procedure/runbook/template links. This T035 PR must pass its own required checks and normal squash before requesting the decision. Because the amendment file is under docs/amendments, the existing selector requires full CI even though it is an inactive proposal. Its final exact-head result and merge belong in the PR evidence; no pending check counts as acceptance. Documentation preparation supplies no human observation.

A-004 remains **not approved**. The existing T019 boundary requires explicit authorization for a new sequencing exception. Publishing this proposal does not activate it or begin T020/T021.
