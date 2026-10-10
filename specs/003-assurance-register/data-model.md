# Assessment data model

`catalogue.json`: source/version/tag/commit/url/path/SHA-256/row_count/license/attribution. Source path is local and contained. The expected version, tag, commit, URL, SHA-256 and 345/17/80 counts are independently pinned as reviewed checker constants; editable metadata alone cannot change the trust anchor. They must match before interpreting rows.

`asvs-5.0.0.csv`: unchanged official columns `chapter_id,chapter_name,section_id,section_name,req_id,req_description,L`; source IDs unique, required levels 1–3.

`roles.json`: IDs and responsibility descriptions with `named_owner: null` while assignment is pending. Role ownership does not imply a person has accepted it.

`threats.json`: unique IDs, statement, assets, boundaries and owner role. This is initial threat routing, not a complete reviewed deployment threat model.

`controls.csv`: exact header with these fields:

- `control_id,source_id,source_version,level`: unique normalized identity, exact official ID, 5.0.0 and original level.
- `triage`: expected/conditional/mixed/deployment; provisional routing only.
- `applicability`: pending/applicable/not_applicable; `rationale` is always required.
- `owner`: role ID; `requirements`: existing FR/NFR IDs separated by `|`; `tasks`: existing Mxx-X roadmap task IDs; `threat_id`: existing threat.
- `component`: planned enforcing boundary; `implementation`: zero or more local file paths separated by `|` (empty while not built).
- `automated_tests`, `manual_assessment`: test IDs or concise assessment procedure; empty when unassessed.
- `evidence`: contained existing local files separated by `|`; `evidence_date`: ISO date not after today; `evidence_scope`: component, product revision, configuration and provider/browser scope (where relevant).
- `result`: not_assessed/pass/fail/not_applicable/exception. No implied state changes.
- `residual_risk`: explicit outstanding risk; required for an exception.
- `exception_approver`, `exception_expiry`, `exception_reason`: approved exception metadata; expiry is an ISO date on/after today. No expiry means no valid exception. An exclusion uses approver/reason but no expiry; its reassessment trigger remains mandatory.
- `assessed_by`, `reviewed_on`: nonempty reviewer and nonfuture ISO date required for assessed outcomes; initial reviewed_on remains empty until an assessment review occurs.
- `reassessment_trigger`: always required.

Pass requires applicability applicable, implementation + evidence + date + evidence_scope, assessed_by/reviewed_on and at least one automated/manual assessment. Fail requires applicable status and dated assessment evidence. Exclusion requires applicability not_applicable, assessed_by/reviewed_on, approver/reason and dated evidence. Exception requires applicable status, reviewed assessment, dated evidence, residual risk and approval with valid expiry. Pending applicability permits only not_assessed. Any populated local reference or date is validated, including in pending records.

Schema validity is necessary but does not authenticate approvals, assess independence, prove test IDs ran or verify evidence content. A human reviewer owns those decisions. Multiple-value cells cannot include empty entries or duplicate tokens.

Allowed pairs are exhaustive: pending → not_assessed; applicable → not_assessed/pass/fail/exception; not_applicable → not_applicable. Exception approver/reason/expiry must be empty in other results. Exclusions require approver/reason and an empty expiry. Assessed review date must not precede evidence date. Dates must use canonical YYYY-MM-DD (not compact ISO).

Reject duplicate JSON keys, wrong JSON shapes, malformed CSV quoting/row widths, empty or duplicate multivalue tokens, and absolute/traversal/URL/broken/symlink-escaping file references. Snapshot consistency is checked; historical state transitions and authentic approvals require human review.
