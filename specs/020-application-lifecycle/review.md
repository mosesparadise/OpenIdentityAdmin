# Lifecycle planning review

**Date**: 2026-11-04. Product implementation and acceptance remain unclaimed.

## Initial independent findings and dispositions

| Reviewer | Finding | Disposition |
| --- | --- | --- |
| Architecture | No lifecycle state; keep it distinct from editability | Add Enabled and nullable details state in proposed model/contract |
| Architecture | Old pending authority must not revive on enable | Advance existing aggregate revision on both transitions; retain old bindings |
| Architecture | Mutation and permission fallbacks are unsafe for new actions | T004 requires explicit closed dispatch and unknown-action rejection |
| Architecture | Reason has no existing audit storage | Add linked finite reason detail with same transaction and independent verification |
| Architecture | Consumption may commit before later disable | T006/T012 preserve consumption and deny later issuance |
| Test | Simultaneous tasks alone do not prove contention | T012 requires independent roots and provider-observed waits |
| Test | Wrong-audience token rejection cannot prove lifecycle enforcement | FR-010 and T006/T012 use genuine managed issuer outcomes |
| Test | Bulk revocation requires durable jobs and distinct authority | Scope question remains pending; no synchronous scan planned |
| Test | Upgrade, exact inventories and browser/demo evidence required | T002 and T014–T017 explicitly retain these obligations |
| Final test review | Upgrade task pointed to a nonexistent Administrator provider directory | T002 corrected to the existing Persistence ProviderCompatibility test directory |
| Final test review | Real bearer authority may expire before the isolated MFA boundary | T013/plan distinguish component MFA proof from genuine HTTPS native-deadline proof; no lifetime extension |
| Final test review | Global issuer shutdown could satisfy target-only denial tests | FR-006/T006 require a second enabled managed registration to complete a genuine journey without collateral lifecycle changes |
| Final architecture review | Final publication wording implied retracting an earlier committed issuance | FR-006/plan/T012 explicitly linearize at commit and allow the sealed response to arrive after disable |
| Final architecture review | Quiescing only during incompatible downgrade is insufficient | Data model requires traffic remain blocked until compatible enforcement or separately approved recovery |
| Final architecture review | Existing cleanup does not implement audit retention deletion | Data model now records retained audits and cascade only for separately authorized future deletion |
| Security | SQL Server string equality can admit trailing-space reason aliases despite a finite IN constraint | Store a constrained numeric reason code behind exact wire strings; T003 requires both-provider alias/invalid-code controls |
| Security | Strict BFF JSON rejects new API fields; additive DTO is not automatically rolling-compatible | Contract/data model/T007 require coordinated quiesced API/BFF/UI switch and real proxy probes; online bridge remains separate |

## Packet review status

Architecture and test reviewers verified the corrected packet. CHK001–CHK006 are satisfied for
requirements quality; CHK007 remains open. The independent security reviewer also passes the
proposed issuance-only design after the reason-storage and coordinated-deployment corrections.
These are design dispositions, not new runtime evidence or acceptance of the unanswered scope option.
Requirements and custom checklists track document quality separately from the seventeen
unstarted implementation tasks. One optional product-scope question has been asked and no answer
has been recorded. No recommendation has been relabeled as user approval.

Cross-artifact analysis maps all 14 functional requirements and five buildable success criteria
to the 17 tasks; no unmapped tasks or constitution exception was identified. The outstanding scope
choice prevents declaring implementation readiness. The branch/spec directory names are deliberately
independent: Spec Kit's local ignored `feature.json` points to `specs/020-application-lifecycle`.
No extension hooks are registered; pinned integration files remain unchanged.

Local documentation validation passes: seven CI-scope controls, eight skill/reviewer configuration
checks, assurance snapshot integrity for 345 still-unassessed controls and 27 assurance controls.
Packet local links, 17 sequential unchecked tasks, all 19 traceability mappings and whitespace
checks pass. These are documentation checks, not product tests or compliance acceptance.

The packet is published as draft [PR #122](https://github.com/mosesparadise/OpenIdentityAdmin/pull/122).
Its initial source `81fa6e36e518454ffffa24aba773f4b3b2684265` passes documentation-only
[run514](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/41935503974): four required
jobs succeed; sixteen full-scope jobs are intentionally unselected, not passes. Later packet
corrections require their own exact-head check; consult the PR for that result. No full application
CI, SQL experiment, migration, production correction or merge was performed for this packet.

The packet is useful for reviewing the next increment; it does not close M04-A or M04-E.
