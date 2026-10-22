# Shared Sessions transaction participation

22 October 2026. Continuation of [native identity/state work](native-integration.md) in [PR #71](https://github.com/mosesparadise/OpenIdentityAdmin/pull/71), under [A-009](../../docs/amendments/A-009-roadmap-browser-deferral.md). This implements the ticket participation prerequisite within T003. Complete B3 integration, generation/intent persistence, actor-bound audit, issuer completion and logout remain open.

**Later exact-source verification:** [prerequisite evidence](prerequisite-delivery.md) records both-provider execution and the current delivery status. The implementation-time pending statements below retain their original scope.

## Implementation

`SessionTicketTransaction` binds one existing `AdminSessionDbContext` and its exact current native transaction. It opens no transaction or service scope and owns no disposal. It stages the existing B1 create/renew/revoke writes and their audit records, preserving CAS predicates, post-write expiry checks and idempotent absent revocation. `SessionTicketPersistence` retains the existing B1 begin/dispose boundary and delegates those operations to the participant. Cleanup is unchanged; there are no schema or migration changes.

The owner can perform additional Sessions writes before calling the participant's guarded commit. Immutable original lifetime snapshots are checked again immediately before the one native commit attempt. A false stage, exception, cancellation, rejected overlap, lost commit acknowledgement or completed commit cannot be retried through that participant. Atomic Ready/Staging/Closed transitions ensure an in-flight write cannot revive a transaction closed by a competing call. Exact current-transaction identity is checked at entry and after staged asynchronous work.

The caller still owns rollback/disposal and must abandon its transaction after any independent owner-work failure. The helper cannot observe an exception in arbitrary caller code or prevent the owner from directly calling the native transaction. Future B3 orchestration must use this guarded commit and maintain that rollback obligation. Existing B1 wrappers continue publishing a handle only after commit and disposal return successfully. B1 audit records remain minimal mutation intent, without a claim of authenticated B3 actor provenance.

B2 native client-state creation/redemption retains its separately owned durable consumption boundary. The new fixture's directly inserted native token row is an independent mapped-state marker proving shared transaction participation; it is not a native manager redemption or a completed product callback.

## Tests and review

`SessionTransactionTests` adds three discovered cases per provider under the existing mandatory `BffSessions` group:

| Test | Invariants |
| --- | --- |
| `Given_SharedSessionTransaction_When_OwnerCompletesOrFails_Then_AllRowsFollowItsOutcome` | Protected ticket, audit and native marker commit together; owner disposal and a later real constraint failure undo all three; absent revocation does not poison other work. |
| `Given_StagedTicket_When_StageOrFinalAdmissionFails_Then_CommitIsPermanentlyDenied` | Caught post-insert audit failure, missed renewal CAS, cancellation and expiry equality after staging deny commit; modifying the caller's row cannot extend captured deadlines. |
| `Given_TransactionOwnership_When_ReplacedOrCommitResponseLost_Then_NoSecondCommitIsAllowed` | Wrong context, disposed/replaced transaction, competing stage/commit while audit is held, and lost native commit response cannot permit reuse; independent reads distinguish rollback from a committed but uncertain result. |

All use the real Aspire topology and Sessions-only runtime principal, actual provider commands, protected B1 material and fresh-context durability assertions. Existing B1 failure/concurrency controls remain mandatory. Native discovery and exact selector reconciliation pass for both providers: Foundation remains 8, BffSessions increases 7→10, and AppHost totals 15→18. No deadline, coverage floor, collector input or security assertion was relaxed. This maps to FR-005/008/011/013 and prerequisite evidence for B3-STATE-01–03; it does not complete the browser-generation races.

Local AppHost and Host builds pass with zero warnings/errors; all 65 AppHost unit controls also pass with zero skips. The rebuilt Host suite passes 1,192/1,192 with zero skips; TRX SHA-256: `0eb6e2f225af2506a702cdb36c61bedd3203454ef14d30c8eed08dbb8540ff7c`. All 211 Python controls pass after updating independent inventory expectations for the six added cases. Host/AppHost IDE0005 style checks and code-quality checks pass. Actual provider execution is pending hosted CI; build/discovery is not execution evidence.

Independent architecture/test and security source reviews found an initial overlap-state defect. Root corrected it with monotonic atomic transitions and added both held-audit interleavings. Review now passes the production boundary, conditional on native provider execution. Test cleanup preserves the first failure while always releasing and joining the held operation. Exact EF Core 10.0.12 `ReaderModificationCommandBatch.ExecuteAsync` source review corrected the audit-fault assertion to require its native `DbUpdateException` wrapper and `InvalidOperationException` inner; the single observed fault, permanent commit denial and rollback oracles are unchanged.

## Preserved CI failures and correction

Initial source `8511df7858a2f8f4fdb47f73165a643b1e9dded0`, run [39860041729](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/39860041729), failed static analysis on one unused test import and Contracts core on new/changed viewer advisories. The import was removed and the actual IDE0005 check passes locally.

An independent [exact-byte/configuration advisory review](../../eng/contracts/vendor/configuration-review-2026-10-22.json) records three newly observed DOMPurify advisories and their absent required sanitizer modes. The assessed registry response/hash is refreshed while strict unknown/changed-advisory detection, unresolved vendor inventory/qs findings and activation/public-release blockers remain unchanged. The full local supply-chain gate passes. The failed hosted HTTP response was not archived; the fresh response reproduces rejection but cannot establish the exact historical response bytes.

All six historical findings, 48 human rows and 345 not-assessed controls remain open. PR #69 is preserved draft/unmerged. No production activation, SQL diagnostic experiment or public-release claim follows from this implementation.

[Browser provisioning correction](browser-provisioning.md) preserves the two failed attempts and the official Ubuntu archive selection needed for complete hosted evidence. No product assertion or deadline changes.
