# Internal administrator native client operation

This runbook covers feature 011's dormant B2 issuer/client composition. It does not activate production routes, establish a B1 browser ticket or authorize a deployment. The coherent B2 acceptance packet remains incomplete. Firefox is deferred only through internal B2 under A-007; restore it before B3 implementation, activation or release. All historical findings and human/public-release gates remain open.

## Initialization and upgrade

Preserve existing Platform and Sessions databases, provider registrations, migration assemblies and migration-history tables. Use the approved one-shot Migrator lifecycle with the selected relational EF Core provider; do not recreate databases or rewrite historical migrations. Apply the additive Platform protocol and Sessions client-state schema before using the corresponding runtime composition. Follow [database provider integration](../../specs/012-database-provider-plugins/quickstart.md), [issuer protection](issuer-protection.md) and [B1 session-store operation](bff-session-store.md).

Initialize the single fixed administrator application through [the explicit native registration command](administrator-protocol-initialization.md). Its public assertion key and exact callback must match the client's profile. An exact repeat verifies the stored application/scope/manifest; a partial, disabled or drifted registration fails. Initialization is neither repair nor key rotation. Retain the operation ID and finite result. If commit acknowledgement is lost, inspect committed state independently before an intentional verification invocation; do not automatically repeat a security mutation.

The AuthServer uses its approved Platform runtime identity. The native client uses only `AdminSessionDbContext` with the Sessions runtime identity and the provider's existing schema/readiness contract. The client receives no Platform database credentials or grants. Migration/role-provisioning credentials never belong in either runtime identity. Test Sessions provisioning reuses the graph's Sessions identity without changing Platform credentials.

## Credentials and fixed profile

The internal composition accepts an explicit `NativeClientProfile` and externally owned `NativeClientCredentials`. No production Program currently calls `AddAdministratorClient`; there is no supported environment-variable switch that enables this boundary. The caller selects the configuration section passed to `NativeClientCredentials.Load`. Its relative schema is:

| Role | Required settings | Native use |
| --- | --- | --- |
| `StateSigning` | `Path`, `Password`, optional indexed `Previous:0` through `Previous:3` entries with their own `Path`/`Password` | Sign new state with the active key; validate retained state |
| `StateEncryption` | Same schema | Encrypt new state with the active key; decrypt retained state |
| `AssertionSigning` | `Path`, `Password`; no `Previous` collection | Sign the single registered client's PAR/token assertions |

Supply encrypted PKCS#12 files using the existing protected-file/directory permissions and secret-injection mechanism. Each role needs a usable RSA private key of at least 3,072 bits and its declared digital-signature or key-encipherment usage. Active credentials must be current and cover the artifact's lifetime. Unknown fields, invalid files, ambiguous settings, repeated key material and unusable roles fail with a finite credential category; there is no ephemeral fallback.

Keep these three roles distinct from issuer signing/encryption/wrapping and B1 protection material. Compare public-key fingerprints across those independently configured applications during configuration review; separate file paths, certificate objects or passwords do not prove separate keys. The client loader enforces separation within its own loaded roles, not across an independently configured issuer or B1 host. Do not retain private keys, passwords, protected files or connection strings in evidence.

Use one canonical HTTPS issuer, one canonical HTTPS client identity, one exact same-origin callback and one explicit public assertion-key ID. Discovery must match the fixed authorize/token/PAR/JWKS destinations and advertise code/query, S256, RS256 and private_key_jwt for both backchannels. Trust validation remains enabled. No shared secret, UserInfo, refresh/offline scope or alternate grant is configured. The callback correlation cookie is distinct from B1: `__Host-identity-admin-client-state`, Path `/`, no Domain, Secure, HttpOnly and SameSite=Lax.

Dispose every client service root, request and asynchronous child before disposing its credentials. Every application instance imports its own certificate objects while sharing the approved durable keys and Sessions database. Stop new requests and join in-flight work before releasing databases, providers or credentials.

## Rotation, retention and failure handling

