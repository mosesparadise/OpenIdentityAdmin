# T021 manual acceptance handoff

Status: **handoff prepared and independently reviewed; CI/delivery pending; no manual results recorded**. Date: 11 October 2026. This is the execution plan for the next outstanding feature 004 obligation, not a new product scope or an acceptance waiver. Approved v0.2, LOGIN-007/009, US2 AC4, FR-19 and NFR-09 govern the work. [T032](authorization-state-acceptance.md) has squash delivery in [PR #15](https://github.com/mosesparadise/OpenIdentityAdmin/pull/15), commit `f1b1a4fe040a7e425557ed51ce835572264fda78`.

## Outcome and boundary

Make the remaining human assessment executable and auditable: prerequisites, explicit manual cases, provider/browser/assistive-technology scope, results template, defect/retest process and closure criteria. The lead can deliver and review this handoff now. Named human assessors, a configured interactive relying party and actual manual results are still prerequisites to closing T021. The automated test relying party exits with its fixture; starting AppHost does not supply an interactive client. Do not label this handoff a completed manual assessment.

No production code, project structure, dependency, security policy, CI selection rule or numeric acceptance target changes. No additional product client is silently introduced. Use existing `docs/operations/` and feature 004 documentation locations. Full M01-D remains partial; M01-E/M01-F and later work retain their existing allocation. The NFR-09 five-minute target is **administrator application creation**, not login; that timed product acceptance stays in M09-A after its journey exists.

## Tasks and dependencies

- [x] MA-001 Inspect the approved requirements, current Razor states, operational runbook, T032 final evidence and Spec Kit prerequisites. Requirements checklist: 7/7 checked; no extension hooks configured.
- [x] MA-002 Write `docs/operations/pilot-manual-assessment.md` with setup/preflight, cases, proposed Windows/WSL2 matrix and security-conscious evidence handling. Add `manual-assessment-results.md` as an explicitly unexecuted copyable result template.
- [x] MA-003 Obtain independent UX and test/evidence review of the handoff and resolve actionable findings. Review source and instructions only; AI review is not human assistive-technology evidence.
- [x] MA-004 Link the pack from the runbook, quickstart, acceptance matrix and roadmap; reconcile PR #15's actual delivery. Keep T021 unchecked and preserve every external/manual gate.
- [ ] MA-005 Check documentation targets and whitespace, assurance/instruction integrity and the normal PR workflow. Record the actual selected matrix/counts, not a copied historical total. Normal squash delivery records exact source/checks/reviews on its PR.
- [ ] MA-006 **Human execution pending:** name an assessor, record capability/scope and configure/verify the interactive HTTPS relying party. Execute each scheduled cell/case, retaining sanitized evidence and defects. Blocked/unrun cells are not passes.
- [ ] MA-007 **Acceptance pending:** fix findings with regression and human retest, obtain qualified scoped accessibility disposition, reconcile requirements, and run/review full-feature gates on the accepted product revision before closing T021. Public-release and wider-product gates remain separate.

MA-001 → MA-002 → MA-003/004 → MA-005 delivers the handoff PR. MA-006 → findings/retest → MA-007 closes manual/full-feature acceptance only when evidence exists. No additional approval is needed to prepare these authorized documents; missing human observations cannot be supplied by an agent.

## Validation and review record

The plan preceded document implementation. Independent UX and test/evidence reviewers passed the handoff scope after three corrections: isolate browser/vault before the first visit, record the supplied client's exact identity/build/source revision separately from its private configuration, and specify single-use/expiring/browser-session-bound correlation with independent tab state. They inspected documentation/current Razor source, not human sessions; neither review closes manual acceptance.

Local checks passed: 111 relative documentation targets across eight changed documents resolve; `git diff --check`; `python3 scripts/check_assurance.py` (345 controls remain not_assessed); `python3 scripts/test_assurance.py` (27 tests); `python3 scripts/check-ai-instructions.py` (eight skills/eight reviewer configurations). Spec Kit prerequisites returned feature 004 with tasks; the requirements checklist is 7/7 and extension hooks are absent. Existing ignore rules cover private key, environment and artifact examples. No production code, test or policy changes were made, so no new implementation-mirroring tests were added.

The unchanged browser selector chooses Chromium on both providers for this documentation-only diff. Full normal PR CI, actual selected reports/counts, exact source/checks/review state and normal squash delivery remain MA-005 and are recorded on the handoff PR. Historical T032's six-engine/provider-cell total must not be copied as this run's count. The PR records final evidence to avoid claiming a future merge in its own source. The [results template](manual-assessment-results.md) must remain visibly unexecuted until actual assessor observations are supplied.
