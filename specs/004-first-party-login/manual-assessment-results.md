# Manual pilot assessment results — unexecuted template

**Status: NOT RUN. No assessor observations, compatibility result, WCAG determination or acceptance is recorded here.** Copy this template for each session; preserve it as a blank template in the repository. Follow the [assessment instructions](../../docs/operations/pilot-manual-assessment.md). Sanitize a completed copy before any PR or chat attachment; do not include credentials, callback URLs, browser storage, vault exports or raw recordings.

## Session and preflight

| Field | Recorded value |
| --- | --- |
| Session ID / UTC date | Pending |
| Human assessor / role / relevant competence | Pending; use an approved identity reference for shared evidence |
| Engineer / qualified accessibility reviewer | Pending |
| Tested commit / clean working tree | Pending |
| Product scope / excluded and externally supplied client surfaces | Pending |
| Windows / WSL distribution and versions | Pending |
| Database provider / exact version | Pending; separate PostgreSQL and SQL Server observations |
| Browser / engine / version / isolated profile | Pending |
| Screen reader / version / modes and settings | Pending |
| Password manager / version / synthetic vault isolation | Pending |
| Theme / viewport / scale / text resize / forced-colors | Pending |
| Supplied RP name / exact release or build / source revision (or vendor build identifier) | Pending |
| Reviewed RP configuration reference / engineer sign-off | Pending; private configuration must not be pasted here |
| TLS trust / bootstrap / genuine success and fresh-login preflight | Not run |
| Matrix selection / alternatives / coverage rationale | Pending qualified assessor review |
| Evidence owner / access restrictions / retention and disposal decision | Pending |

## Cases

Duplicate each applicable row for every selected provider/tool/state combination. A row may not stand for unexecuted combinations. Record actual mode, theme and viewport alongside the matrix cell. Blank observations never imply pass.

| Case | Provider / cell / appearance/state | Status | Actual observation / sanitized evidence ID | Defect ID / assessor / UTC |
| --- | --- | --- | --- | --- |
| MAN-01 | Pending | not_run | None | Pending |
| SR-01 | Pending | not_run | None | Pending |
| SR-02 | Pending | not_run | None | Pending |
| PM-01 | Pending | not_run | None | Pending |
| PM-02 | Pending | not_run | None | Pending |
| MAN-02 | Pending | not_run | None | Pending |
| MAN-03 | Pending | not_run | None | Pending |
| MAN-04 | Pending | not_run | None | Pending |
| MAN-05 | Pending | not_run | None | Pending |
| MAN-06 | Pending | not_run | None | Pending |

## Qualified accessibility coverage

Provide or reference a complete **WCAG 2.2 A/AA** inventory for the scoped process. These empty fields are not that inventory. The reviewer records each official criterion, applicable surface/state, method, outcome, evidence and rationale for any non-applicability. Include omitted platforms/assistive technologies and the impact on the disposition. This pilot report cannot establish product-wide conformance.

| Criterion / official version | Applicability and rationale | Method / exact state | Result / evidence / finding |
| --- | --- | --- | --- |
| Pending assessor inventory | Not assessed | Not run | No evidence |

## Findings and retests

| Finding ID / severity / requirement | Safe reproduction / expected vs observed | Owner / fix commit | Original result / retest case, revision and disposition |
| --- | --- | --- | --- |
| No findings recorded because no assessment ran | Not run | Pending | Not run |

## Disposition

- Executed / passed / failed / blocked / not-run case counts: **not yet measured**.
- Selected matrix completeness and missing provider/tool/state cells: **pending**.
- Qualified scoped accessibility disposition, reviewer and date: **pending**.
- Unresolved findings, rationale and authorized exceptions: **pending assessment; no exception granted**.
- Exact automated CI source/run and relation to manually tested revision: **pending**. Historical automation is linked in the [acceptance matrix](acceptance-matrix.md); do not copy it as this session's evidence.
- T021 / full M01-D: **open**. This template does not close either item.
- NFR-09 five-minute administrator application creation: **not run; future M09-A journey**, not a pilot-login threshold.
- M01-E/M01-F, broader product and public-release certification gates: **unchanged/open**.