For planned state-key replacement, prepare the new active state signing/encryption keys and retain the previous keys explicitly on every instance that may receive callbacks. New issuance selects the configured active keys even when a retained certificate expires later. Old keys must remain available until outstanding state has expired and can no longer be accepted. The state acceptance bound is five minutes; do not derive a fleet-wide deletion time from one instance's clock. Verify configuration, pending-state inventory and all instance transitions before removing retained keys. Retained signing-validation/decryption support does not constitute a production rotation command or a tested fleet rollout.

Assertion-key replacement requires the fixed server registration to agree. No general registration-update or repair command is implemented. Keep this boundary disabled while a separately reviewed change procedure is prepared; do not edit native application/manifest rows independently or overwrite them through the initializer.

PAR and token calls use a ten-second HTTP timeout with both native retry mechanisms and automatic redirects disabled. The original owned request has a ten-second bound and writes retain the five-second authority bound. State/proof/registration guards apply again across awaited work. A timeout, connection loss or canceled response does not establish rollback. A lost PAR response can leave committed state/PAR rows; a lost token response can leave consumed callback/code state and committed issuance. Do not resubmit the same operation or restore consumed state. A new authorized sign-in attempt must generate fresh native artifacts.

Missing/unusable keys, unavailable storage, uncertain commit, expired state, changed registration, bad correlation and failed cleanup deny the affected path. Investigate using generated correlation IDs, source/run/provider/version and finite phase/outcome categories. Never log assertions, codes, state, tokens, verifiers, cookies, raw query/body, private keys, connection strings or native exception messages. Native client transaction logging is suppressed for this profile because native informational messages contain protocol material. A generic unavailable result does not identify the cause or prove a database rollback.

## Capacity, cleanup and restore

The fixed profile bounds are enforced in the owning database:

| Inventory | Limit |
| --- | --- |
| Retained assertion consumption rows, including tombstones | 10,000 |
| Live / retained native PAR entries | 128 / 10,000, also counted within total native tokens |
| All retained server native tokens / authorizations | 20,000 / 2,000 |
| Live / retained native callback state | 1,024 / 10,000 |
| Eligible deletion batch per owning invocation | At most 100 |

Capacity check and native creation serialize together. Expired, revoked or consumed rows remain counted until safely removed. Cleanup respects live token/state and authorization dependencies; full or uncertain capacity denies issuance instead of evicting live rows. Do not manually delete replay tombstones or active state to restore capacity. Feature-local bounded cleanup is implemented; a platform-wide cleanup scheduler, generic audit retention service and production cleanup command are separate unfinished work.

A restored backup can resurrect registrations, grants, sessions, assertions and native state that were previously disabled or consumed. Keep the restored boundary disabled and isolated. Before any separately authorized re-enablement, invalidate the affected fixed registration, administrator grants/sessions, pending native server artifacts and Sessions callback state through a reviewed, provider-aware procedure. Reconcile key availability and the independent Platform/Sessions restore points; elapsed time alone does not establish that resurrected replay records are safe. There is no automatic restore-invalidation command in this implementation, and initialization does not perform invalidation. This missing operational automation remains visible in the acceptance register.

For rollback, stop the opt-in test/host boundary and drain owned work before reverting application composition. Preserve additive schema, data, migration histories and retained credentials needed for investigation. Do not reverse migrations or reactivate a prior registration as an implicit rollback step.

## Evidence required before acceptance

Both PostgreSQL and SQL Server must pass actual native interoperability, durable consumption, quotas, concurrency/failure and permanent regression controls. Retained-key cryptographic tests establish the stated cryptographic behavior; in-process adapters do not establish TLS. The real HTTPS harness preserves native certificate validation and raw backchannel requests, but still uses test-owned authorization/callback dispatch. Preserve that distinction in reports. Certificate-rejection, cancellation/outage, full protocol-negative and B2 mutation/operational evidence must be reconciled with [feature 011 tasks](../../specs/011-administrator-issuer-client/tasks.md) before coherent B2 acceptance and conditional ordinary squash. This runbook is not execution evidence or production authorization.
