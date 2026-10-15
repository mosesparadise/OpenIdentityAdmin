# Internal BFF session ticket store

Feature 010 supplies an internal store for later M02-B integration. Default BFF
composition still exposes its existing operational surface only. No login,
callback, cookie, refresh, proxy, management API or React access is enabled.
A store read is not authentication or current administrator authorization.

## Storage and upgrade

Run the existing one-shot Migrator with the selected PostgreSQL or SQL Server
provider before starting a consumer. Both additive `ProtectedSessionTickets`
migrations retain native OpenIddict client-state tables and add ticket and
independent audit tables in `bff`. Readiness requires the new migration identity.
Never migrate from web startup. Reapply the existing session-runtime grants
through the approved provisioning lifecycle; the BFF account needs only the
session schema and cannot acquire platform business-write privileges.

The database contains a SHA-256 digest of each random 32-byte reference, bounded
opaque subject/source-session metadata, full-precision UTC ticks, a positive
revision and an encrypted native authentication-ticket envelope. Do not treat
metadata as public telemetry. The full reference, native claims, tokens and
protected bytes must not appear in logs or diagnostics.

## Separate protection ownership

The internal composition helper accepts an explicitly verified `SessionProtection`
owner. Configuration uses `Bff:Sessions:Protection` with:

- `ApplicationId`: stable deployment identifier, 1–100 ASCII letters, digits,
  dot, underscore or hyphen. All BFF instances sharing tickets use the same ID.
- `KeyRingPath`: absolute, existing private Linux directory (0700), with trusted
  non-symlink ancestors. Provision it separately from issuer keys.
- `Current:Path` and `Current:Password`: private password-encrypted PKCS#12
  wrapper material, one usable RSA private key of at least 3072 bits and key
  encipherment usage. Active material must be within its validity period.
- Optional contiguous `Previous:0` through `Previous:3` entries with the same
  path/password shape. Retained expired wrappers may decrypt historical keys.

Supply secrets through the environment's approved secret facility. No literal
password example belongs in configuration, command history or evidence. Private
credential files require 0400/0600 and a private parent. Unknown fields and
children under scalar fields are rejected. There is no ephemeral fallback.

The BFF application discriminator and ticket purpose are code-owned and separate
from the issuer. The owner contains its own Data Protection service provider and
wrappers. Stop/dispose store consumers first, then dispose protection; its native
provider is disposed before wrapper keys. Startup checks durable writes, native
encrypted format, active wrapping and every retained decryptor. Framework
protection diagnostics are suppressed rather than risking key/path disclosure.

## Lifetime and concurrency

Creation requires the source **administrator authentication-session** expiry,
not an access-token deadline. Idle duration is strictly positive and at most
30 minutes; absolute duration is at most eight hours and is capped by that
source deadline. A shorter idle duration is authenticated and immutable.
Loads never renew. Equality with either deadline and clock rollback deny access.
Native `AuthenticationProperties` use RFC1123 whole seconds; their conservative
projection may expire less than one second before the full-tick store deadline.
No truncation extends eligibility.

Renewal requires the expected revision and matching native identity. The store
never upserts or retries mutations. It checks original/replacement eligibility
after acquiring write ownership and again after audit insertion, before commit.
Revocation needs no caller revision and permanently prevents renewal. A load
already returned before revocation is not recalled; later live authorization
boundaries remain required.

Create, renew and revoke commit minimal intent in the same transaction. Failed
audit insertion rolls back the state change. An unknown commit result is a safe
failure, never an automatic retry or a successful reference. Reauthentication
and authorized reconciliation may be needed; do not infer rollback from a lost
acknowledgement.

## Cleanup, rollback and recovery

Each explicit cleanup invocation removes at most 100 revoked/expired ticket
rows and rechecks eligibility when deleting. It never deletes audit intent.
Scheduling, actor attribution, export and audit retention approval remain later
integration work; no retention purge or complete PRD FR-12 claim is supplied.
Monitor growth using aggregate operational counts without exposing identifiers.

Rollback stops the consumer and retains additive tables, audit and keys. Do not
drop a key or historical wrapping decryptor while retained live state needs it.
Test rotation with a new wrapper and retained old decryptors before retirement.
Loss of the ring prevents decryption of existing tickets; a newly provisioned
ring cannot recover them.

Restoring an older database snapshot can revive rows revoked after the snapshot.
Before any activation, invalidate affected BFF sessions and upstream grants,
record the restoration scope, and validate matching key/decryptor recovery.
Local ticket revocation does not revoke upstream OAuth credentials. Remote
logout, refreshed-token replay policy and current administrator eligibility
remain explicit M02-B/C/D dependencies.

Safe troubleshooting reports only fixed failure categories, provider, operation,
source revision and sanitized evidence identities. Retain private raw evidence
under controlled access. Do not publish exceptions, connection strings, tokens,
handles, payloads or key files. Preserve the existing 30-second command bound;
failures do not justify a retry policy, weaker checks or a deadline increase.
