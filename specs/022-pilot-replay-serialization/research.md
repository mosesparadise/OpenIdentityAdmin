# Pilot replay correction research

Source inspected: main `e44667c45d103cdc3d70427577f89b77539f6f22`.

## Established defect

`PilotReplayOrderingTests` pauses actual native redemption, permits a competing
replay to revoke existing entries, then allows the winner to insert a valid access
entry. Current `PilotReplayCleanup` repairs that entry only on another replay.
The retained original reproduction and later correction are in feature021; they
prove the bounded mechanism, not complete prevention of late issuance.

## Decision: serialize before lifecycle reads through issuance cleanup

Hold a database-backed pilot gate while native validation, durable consumption and
winning issuance execute. Preserve exactly one successful ordinary concurrent
exchange, as required by feature004 SC-002 and feature006 DST-05. A late status check
alone permits a check/write race. Rejecting both otherwise valid requests would
silently change the existing acceptance contract.

## Decision: separate coordination state from authority state

An initial review suggested holding a no-op update lock on `PilotManifest` in a
separate context. Root review found that `PilotEligibility.UserAsync`,
`PilotEligibility.RegistrationAsync` and `PilotReplayCleanup.IsRevokedPilotCodeAsync`
read that same row from the continuation's context. SQL Server locking READ COMMITTED
can therefore self-block. No repository-supported RCSI guarantee was found; runtime
RCSI state was not measured. This candidate is rejected without claiming an executed
deadlock reproduction.

Use a dedicated singleton `PilotProtocolGate` instead. No foreign keys or native/
eligibility reads touch its row. The gate transaction also owns winning generation.
Both-provider migrations create and seed it; requests never recreate missing state.
This adds no provider switch or advisory-lock API to shared code.

## Decision: retain native consumption outside issuance rollback

Native early redemption intentionally remains durable when subsequent issuance
fails. Keep it in a separate committed phase from generated token writes. The gate
transaction stays live throughout both phases and also owns all generated entries.
Native replay revocation commits in a separate phase before a later response failure
can discard its effect.

Root review also rejected an independently held gate plus an independently committing
issuance transaction: loss of the gate connection can let replay overtake that issuance
after its final status check. Cancellation alone does not fence the later commit.
The selected design binds the gate and generated entries to the **same** database
transaction/context. Losing that transaction also prevents its generation commit.

`ProtocolStorageOwner.BeginChild()` rejects children under an active transaction.
Do not weaken this general invariant. `ProtocolHttpBoundary` assumes owned stores
and private publication proofs, while pilot routing explicitly selects LegacyPilot.
Existing response capture/deadline components are reusable primitives; direct use of
the entire owned boundary needs an explicit design, not a mode-label substitution.
Use a cohesive pilot coordinator and avoid copying the complete owned transport graph.
The pilot remains explicitly unowned. Initial native validation/replay uses its own
short gate transaction; generation reacquires the gate and freshly validates. Only
early redemption runs in a child without reacquiring the generation owner's gate.
That child selects LegacyPilot before manager resolution, is bound to the actual
original continuation and requires fresh Valid status on the same native tracked
row before invoking redemption. It never performs replay cleanup. Otherwise gate
loss could turn the late child into an unfenced revoker of a newer generation.
See the [explicit phases](plan.md#native-phases-and-fencing). This does not change
the active-transaction prohibition for Owned profiles.

## Decision: read fresh under the gate

Native entity caching is disabled, but EF tracking can still retain an older row.
Gate acquisition must precede authoritative native lifecycle reads, or those reads
must occur in a genuinely fresh context after acquisition. Do not clear a tracker
containing pending writes or infer freshness from `DisableEntityCaching()`.

## Evidence limits

This is source/design review. No new SQL experiment, package change, runtime fix,
provider pass, coverage result or production operation is claimed. The current
environment reports enforced network policy, including both requested MCR data
hosts; this alone is not a fresh remote access test. The resumed SDK/package cache
is absent and disk space is low; verification setup must preserve existing evidence.
