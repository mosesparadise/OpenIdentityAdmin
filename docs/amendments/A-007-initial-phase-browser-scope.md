# A-007 — Initial development browser scope

Approved by the requesting user's instruction on 15 October 2026: **“Can you proceed with implementing the roadmap? Also, for now, can we exclude Firefox testing from this initial phase?”** This records that current authorization; it does not renew A-004, A-005 or A-006.

## Scope, owner and expiry

The initial phase is internal development through M02-B2 / feature 011, including delivery of PR #56's native-client prerequisite and MessagePack correction. The requesting user owns the scope decision; the implementing maintainer owns its CI enforcement and restoration. Reassess at B2 convergence (T026) and B3 planning (T027). This exception expires before B3 implementation, any new product browser/API activation, production deployment or public release, whichever comes first. A later extension requires a separate explicit user decision. Preparing B3 prose does not extend the exception.

Firefox execution is deferred during this phase for PR, main, scheduled and manual foundation verification. Chromium and WebKit remain required on both PostgreSQL and SQL Server for every full verification run, including previously narrow executable changes. Documentation-only selection remains governed by the existing strict scope gate. Unknown changes select full verification and both active engines. All native test identities, Firefox CLI support and assertion bodies remain intact. The one-shot diagnostic branches stay frozen and unmergeable.

The seven deferred jobs are six Firefox browser cells (standard, administrator Accounts and administrator Security on each provider) and the Firefox contract cell. They contain 93 ordinary cases at the current inventory. The two-arm history comparison within each Firefox Accounts job is also deferred: four separate diagnostic executions, not coverage inputs. These are **deferred/not run**, never passing or fixed. The previous failed Firefox run and incomplete document observation remain evidence; no assertion, timeout, retry policy or oracle is weakened.

## Required evidence and limits

- Retain every non-Firefox job, including real both-provider tests, Chromium/WebKit browser and contract cases, WebKit contrast, all six mutation jobs / 42 kills with baseline/restored controls, static analysis and supply-chain checks.
- Full verification coverage obligations change from 55 to **48** inputs: only seven Firefox reports are removed. A Chromium-only matrix is rejected; executable BFF changes cannot silently omit WebKit. All nine product-module coverage floors remain unchanged; a missing, empty, duplicated or mislabelled required input must still fail.
- The browser plan must emit a visible A-007 deferral notice. Browser, contract and coverage selection share one explicit source-owned phase policy; no environment skip flag or continue-on-error is introduced.
- PR #56 may proceed to normal squash only after every applicable exact-head check and independent review passes under this scope. This supplies prerequisite delivery, not B2 completion. T005's native cancellation/transaction proof and the full issuer/client implementation remain required.
- All six historical findings, 48 human rows, Firefox acceptance and production/activation/public-release blockers remain open. No browser support or whole-product acceptance claim follows from the amended green run. The approved v0.2 snapshot is unchanged.

## Restoration obligation

Feature 011 T028 tracks restoration before B3 implementation. Restore Firefox in the shared engine policy, require all 55 original coverage inputs plus any newly added obligations, execute all three engines on both providers, resolve the actual failed history observation and complete independent review. Remove the A-007 phase notice only with that reviewed change. A green two-engine run cannot satisfy this task or close ADM-EVID-01.

Implementation: `scripts/browser_matrix.py` owns active engines and contract partitions; `scripts/check_coverage_inputs.py` validates the matching obligations; `.github/workflows/verify.yml` consumes the generated matrices. Independent controls enumerate required engines, contracts and all coverage inputs. Current results and delivery identity belong in the feature verification record and PR, not inferred from this approval.
