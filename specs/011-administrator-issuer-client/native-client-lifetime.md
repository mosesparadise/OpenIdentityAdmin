# Native callback expiry and rejection evidence

20 October 2026. Continues the authorized feature 011 T005–T009 batch and its coherent B2 dependencies. FR-005–011/013; SC-002/003/005/006. Production composition remains dormant. PR #66 remains draft/unmerged; none of this record closes B2 or an external gate.

## Preserved hosted checkpoint

Source `efcd61f5a2829138277afee3eb49f25e1a7289df`, tree `e6a319fd4debcf55b562a3313deda55717f73d76`, completed [run 39103777408](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/39103777408): 60 jobs succeeded, four failed, and aggregate coverage was skipped. Both ProtocolAdmission jobs executed 54 cases: 51 passed, three failed, zero skipped. The three native-client cases now get past the previously corrected callback receipt exception, but their first positive callback returns HTTP 400 without a captured exception. No later HTTPS fault, awaited mutation, replay or acceptance assertion in those cases receives credit. Native rejection status alone does not establish which validation failed.

SQL Server foundation also failed its native-state creation/quota case; the subsequent AppHost foundation invocation did not execute. Its cleanup could replace an earlier observer/orchestration exception with a cancellation from joining a competitor. The original failure cause remains unproved. Neither a retry nor a larger deadline is a correction for that evidence gap.

## Strict expiry with durable consumption

The previous client row lookup returned no bound when the row or protected expiry was reached. That denied the callback but left its state Valid. Native lifetime validation also denied sufficiently old state before durable consumption. The contract requires cryptographically valid, correctly bound expired callback state to be consumed and then denied.

`AdministratorNativeToken.StateExpirationAsync` now returns the earlier finite persisted/protected deadline after fresh type/application/authorization/subject binding checks, even when expired. The server artifact API retains its strict current-time filter. `DurableClientRedemption` uses the client-specific bound, calls actual native status validation/redemption under the owning transaction, commits consumption, and rejects expiry at equality before commit and again after child cleanup. A rejected callback receives no private consumed-state receipt.

`NativeClientStateExpiration` preserves the native lifetime descriptor's filters/order and delegates every token except the fixed registration's exact state type. Native signature, issuer and token-type validation still precede it. Only that state defers lifetime denial to durable consumption; this does not extend accepted lifetime. Four Host controls exercise the actual native expiration handler, and existing provider consumption controls now distinguish expired consumed client state from unchanged unconsumed server artifacts.

Six new provider scenarios require a successful native baseline first, then exercise exact expiry, expiry beyond native clock skew, and simultaneous callback delivery from independent service roots. Expiry uses the independently persisted deadline and fresh committed-status reads. Concurrency observes an actual database peer blocked by the winning session before release, with exact token-send and final-state assertions. No fabricated provider command or in-memory substitute counts as contention evidence.

## Failure evidence without protocol data

A test-only native `ProcessErrorContext` observer records only exact allowlisted error categories/documentation identifiers. Unknown values and URI suffixes become fixed `unknown` values; descriptions, arbitrary URLs, token/state/code values and cookies are never included. The observer does not change native rejection or response handling. Positive baseline assertions report only HTTP status and this finite category. A failure arising only in later `AuthenticateAsync` can still report `none`; the observer targets the currently failing HTTP 400 path.

Both race helpers now preserve the first orchestration exception while releasing the winner, canceling/joining the observer and joining both real operations. Cleanup failures remain observed and cannot replace the original failure. Permanent Host controls require original exception identity and prove a pending peer is joined even when another operation has already failed. No retry, observation deadline or request budget changes.

## Verification and limits

The earlier expiry-only local source passed 1,087 Host cases with zero skips. Final local execution passes **1,094/1,094 Host cases**, zero skips, including four expiry controls and seven sanitization/causal-cleanup controls. Administrator and Persistence builds pass, and all **206 Python checks** pass. Actual Administrator discovery preserves all **1,129** prior identities/types/traits and adds six cases: **1,135 total**, ordinary PostgreSQL **502**, SQL Server **607**, ProtocolAdmission **57 each**, aggregate selection **859 / 964**. Inventories require the additions without removing existing cases.

Independent security review passes the narrow expiry source correction; architecture review passes causal cleanup and the nonintervening finite diagnostic. The resumed environment had lost its temporary SDK/package cache; the exact pinned SDK archive was hash-verified and dependencies restored in locked mode. An intermediate Host run passed 1,093/1,094: moving its assembly outside the repository caused the production-route test's repository lookup to fail. Keeping that assembly inside the repository while linking regenerable runtime dependencies corrected the environment; the complete final 1,094-case run above passed. The failed report remains retained and is not described as a product defect or clean pass. No local database/Docker experiment ran.

The five independently reviewed artifacts from the retained hosted checkpoint contain **1,317 executions: 1,310 pass, seven fail, zero skips**; all 1,311 earlier subset identities/traits remain. The unit artifact passes 1,178/1,178 including Host 1,083. PostgreSQL foundation passes 23/23; SQL Persistence executes eight with seven passes and one failure, leaving the subsequent 15 AppHost cases unexecuted. Source and tested merge `2c1b01a3442ce1c949547faf51b371e5d74f1ad4` share the recorded tree. Provider acceptance for the new source remains pending and cannot be inferred from source review or older green runs.

Preserve all prior test identities, coverage inputs/floors and mutation suites. The remaining native-client negative, outage/key/restart, operations and B2 mutation obligations remain tracked in [tasks](tasks.md) and [previous integration evidence](native-client-https.md). All six historical findings, 48 human rows and activation/public-release blockers remain open. Firefox remains deferred only through internal B2 under A-007 and must return before B3 implementation/activation/release.
