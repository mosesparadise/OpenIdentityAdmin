# Planned ticket and audit model

Design only. Use additive migrations in both existing provider assemblies; preserve native OpenIddict state tables.

## AdminSessionTicket

| Field | Contract |
| --- | --- |
| Handle digest | SHA-256 of a store-generated 32-byte random handle; canonical uppercase 64-character hexadecimal lookup key. Never persist full handle. Canonically validate the 43-character unpadded base64url handle before lookup. |
| Subject/source-session binding | Nonempty opaque identifiers, each at most 400 characters, compared ordinally. Authentication binding, not permission authority. |
| Protected payload | Native authentication ticket inside a versioned authenticated envelope; native serialization at most 64 KiB, protected envelope at most 96 KiB. Reject excess/unsupported version before deserialization. |
| CreatedAt / LastActivityAt | UTC instants; immutable creation and monotonic activity. Reject clock values earlier than either. |
| AbsoluteExpiresAt | Immutable minimum of creation + requested lifetime (at most eight hours) and source administrator authentication-session expiry; strictly after creation. Access-token expiry is not this upstream ceiling. |
| IdleLifetime | Immutable effective duration selected at creation, strictly positive and at most 30 minutes; included in the authenticated envelope so renewal cannot widen a shorter policy |
| IdleExpiresAt | Minimum of activity + requested idle lifetime (at most 30 minutes), absolute deadline and upstream expiry; equality means expired |
| Revision | Positive portable 64-bit counter, initially one; CAS renewal increments without wraparound |
| RevokedAt | Nullable UTC instant, never cleared once set; load excludes revoked rows |

Authenticate the digest, bindings, creation/activity/deadlines, immutable idle duration and revision inside the envelope and compare against row metadata after decryption. Renewal uses that retained duration, never a current default. Validate the native scheme, single identity and exact `sub`/`sid` plus store-owned lifetime properties under the [contract](contracts/ticket-store.md#native-ticket-binding-and-time-ownership). Indexing metadata cannot substitute for protected contents. Revocation is an additional database denial; no envelope overrides it. Purpose/version changes require explicit compatibility behavior.

Index expiry/revocation for cleanup and subject/source bindings for later bounded revocation. Preserve UTC precision needed by boundary tests and canonical digest equality on both providers; SQL Server's default collation is not ordinal protocol semantics.

## AdminSessionAudit

Commit minimal create/renew/revoke intent in the same transaction as each successful transition: opaque event ID, independently generated operation correlation ID, UTC time, finite operation/outcome and target ticket digest/revision. No tokens, full handles, serialized claims, keys, request URLs or connection data. A failed audit write rolls back state. Repeated idempotent revocation does not fabricate another transition.

This maps to PRD FR-12's mutation intent, target and correlation subset. Full authenticated actor attribution and external delivery remain integration gaps: a ticket's subject is not necessarily the actor revoking it, and this store does not authenticate callers. B3/C/D must supply trustworthy actor context at their actual authorization boundary before activation; do not invent an actor from untrusted input or claim FR-12 complete.

Digest correlation remains internal and is not emitted in public logs. This supplies durable intent, not external audit delivery. Do not purge audit rows in this slice or claim an approved legal retention policy; retention/export need review before activation. Ticket cleanup cannot cascade-delete audit intent.

## State transitions

- **Create**: validate bounds, generate handle, protect native ticket/envelope, atomically insert ticket/intent, return only after commit. No caller-selected handle or automatic retry after unknown outcome.
- **Load**: bounded lookup; reject unknown/revoked/expired/rollback state; decrypt and compare all bindings; return state/revision without renewal. Operational failure never returns usable state.
- **Renew**: preserve handle/bindings/creation/absolute deadline. Validate live state and expected revision, re-protect changes, atomically update/append intent. No upsert, recreation or silent conflict retry.
- **Revoke**: atomically deny the current row and append intent, idempotently, without requiring a stale caller revision. Prove both renewal/revocation interleavings on both databases.
- **Cleanup**: delete at most 100 rows revoked or expired at captured UTC time, rechecking eligibility in the deletion. Do not delete a renewed live row from a stale selection. Retain audit intent.

A load begun before revocation commits may already have returned data. Later request/eligibility/token boundaries must handle this; store tests do not prove five-second API enforcement.

## Recovery and compatibility

Migration is additive; old health-only BFF code does not use new rows. Rollback stops the consumer without deleting tables/credentials/intent. Restoring an older snapshot may revive revoked rows: require invalidating affected BFF sessions and upstream grants before activation, with scope recorded. Key rotation/restore retains decryptors for live state; missing decryptors fail closed.
