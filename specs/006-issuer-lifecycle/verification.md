# Issuer lifecycle verification — no runtime acceptance yet

11 October 2026: feature 006 is a design draft under approved M01-E. There is no new lifecycle project, production change, executed lifecycle case, or accepted restart/two-node/rotation result.

Research inspected the existing credential loader, DP issuer discriminator, pilot protocol/form fixtures, Aspire13.6.1 command/notification APIs, process cleanup and test-selection pipeline. Independent architecture/security research identified stale health/no-op restart, missing replacement-process cleanup, auto-login fallback and expiry-only replay false positives. The initial plan incorporates these findings; final design acceptance awaits user structure/sequencing choices and concrete harness review.

Spec Kit template resolution and setup-plan/setup-tasks scripts ran for this feature. Extension hooks are absent. The [checklist](checklists/requirements.md) deliberately remains incomplete while those choices/reviews are open. No implementation command or required-check waiver is being inferred.

The prior accepted code checkpoint is PR #16 squash `559380e952059383741a7bc75c259b5bb39ed1e1`, whose documentation-only CI passed 13 checks/901 ordinary cases. That evidence does not exercise lifecycle scenarios. T021's human assessment remains open, as do M01-F and public-release gates.
