# Managed application lifecycle data model

This is a proposed additive model, not a migration already applied.

## ApplicationPolicy

Add a non-null boolean `Enabled`, default true for existing managed rows and newly created
registrations. Preserve all IDs, native records, revisions, credential hashes/times and prior
migration files/history. Native-only/system registrations are not adopted by migration.

Keep the existing positive aggregate/credential revision constraints. Disable and re-enable
increment `Revision` exactly once and preserve `CredentialRevision`, issued/expiry times and
native configuration. Existing edit/replacement must explicitly preserve Enabled. Include
Enabled in frozen write snapshots, persisted verification, managed admission comparisons and
conditional observation predicates. Management reads must still return disabled rows safely.

| Source | Action | Result |
| --- | --- | --- |
| Enabled, matching version | Disable | Disabled; Revision +1; one accepted audit plus reason |
| Disabled, matching version | Re-enable | Enabled; Revision +1; one accepted audit plus reason |
| Either, stale version | Either action | 412; no state/version/audit changes |
| Already requested state, matching version | Same-state action | 409; no state/version/audit changes |
| Unsupported/system target | Either action | Existing protected/unsupported rejection; no adoption |

Re-enable does not depend on credential freshness for the management write, but issuer checks
continue to reject an expired credential. Expiry is visible to the operator.

## Application lifecycle audit detail

Add an application-owned `ApplicationLifecycleAuditDetail` table with:
- `AuditId`: primary key and foreign key to the accepted SecurityAuditIntent.
- `ReasonCode`: required integer with a database constraint allowing only 1 (maintenance),
  2 (security) or 3 (configuration), represented by feature-owned named enum values. The public
  JSON reason remains the exact lowercase string defined in the contract. Integer storage avoids
  provider collation and SQL Server trailing-space equality widening the allowed reason set.
  Reject unknown numeric values at the database boundary and reject case/whitespace aliases at
  input on both providers; verify independently specified persisted code/action pairs.

Actions are `ApplicationDisabled` and `ApplicationEnabled` on the existing audit intent;
actor, target, UTC time and correlation stay in their existing columns. No free-text reason,
credential, token, callback or browser input is copied into audit/log output. The detail shares
the same transaction and retention lifetime as its audit. Current ceremony cleanup retains audit
evidence; no audit-retention worker is introduced here. A separately authorized future audit
deletion must cascade to its detail. Ordinary API/BFF details never expose private audit rows.
Verify both rows with a fresh read before commit.

## Migration and recovery

Generate separate additive Platform migrations and snapshots for PostgreSQL and SQL Server.
Prove a populated feature019 upgrade, reapplication/idempotent migrator behavior and fresh install.
Retain the frozen historical schema witness; add a new witness for this schema rather than
rewriting old evidence.

After any disable commits, rolling back to an old binary that ignores Enabled can restore issuance.
Operational rollback must therefore stop/quiesce issuer traffic before an incompatible downgrade;
traffic must remain blocked until compatible lifecycle enforcement is restored or a separately
approved recovery resolves the disabled state. Stopping traffic only during downgrade is insufficient.
Do not silently drop state or advertise a safe online downgrade. Test the additive upgrade and
document backup/restore and mixed-version deployment ordering. Deploy lifecycle writes only after
every issuer instance enforces Enabled; a mixed fleet containing old issuers cannot satisfy the
five-second contract.

The new details field also requires a coordinated API/BFF contract switch. Existing
`ApplicationJson.Options` rejects unmapped JSON members and the BFF uses it for details parsing;
an old BFF cannot be assumed to accept a new API's `enabled` field. Quiesce management traffic
while switching API, BFF and their generated UI assets to the matching contract, then probe the
actual details/mutation paths before reopening traffic. An online mixed-version rollout is out
of scope unless an explicit compatibility bridge is implemented and independently tested.
