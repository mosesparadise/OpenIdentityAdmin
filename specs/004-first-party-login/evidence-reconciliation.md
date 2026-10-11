# T029 evidence reconciliation

Authorized sequence: finish and squash T030, then reconcile this pilot in its own reviewed PR. T030 is merged in [PR #12](https://github.com/mosesparadise/OpenIdentityAdmin/pull/12) as `fca82fb`; final head `4cdb043` passed all 17 checks and 845 ordinary cases with zero failures/skips in [run 38148540658](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38148540658). This document records the T029 plan before editing the status registers. No product behavior or approved acceptance criteria change.

## Scope and method

1. Create a current acceptance matrix linking all LOGIN-001–009, SC-001–005, story scenarios and issuer backlog requirements to concrete test groups, the accepted revision/run and remaining limits.
2. Reconcile T004–T030 individually against source and executed evidence. Close implemented tasks only where their stated obligations are met; retain manual/full-feature obligations. Replace stale task-list chronology with a short delivery register, preserving detailed history in `verification.md`.
3. Align README, roadmap, issuer backlog, runbook and quickstart with merged capability. Distinguish the automated test relying party from a shipped interactive client, and document Windows/WSL2 prerequisites without claiming an executed Windows assessment.
4. Add narrowly scoped implementation/test/evidence references to relevant official ASVS rows. Keep all 345 applicability decisions pending and all results `not_assessed`; do not invent assessors, dates of assessment, exceptions or certification claims.
5. Obtain independent test, documentation and compliance reviews. Check repository links, Spec Kit prerequisites, assurance integrity and its regressions. Run the existing exact-head CI selection, fix findings, inspect review state, then squash-merge under a Conventional Commit title.

## Delivery checks

- [x] REC-A Confirm approved scope, prerequisite documents and T030 merged evidence.
- [x] REC-B Reconcile requirements, task state, operational guidance and limited control references.
- [x] REC-C Resolve independent review findings and validate documentation/register integrity.
- [x] REC-D Verify the final PR head through the existing required gates and squash-merge; record final CI/merge on the PR to avoid a self-referential evidence commit.

## Preserved gates

Full pilot acceptance remains distinct from automated acceptance. Qualified screen-reader/password-manager review, timed usability work and deployment-specific assessment stay explicit. M01-E restart/two-instance/key overlap and M01-F versioned Admin API/BFF contracts, Scalar/Swagger and generated-client gates remain unbuilt. Admin MFA/BFF/React, later protocol journeys, retention/recovery/performance and R1 assurance remain in the approved roadmap. A-001 still blocks public release on actual certification; A-002 is expired and its reproduced browser finding was corrected through the existing closure criteria. No production operation or external declaration is authorized by this reconciliation.

## Reconciliation findings

Independent source-to-requirement review identified specified cases not directly exercised by the existing HTTP suite: missing/body-only/conflicting client authentication, unsupported grants, wrong protocol methods/content types, forwarding-header non-influence, and committed eligibility changes at a new/resumed authorization boundary. Existing configuration/password-entry/redemption tests do not establish these other boundaries. Record T031/T032 as follow-up acceptance tasks, keep T018/T021 and full M01-D open, and retain the passing 845-case checkpoint with its actual scope. No runtime defect is asserted from this evidence gap, and no runtime product implementation is added under T029.

Adding valid candidate evidence exposed a supporting test-fixture defect: all 27 assurance regressions failed during setup because the temporary register lacked the referenced implementation/evidence files. Its fixed 10 October date would also reject newly dated valid evidence. Before modifying that fixture, the corrective plan is to copy each validated repository-contained reference into the disposable root and capture the current date once per test invocation, expressing synthetic valid/expired/future cases relative to it. Keep the production validator, catalogue, inventory, negative controls and strict no-skips runner unchanged. This is a necessary assurance-test maintenance change, with no runtime product behavior; a scripts change requires the ordinary full six-cell CI selection.

## Source verification and review

The current matrix covers all LOGIN requirements, success criteria and story acceptance scenarios, linking merged evidence while retaining T031/T032 and qualified manual obligations. T004–T017/T019 and T022/T029 are reconciled for their stated scope; T018/T021 and full feature acceptance remain open. Twelve ASVS rows gain candidate references only; all 345 remain pending / `not_assessed`, with no assessment or exception metadata added.

Independent test, documentation and compliance reviewers accepted the source changes. Findings corrected missing HTTP/authorization boundary allocation, forwarding-header non-influence semantics, the bootstrap partial-class test identifier, WSL2 and test-only relying-party guidance, and a health-only statement that needed qualification by `Pilot:Enabled=false`. Historical task plans now link to the current matrix. Reviewers inspected source and evidence references; their review is not an independent test execution or external assessment.

The supporting assurance fixture correction passed all 27 existing regression cases with zero failures/skips after the original setup failures. The test reviewer confirmed validated reference containment, cleanup and preserved date/negative controls; compliance review confirmed the production validator and unassessed register state remain unchanged. Local assurance integrity, AI instruction inventory, Spec Kit prerequisites, changed Markdown target/heading checks and whitespace validation pass. No .NET runtime code, packages, production configuration, acceptance threshold or required case is changed.

REC-D is the delivery gate, not a request for new user approval: existing authorization covers this PR's review, fixes and normal squash merge. Its final tested source/merge revision, exact required-check results and actual squash commit are recorded on the PR to avoid a self-referential evidence commit. The source checkpoint alone does not assert completed CI or a merge.

REC-D delivery completed: PR #13 squash-merged as `237afa5` after source `87492cd` passed all 17 checks and 845 ordinary cases with zero failures/skips in [run 38151249880](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38151249880). Its tested merge tree matched the source and squash tree. Earlier pending statements above describe the pre-delivery checkpoint.
