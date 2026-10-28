# Dormant cleanup assertion ownership

## Candidate scope — 28 October 2026

This T003 increment starts from main `3fec8b8bf1692e13113544e11b7b0601dfcf913a` after PR112/113. It implements a separately typed, explicitly composed cleanup client-proof receipt. It uses native cryptographic assertion validation and the existing durable assertion ledger; it does not add a production endpoint, change the B3 manifest, reserve Sessions credential/cleanup slots or enable refresh.

The sole eligibility exception is central `AdministratorProtocolProfile.Enabled=false`. The exact B3 application, scope, key, destinations, permissions, native enabled markers and revision remain mandatory. Existing PAR/token registration and consumption still require enabled issuance. Cleanup receipts cannot satisfy issuance. T005 owns credential/cleanup capacity and T007 owns the future native revocation permission; those dependencies remain open.

Consumption commits in a fresh child scope after native client authentication, under the existing profile lock and original request deadline. Only completed commit and actual child disposal permit a receipt. A separate fresh cleanup epoch may claim it once; request/transaction/context/principal/parameter changes, expiry, stale ownership and registration revision changes deny continuation. Every original request parameter is frozen before the consumption await. Downstream rejection or rollback cannot restore a consumed assertion.

## Finite verification inventory

Three new required persistence theories execute on both PostgreSQL and SQL Server, adding six identities without replacing existing tests:

| Scenario group | Required observations | Traceability |
| --- | --- | --- |
| Native proof and eligibility | Real RSA proof, cleanup-only receipt, shared cleanup/PAR/token replay denial across independent roots; central disablement permits cleanup proof but not issuance; malformed proof, wrong key and exact registration drift deny, restored controls succeed | REF-AUTH/END; FR-003/009/013 |
| Receipt and commit ownership | Fresh owned continuation; foreign/replaced request/transaction/endpoint/context/proof/registration and duplicate/stale claims deny; exact before/after commit and actual disposal faults; later rollback preserves spent proof; unclaimed and claimed receipts deny after native rejection | REF-OWNER/LOSS/END; FR-007–009/013 |
| Ledger bounds | Exact existing 10,000-row assertion limit, strict expiry/retention equality, at most100 reclaimed entries, shared digest namespace, clock rollback and denied speculative-cleanup rollback; no live eviction | REF-OPS; FR-008/013 |

The ordinary native observer runs immediately after actual client-proof consumption and before generic-token validation without altering native flags, principals or errors. Separate hostile component probes deliberately substitute individual inputs. The claimed-receipt rejection control invokes the actual native `ValidateGenericToken` stage inside a fresh owned child while the receipt phase remains live; it is a selected-stage control, distinct from untouched full-dispatch controls. The synthetic invalid target must still produce native `invalid_token`; the independently read assertion row remains spent. Receipt tests exercise a component continuation inside the same original request, not successful revocation. They prove neither endpoint-permission handling, selector/family authorization, actual HTTP confirmation, TLS loss nor genuine B3 sign-in.

Independent architecture/security and test source reviews pass after corrections: canonical native JSON serialization preserves parameter kinds; opaque registration bindings require the original request/owner/phase; additive composition retains one proof capture; actual persisted B2 is positively readable for issuance and excluded from cleanup; foreign transactions preserve all other inputs; swallowed duplicate claims still poison the request. Fresh-scope rejection and stale-phase controls preserve the existing lock freshness protection. All19 requirements-quality checklist items are checked; they are not runtime evidence. Local locked restore and persistence build pass with zero warnings/errors; code-quality checks pass without allowance growth. Native discovery preserves all24 previous persistence identities and adds exactly six (15/provider);30 selector controls pass. Required CI expects4,703 ordinary passes, retaining95 jobs,89 artifacts,79 reports,76 coverage inputs/15 floors and64 mutation controls. Hosted execution and coverage remain pending. All T003–T022, historical/human/Windows/A-009/production/public-release gates stay open.

The three new native theory names are `Given_NativeCleanupProof_When_EligibilityAndReplayAreExercised_Then_OnlyExactDenyOnlyProofIsConsumed`, `Given_ConsumedCleanupProof_When_ReceiptAndCommitBoundariesChange_Then_OnlyOriginalLiveOwnershipContinues` and `Given_DisabledCleanupProfile_When_LedgerBoundariesAreReached_Then_RetentionAndCapacityRemainStrict`, each with PostgreSql and SqlServer provider rows.

## Retained rejected acquisition

Run40738346731 at source`65dd717` failed persistence discovery before database execution because `eng/persistence-test-matrix.json` omitted the six new metadata records. Updating numeric totals alone was insufficient. The correction adds exact native-discovered metadata, independently preserves all24 prior records, and runs the same complete-inventory and both-provider selection checks locally. This failed run is not acceptance evidence; a corrected exact-head full run is required.
