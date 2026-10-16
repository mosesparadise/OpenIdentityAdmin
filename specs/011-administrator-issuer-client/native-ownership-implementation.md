# Opt-in native ownership implementation

16 October 2026 (Europe/London). This is the bounded executable continuation of [native store ownership](native-store-ownership.md), from verified main `456a99c362ba3fb397901cfe41e1e086e2036268`. Feature 012 and the earlier four characterization cases remain delivered. This candidate does not complete T005 or B2.

## Implementation boundary

`Infrastructure/Features/ProtocolStorage/` supplies an explicitly owned child service scope and four native OpenIddict EF store adapters. The adapters override the independently reviewed 65 database entries, including 19 deferred sources, and retain all 88 pure native methods. Query filtering, relationship resolution, native manager validation and EF persistence remain native. Broad Prune rejects and faults the owner. Default issuer/BFF composition does not register this feature; test composition opts in explicitly.

The scope starts one monotonic ten-second lifetime before native context acquisition. An explicit transaction phase starts before connection/transaction acquisition with at most five seconds and no more than the original remaining lifetime. Native execution requires the exact wrapped context and transaction, linked cancellation and strict elapsed-time checks before and after completion. Writes require an explicit transaction. Completed tasks cannot bypass deadline equality. Connection behavior comes from the context's selected provider; the existing CeremonyConnection now accepts DbContext so both Platform and Sessions use the same provider contract.

Deferred sources capture their phase synchronously and enlist only when enumerated. Reader lifetime persists through actual native disposal; the exclusive execution slot covers calls, MoveNext and disposal. Native manager relationship getters can therefore run sequentially between yields. Commit excludes outstanding readers and faults, including cancellation swallowed by native TryRedeem/TryRevoke. Unknown or late commit is terminal without retry.

Disposal closes admission synchronously and publishes one asynchronous cleanup task before callbacks. Cancellation uses CancelAsync; cleanup waits for actual native work before reader disposal, transaction rollback/connection restoration and child-scope disposal. The caller waits at most five seconds for cleanup. A timeout retains the entire scope and observes that same task until actual completion; it does not release occupancy or dispose an active context. Reader/callback cleanup errors are reported after remaining cleanup is attempted. Normal native calls remain attached to their actual task; these helper tests do not prove a hard dispatcher response bound.

A transaction epoch ends the owning scope, whether committed or rolled back. This prevents stale EF tracked state after rollback or native bulk updates from entering a later phase. The subsequent redemption/issuance integration must bind fresh scopes to one original request deadline; separate fresh scope factories alone do not establish that multi-scope property. That integration is still open.

## Verification contract

| Requirement/control | Permanent controls | Evidence boundary |
| --- | --- | --- |
| FR-005–011 / SC-002, SC-003, SC-005; original time and terminal faults | StorageOwnershipTests | Strict completed-task expiry, request-age-nine phase budget, wrong context, missing transaction, caller cancellation and rejected maintenance |
| Deferred native ownership | StorageReaderTests, StorageInventoryTests | Sequential getters, source epochs, overlap, cleanup errors; exact 153-method API partition with 65 true overrides and 19 deferred methods; missing/hidden override mutants rejected |
| Cleanup containment | StorageQuarantineTests, StorageAdmissionTests | Actual pending task retained across caller timeout, blocking/reentrant callbacks, deterministic admission closure, pre-cancelled acquisition, exactly-once child disposal |
| Explicit activation | StorageRegistrationTests | Default stores remain native; opt-in replaces all four; every inventoried database entry rejects an unactivated owner |
| Both-provider and both-store persistence | NativeOwnershipTests (four explicit cells) | Exact selected context/native-wrapper identity; all four managers; commit and partial-create rollback; URI/resource/scopes/relationship filtering; generic queries; app/auth nested-delete rollback; bulk revoke rollback; per-attempt real SaveChanges cancellation/OCE/token; reader/replaced-transaction/Prune commit rejection |

Local Host and Persistence builds pass with zero warnings/errors. All 552 local unit cases pass: Host 457, Core 25, Architecture 5 and AppHost harness 65, with zero failures/skips. Host includes 27 new ownership cases and all 430 prior cases. All 199 Python controls and the code-quality gate pass without allowance changes. Raw discovery retains all ten existing Persistence cases and adds exactly four provider/store cases: seven Persistence cases per provider, 22 foundation cases per provider including the unchanged AppHost groups. The unit profile becomes 552; coverage remains 48 inputs and 15 mandatory module floors. No native SQL execution was attempted in the storage-constrained local environment. Exact-head CI native results and full acceptance remain pending.

Failed intermediate compilation/formatting attempts, the first focused run's two nullable-signature inventory failures (17/19 passed), and the first Python run's stale total assertion are retained locally. Nullable value-type signatures and independently expected provider totals were corrected; no runtime assertion, timeout or coverage floor was relaxed. Subsequent focused 26/26 and complete 457/457 Host runs pass. Independent architecture/test and security source reviews accept the corrected scope and inventories; final runtime evidence review remains required before delivery.

## Remaining acceptance

T005 remains unchecked. Dispatcher rejection/error/finally ownership, shared original request lifetime across scopes, early durable proof/native redemption, quotas, issuance hooks and actual acceptance linearization remain required. Production activation is unchanged. All six historical findings, 48 human rows and public-release gates remain open. A-007 defers Firefox only through internal B2 and requires restoration before B3 implementation/activation/release.

## Initial CI correction

[PR #60](https://github.com/mosesparadise/OpenIdentityAdmin/pull/60) head `52c6aa860521cfbd203681f01a400f583bca72b1`, run [38881117434](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38881117434), passed all 552 unit cases but failed the strict formatting check on import ordering and two unused imports. Corrected only those imports; the normal build had passed because the stricter IDE0005/import check runs separately. The failed run remains evidence, not acceptance. Full corrected-head verification is required.

The initial run finished with 61 successful jobs; only formatting and its dependent final acceptance gate failed. Both native foundation jobs pass 22/22, including all four new ownership cells. Raw reports retain all prior identities with zero failures/skips/errors. Coverage and the six security mutation jobs pass. These results support the correction review but do not replace corrected-head CI.
