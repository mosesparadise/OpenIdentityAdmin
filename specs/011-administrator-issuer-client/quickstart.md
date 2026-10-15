# B2 implementation validation guide

This guide describes future validation. No B2 build, package restore, database case, mutation or browser exchange has run for this planning packet. Use [tasks](tasks.md) and the [protocol contract](contracts/administrator-protocol.md) for requirements; do not run commands for nonexistent partitions.

## Prerequisites

Verify current remote main, planning delivery and the exact implementation branch. Read AGENTS, approval, roadmap, current handoff, accepted B1 delivery and this feature's specification/plan. Use the pinned SDK/packages and trusted HTTPS issuer/client origins. Real PostgreSQL and SQL Server and independent issuer/client instances are mandatory. Preserve all historical failed receipts. Local Docker capacity is currently insufficient; use reviewed hosted capacity for native execution rather than repeating failed pulls/tests.

Use synthetic identities, disposable encrypted key material and real password/TOTP/antiforgery account journeys. Test-only callback transport is isolated from production route composition. Never copy private test material into the repository, artifacts or logs. Existing supported image preparation/deadline rules apply; no manual historical diagnostic dispatch is implied.

## Evidence sequence

1. T004/T005: inventory pinned client packages/lock changes and actual native handler orders. Demonstrate source bindings for assertion verification/consumption, PAR reference identity, native client state/correlation, strict expiry and no-retry configuration. Identify manager transaction and admission linearization points before claiming a testable implementation.
2. T006: discover actual Given–When–Then cases and establish exact mandatory selectors/coverage inputs. Use the existing `scripts/verify.py --help` and selectors after they are implemented; never invent counts or skip a required group. A selector change receives seeded rejection controls.
3. Run focused unit/composition/proof/claim tests, then both-provider initialization/grants and B2-ADM/REG/PROOF/CLIENT/OPS scenarios. Use native reports with complete counters, result/definition/execution bindings and no required skips.
4. Real positive path: initialize exact registration, obtain a genuine issuer administrator session via password/TOTP, begin native client challenge with required PAR/S256, perform `prompt=none` authorization, and complete code exchange through test-owned native callback. Check protected/native outputs, exact subject/session and audience; issue no product BFF cookie.
5. Repeat the denial/race matrix, independently exercising client assertion, PAR request, code and callback-state single-use. Include two-node/restored client state, key retention/loss, native error callback, lost response/cancellation and actual no-resubmission HTTP counts.
6. Run all new and existing security mutations with same-source ordinary/restored controls and an observed decisive kill. Reconcile raw report/discovery and sanitized mutation receipt limitations; no exception/location ambiguity counts as a demonstrated kill.
7. Run applicable full exact-head CI, review every required job and native artifact, retain all existing coverage inputs/floors and include new affected composition. Independent architecture/test/security/API reviewers inspect actual source/evidence; record defects, corrections and limits in `verification.md`.
8. Only then mark B2 implementation acceptance and normally squash. Separate delivery records name exact head, tested merge/tree, run and squash. B3/C/D/E and historical/human/release work stay open.

## Planning packet checks actually available now

From repository root, proportional checks include `python scripts/test_ci_scope.py`, `python scripts/check-ai-instructions.py`, `python scripts/check_assurance.py`, `python scripts/test_assurance.py` and `git diff --check`. Also check relative Markdown links, unique task IDs and requirement-to-task coverage. The existing fail-closed scope selector must classify the complete diff as prose; policy/executable changes require full checks. This does not establish B2 runtime support.
