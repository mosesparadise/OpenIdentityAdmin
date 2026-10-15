# Native store ownership prerequisite

15 October 2026. Continues T005 after PR #56's native dependency/transport prerequisite. This packet adds four pure installed-library controls; it introduces no production owner, registration, route or database experiment. T005 and B2 acceptance remain open.

## Executable native behavior

`NativeStoreBoundaryTests` uses the installed OpenIddict 7.7.1 manager and its default additional ordinal filtering, with entity caching disabled as in existing persistence composition. `NativeStoreProbe` substitutes query rows and update cancellation, while retaining the native relationship getters and scoped context wrapper. No database connection is opened; a PostgreSQL-shaped configuration is not provider execution evidence.

- `Given_NativeRelationshipFiltering_When_Enumerating_Then_GettersRunBeforeReaderDisposal` runs for application and authorization identifiers. A controlled query yields exact and case-variant matches. The actual manager invokes the base relationship getter twice before the query enumerator is disposed and returns only the exact identifier. A deliberately invalid simulated whole-enumerator guard throws at the first getter and still disposes the query. Both exact row identity and disposal counts are asserted.
- `Given_CancelledNativeUpdate_When_RedeemingOrRevoking_Then_ManagerReturnsFalseAndCancellationRemains` runs actual `TryRedeemAsync` and `TryRevokeAsync`. The update seam cancels the request and throws its `OperationCanceledException`. The native manager returns false. Assertions outside its catch bind the received and thrown cancellation tokens to the request, prove the exception category, and preserve the outer cancellation check. An unrelated swallowed exception cannot satisfy this evidence.

The pinned source is OpenIddict commit `9146a148b3333570704a0211d037cd318093770a`: token manager relationship filtering at lines 270–275/308–313, cancellation-catching paths at 932–940/1004–1012, and native token-store relationship getters at 329/370. Tests bind installed behavior; line references explain the integration constraint.

## Required implementation shape

The source inventory has 65 native store database entry methods and 19 deferred enumerables across application, authorization, scope and token stores. Preserve native base behavior and manager filtering. Guard every entry, including generic queries, tracked-entity paths, relationship getters/setters and bulk revocation. Reject broad native Prune; approved cleanup remains at most 100 scoped eligible rows.

An ownership adapter needs separate lifetimes:

1. One original bounded request lifetime; a phase change cannot reset its ten-second budget.
2. A transaction phase bounded by the smaller remaining request budget and five-second authority budget, starting before transaction/lock acquisition. Require the exact context and transaction; no request-only fallback for transaction-required work.
3. Deferred enumeration captures its operation/phase epoch synchronously when returned and retains cancellation and ownership through disposal. An ended/replaced epoch cannot be adopted later.
4. Native execution is exclusive while each awaited call, MoveNext or disposal actually executes. A yielded row must permit the sequential native relationship getter demonstrated above. Keep outstanding enumeration ownership distinct from this execution guard; do not disable native filtering to avoid the interaction.
5. Disposal cancels pending work, but a new owner cannot reuse the context until outstanding calls/enumerators drain. Check linked request/caller/deadline/enumerator cancellation and exact ownership before and after native work and before yielding. Completed-task paths must still recheck elapsed time.
6. Record cancellation/fault even when a native manager catches it and returns false. Final acceptance/commit must revalidate the owner; absence of an escaping exception is insufficient.

The native EF transaction helper catches nonfatal BeginTransaction errors and may return null. Application/authorization Delete and native synchronous commit paths therefore require the exact live explicit owner before/after forwarding; never accept autocommit as fallback. Preserve early durable proof/native redemption separately from issuance rollback. Creation, quota reservation, final authority checks and native persistence still need their reviewed commit boundary before response acceptance, without locks across HTTP.

An initial exploratory whole-enumerator guard was rejected during review and remains outside repository source. No production guard from that exploration is activated or accepted.

## Evidence and remaining gates

