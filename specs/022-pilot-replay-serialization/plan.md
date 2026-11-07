# Implementation plan: Pilot replay serialization

## Baseline and constitution

Continue from main `e44667c45d103cdc3d70427577f89b77539f6f22`, the PR124 squash.
Post-merge runs [42326203442](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/42326203442)
and [42329191762](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/42329191762)
report success. These statuses are not new feature022 test evidence.

Use the existing .NET10, EF relational provider interfaces and pinned OpenIddict7.7.2.
No dependency upgrade, refresh activation, frontend contract or client-profile change
is included. Existing approved scope covers the correction; no new policy exception
is needed. Native managers own protocol writes/crypto. Both providers, exact migration
histories, original deadlines and all15 coverage floors remain mandatory.

## Implementation boundary

1. Add the isolated coordination row described in [data-model](data-model.md), through
   the existing provider-neutral model and new migrations in both provider projects.
2. Introduce a feature-local pilot coordinator under
   `src/backend/IdentityPlatform.AuthServer/Features/Protocol/PilotReplay/`. It owns
   one original request lifetime, retained gate/issuance scope/transaction and
   native invocation through actual cleanup. Use the existing provider connection
   timeout/restoration policy; no process-local semaphore or new provider switch.
3. Classify with native endpoint semantics. Exact pilot selection is routing only;
   native authentication/PKCE and persisted manifest/profile binding remain required.
   Standalone and mixed hosts must enter the same gate before authoritative lifecycle
   reads. No token-producing alternate route may bypass the gate. Malformed and other
   profiles must preserve their existing native behavior and ownership boundaries.
4. Use the explicit phases below. All generated native entries use the **same context
   and transaction that holds the issuance gate**, so losing that transaction cannot
   permit an independent late generation commit. Pilot remains unowned; do not relax
   `ProtocolStorageOwner.BeginChild()` for existing Owned profiles.
5. Retain the native response with the existing bounded capture/feature/deadline
   primitives. Require actual native completion, unchanged bound identities, confirmed
   issuance commit and resource cleanup before publishing success. Replay errors
   retain native protocol shape after durable cleanup. Capture/commit/disposal failure
   produces no successful credential output. Drain write-capable native work before
   intentional commit/rollback; after involuntary loss retain/drain scopes without
   claiming the database lock remains held.
6. The database gate releases at the generation transaction's commit/rollback/loss;
   retain its scope and drain all actual work through cleanup before publication.
   On loss, suppress output. The old continuation must not start a replacement
   transaction, reconnect and autocommit, or write through another context. A new
   contender may enter after the old transaction ends, but no old generation can
   commit afterward. Verify transaction identity at actual native write boundaries,
   not just a last status check. Do not
   automatically retry, replace the request budget or repair coordination state.

The exact coordinator integration is an implementation review obligation. Reuse
stable primitives and extract only genuinely shared transport behavior; do not copy
the full administrator ownership system or grant pilot data administrator authority.

## Native phases and fencing

1. Initial native token-entry validation and repeated-replay cleanup use a fresh,
   short transaction holding the dedicated gate. Freshly bind the exact protected
   principal, native rows and pilot manifest. Native replay side effects commit in
   that transaction before returning native acceptance/rejection to its caller.
2. Before ProcessSignIn generation, acquire the gate in the same fresh context and
   transaction that will own **all** generated entries. Revalidate current native
   lifecycle and application/session authority under the gate. If already consumed,
   commit native replay cleanup in this transaction and stop; no child replay writer
   may outlive this transaction's ownership.
3. For a still-valid code only, synchronously invoke a separately retained LegacyPilot
   consumption scope/short transaction bound to the exact original request, native
   context and coordinator. Select store mode before manager resolution. This child
   does not reacquire the gate and performs **only** native early redemption, after
   requiring fresh Valid status and exact bindings on the same tracked native row.
   It does not run replay cleanup. Preserve native optimistic concurrency; bulk
   revocation need not update the native concurrency token, so prove the actual
   no-usability-resurrection invariant under that interleaving. Never treat TryRedeem
   alone as proof the code was still Valid. Commit consumption before
   returning, retaining and joining the child on every error path.
4. The gate/generation owner reloads the exact consumed code and verifies its bound
   consumption result, current authority and original expiration before generating.
   It never reconnects into a replacement generation transaction. Native generation
   commit and gate release are one database operation. If gate loss overlaps child
   consumption, that child can only consume or fail concurrency; it cannot become
   an unfenced late revoker. The faulted owner produces no successful credentials.

Verify descriptor ordering, filters, actual pinned7.7.2 manager behavior and scope/
transaction identity before implementation. Retain native code/proof validation and
error translation; do not manufacture protocol responses or re-run authentication in
a way that turns a consumed code back into an accepted demand.

## Verification design

The permanent [acceptance matrix](contracts/exchange.md) maps every PRS requirement
to the actual native/HTTP/database/process boundary. Preserve current final-replay,
cleanup-failure and12 binding-negative assertions as separate regressions. The old
ordering that deliberately permits replay to finish while holding the winner must
be replaced with a contention observer; merely waiting for the replay would deadlock
the corrected test. Race actual replay completion against positively observed native
database contention. On old source, completed replay precedes winner release and
the no-late-issuance oracle fails. On corrected source, actual contention precedes
release; both branches assert the same final safety oracle before another replay.
Timeout never substitutes for contention. For the separate old cleanup-write-failure
case, establish a valid linked access entry and revoked code through an explicit
native fixture, not the now-prohibited HTTP ordering; retain genuine HTTP cleanup
and unchanged-state assertions. Do not remove its final-replay cleanup coverage.

Add direct post-wait freshness tests for independently committed account/session/
application/scope/authorization restrictions and original code expiry. Restore
reversible eligibility and require a fresh authorized flow; never renew the old code.

Capture the complete baseline regression source and SHA256 before the first run.
Demonstrate a stable failing safety oracle on the old implementation, then require
the corrected trace. A test-owned event or an uncompleted Task does not prove a
database lock wait: observe the actual blocked command/connection through existing
provider test helpers. Keep all calls bounded and join both requests on failure.

Run focused provider cells serially in this workspace. Full final-head CI and source/
artifact reconciliation follow the integrated correction. No numeric inventory is
invented before native discovery. New required reports/partitions must be included
in current-run coverage manifests without reducing existing floors or cases.

## Operational and review boundaries

No UI behavior changes, but retained browser suites and both native Aspire demos
remain regression gates for the affected host. Actual Windows, manual accessibility,
Firefox/A-009, vendor, six historical findings and public-release gates stay open.
Review architecture/security, test/evidence, provider migrations and Aspire startup/
teardown independently. No defect closure or runtime acceptance follows from this
plan's delivery. Implementation starts with T002's reproducible regression.
