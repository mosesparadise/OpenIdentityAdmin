# Verification: Assurance Register Baseline

## Planning and independent design review

Spec Kit 1.1.3 installed workflows used: resolved spec template, persisted feature pointer, setup-plan and setup-tasks scripts, clarify/quality scan and read-only analyze. No extension hooks are configured. Analysis covered six REG requirements, four success criteria and twelve tasks: all requirements have tasks; no unmapped task or blocking conflict. Requirements checklist: eight checked, zero unchecked before implementation.

Architecture reviewer and test reviewer found two design ambiguities: editable metadata must not define its own source trust anchor, and applicability/result pairs must be exhaustive. Both were resolved in plan/data-model before checker implementation. Added independent constants, coordinated replacement regression, explicit pair matrix and rejection of stale exception metadata. Test recommendations also added canonical date/chronology, strict parsing, symlink containment and no-skip/nonzero test-run safeguards. These were design reviews, not executed acceptance.

Official source acquisition: immutable OWASP CSV SHA-256 and 345-row inventory independently inspected, then reproduced unchanged. This proves provenance/inventory only. All control applicability and verification remain pending.

## Execution

Local commands actually executed on 10 October 2026:

| Command/check | Result |
| --- | --- |
| `python3 scripts/check_assurance.py` | Pass: 345 controls, all not assessed; complete official digest/inventory and local references |
| `python3 scripts/test_assurance.py` | 27 methods pass, no skips or expected failures; positive/negative subcases include all status pairs |
| `python3 scripts/test_verify_evidence.py` | 8 pass |
| `python3 scripts/test_coverage_gate.py` | 3 pass |
| `python3 scripts/test_supply_chain.py` | 8 pass |
| `python3 scripts/check-ai-instructions.py` | Eight skills and eight reviewer configurations pass structural checks |
| Source SHA-256 and local Markdown-link inspection | Exact official hash; no missing checked links |
| `git diff --cached --check` | Sole warning is the immutable official CSV trailing blank line; all authored files pass when that exact source file is excluded |

The first test-first invocation failed at import because the checker did not yet exist; that is missing-module evidence, not a behavioral regression. Initial implementation correctly exposed an upstream trailing blank CSV line; source-only parsing now accepts that line after checking the exact immutable bytes. The editable register retains strict row-width validation.

Independent implementation review identified two medium traceability defects and one low runner issue. Main reproduced missing/duplicate/comment-only task declarations as three failing regressions, then anchored parsing to actual definitions and added the valid M10-A prose case. A focused routing regression failed before consent/resource-server/logout/client links were corrected, then passed. The runner now rejects expected failures as well as skips, zero tests and failures/errors; a synthetic outcome regression covers each result. Historical failing outputs are local scratch evidence; the permanent tests and reviewed correction are retained in the repository.

Architecture review independently reproduced and then closed task-definition and routing findings. Test review independently ran the final baseline and all 27 tests, closing both gate findings. Compliance/source review independently verified official digest/counts, pending states, empty evidence, ownership/attribution and release tensions, then closed the routing finding. These are scoped AI reviews, not independent security assessment.

REG-001/002 and SC-001/003 trace to the source/inventory and routing tests. REG-003/004 and SC-002 trace to parsing, trust-anchor, identity, reference, status, date, path and evidence-field regressions. REG-005/006 and SC-004 trace to the guide/roadmap, dedicated CI gate and recorded independent review. No runtime, provider schema, browser or endpoint changed; existing .NET/provider/coverage jobs remain required in CI. No new product-control pass is claimed.

## Remote verification and convergence

[PR #3](https://github.com/mosesparadise/OpenIdentityAdmin/pull/3) is open. Initial full [CI run 38053344105](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38053344105) passed all seven jobs on `9d21f7c2b8e963879c83ecc7cdc6b37afe17f069`: 151 unit + 7 PostgreSQL + 7 SQL Server .NET tests, 27 assurance regressions and the existing 19 gate regressions, with no skipped cases. Static-analysis negative controls, supply-chain checks and all nine 90% line / 80% branch floors passed. [The manifest](evidence-manifest.json) records job/artifact IDs and digests; normal CI artifact retention is seven days.

GitHub review then requested exact source/attribution metadata protection and current lifecycle wording. Two attribution-mutation subcases failed before correction, then all 27 methods passed after adding canonical source/attribution to the independent pin. The test reviewer independently reran and closed that correction. The final follow-up changes those pins, expands the mutation matrix and updates documentation; it does not change runtime code. This baseline evidence is not a claim that the later head ran the same jobs: use PR #3's live checks for the exact final head, and require them to pass before merge.

Spec Kit convergence inspected all six requirements, four success criteria, the plan decisions and all 18 constitution principles: no remaining buildable-scope finding. The convergence operation left tasks.md unchanged; subsequent implementation closeout marks the publication/verification task complete. Final review/merge remains the PR workflow. M01-C remains partial: named owners, final applicability, complete normative OAuth/NIST crosswalks, product/deployment evidence and independent assessment are ongoing work. Feature 003 acceptance is the bounded register/tooling baseline.