Local build passes with zero warnings/errors; the four controls pass with zero skips and the code-quality gate passes. The corrected complete Host run passes all 404 unique cases with zero failures/skips/errors; all 192 Python gate controls also pass. Raw TRX counters and the four exact new identities were reconciled. Initial formatting errors were fixed; a local launch missing Microsoft.Testing.Platform was corrected through ignored runtime probing configuration pointing at the existing package cache. A subsequent complete Host run enumerated 404 cases but failed 26 child-host startup cases (exit 134 or missing listener); the generated child-host runtime configurations also needed package-cache probing. These failed setup attempts are retained and are not passing test results. No package, TLS or test deadline changed.

Independent architecture/test and security review required exact returned-row and actual cancellation-exception/token assertions; both are implemented. The full Host inventory increases from 400 to 404, preserving every existing test. Full exact-head applicable CI remains required before delivery of this prerequisite.

Still required for T005: implement the reviewed opt-in adapter, prove real native-manager transaction participation/rollback on PostgreSQL and SQL Server for both Platform and Sessions contexts, and prove actual dispatcher rejection/error/finally/commit paths. These four pure controls do not establish database cancellation, transaction atomicity, issuance acceptance or runtime owner correctness. All six historical findings, 48 human rows and activation/public-release gates remain open; A-007 governs current browser scope only.

## Continuation after provider delivery

16 October 2026 (Europe/London). PR #57 delivered the four installed-manager characterization controls as `38eb41e06d40b6addbe57e3af839f912645e6dea`. PR #58 subsequently delivered [feature 012 provider registration](../012-database-provider-plugins/delivery.md) as `f8539bf5aad826319864550c4113609f2205e2e1`. Those completed controls are prerequisites, not work to repeat. T005 remains open.

The next executable scope is an opt-in owner under `Infrastructure/Features/ProtocolStorage/` and four thin native EF store adapters. Preserve all 65 database entry methods and 19 deferred sources from the pinned inventory, including generic queries, relationship/tracked-navigation paths and bulk revocation. Resolve connection mechanisms from the context's selected `GetDatabaseProvider().Connections`; introduce no engine switch, new driver dependency or replacement provider contract. Both Platform and Sessions contexts must use the same reusable ownership mechanism, while their selected provider/store boundaries remain intact. Default issuer/BFF composition remains dormant; activation is isolated to test composition until the complete B2 path is accepted.

Implement one monotonic ten-second request deadline. Start each explicit transaction phase before connection, transaction and lock acquisition; its budget is the smaller remaining request lifetime and five seconds. Bind the exact wrapped context and `CurrentTransaction`, require a live explicit transaction for writes, and reject replaced ownership before/after native work. Deferred sources synchronously capture the phase epoch; retain enumerator ownership through disposal while excluding overlapping native execution only during calls, `MoveNext` and disposal. Sequential native relationship getters between yields must continue to work. On cancellation, cancel then drain before reuse; latch faults and poison an undrained context. Recheck original time/tokens, exact context/transaction and outstanding work before commit, even when a manager returned false after swallowing cancellation. Unknown commit is failure without retry. Reject broad native Prune; scoped bounded cleanup remains separate B2 work.

Required new evidence: deadline equality and completed-task expiry; a phase begun at request age nine seconds retains at most one second; wrong context/replaced transaction; stale deferred sources, overlapping execution and failed disposal/drain; real installed-manager getter ordering through the adapter; and both-provider, both-store manager commit/rollback. Prove native application/authorization Delete joins the explicit transaction despite the helper catching a nested BeginTransaction failure. Prove caught TryRedeem/TryRevoke cancellation prevents owner commit. An inventory control must detect an unguarded native override. These are planned tests, not executed results.

This bounded prerequisite does not finish T005. Dispatcher rejection/error/finally ownership, early durable proof/redemption, quota reservation, issuance commit hooks and actual linearization remain required before B2 acceptance. No helper-only B2 completion, production activation, historical finding closure or Firefox acceptance is implied.
