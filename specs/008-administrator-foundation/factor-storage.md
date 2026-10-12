# T023 — protected factor storage and native Identity integration

Implements the storage prerequisite for ADMIN-003–006/009/011/012 and MFA-03/13. T024 owns authoritative locks, budgets, replay decisions, audit and cleanup; T025/T026 own journeys/session authority. No account endpoint or usable administrator session is enabled by this packet.

## Concrete design

Preserve existing projects and Features/Setup. Add persistence records/configuration under Infrastructure/Features/Identity/Persistence and cohesive native-store/protection code under Infrastructure/Features/Identity/Factors. Shared persistence installs a default-deny EF-derived user store. AuthServer explicitly selects protected factor services only with validated issuer protection. Migrator retains its native password/bootstrap manager with no Data Protection dependency. Block reserved native authenticator/recovery token paths as well as factor interfaces, preventing plaintext fallback.

Add orthogonal suspension and factor/recovery generations to enrollment. Add authentication state, immutable interaction slots with separately rotatable public identifiers, protected factors, replay records, recovery verifiers, sessions, browser budgets and installation admission. Use compact surrogate keys for material rows, bounded ordinal identifiers, nonclustered uniqueness for user/generation, portable numeric revisions and restrictive relationships. SQL Server's 900-byte clustered-key limit rules out composite clustered keys beginning with the existing 450-character user ID. Add both native EF migrations and readiness identities. Preserve existing disabled Pending rows, permanent receipt, consumed governance, native credentials and pilot data; create no active factor/session or MFA authority. New bootstrap records initialize inert authentication state; migration backfill never grants authority.

Protect seeds using the verified shared Data Protection ring with product/version/canonical issuer/user/material-kind/generation purposes; staged seeds additionally bind the persisted interaction identity. Recovery generation uses the native manager hook with 128 random bits, exactly ten distinct canonical codes, and domain/user/generation-bound length-prefixed SHA-256 verifiers. No raw/joined recovery collection may enter native token storage. Mutations require the existing scoped database transaction and narrowly scoped internal operation context; no public request-created installation capability. Staged selection is scope-owned and always disposed. Installation proof must come from actual native verification for persisted binding and be single use. These storage primitives do not establish T024 ceremony lock/replay/audit acceptance; production ceremony access remains unavailable.

## Verification and delivery

Use the approved Administrator project, new Features/Factors and existing Fixtures. Reuse Bootstrap database leases and durable issuer fixtures. Pull forward only T027's necessary story partition plumbing: explicit Bootstrap and Factors runs per provider, exact identity inventories, unchanged 660-second invocation deadline and mandatory factor coverage inputs. Preserve all 45 Bootstrap identities/provider; leave later browser partition work and T027 overall acceptance open.

Cover native dispatch, reserved-token bypass denial, unsupported Migrator operations, actual native TOTP verification, protected purpose/user/issuer/generation/interaction substitution, corrupt/missing keys, durable ring continuity, independent recovery verifier expectations, single-use native redeem, rollback/cancellation and actual both-provider schema/upgrade constraints. Tests must not print seeds/codes/ciphertext or provider exception payloads. Pin previous migration IDs in historical upgrade regressions instead of relative list offsets. Component/store evidence is not MFA journey, distributed replay or full ceremony acceptance.

Independent architecture/security and test design reviews found no unresolved user clarification. Their native-token bypass, SQL clustered-key, scoped capability, exact discovery and historical migration-index findings are incorporated above. Final exact-head source and CI acceptance is recorded in [verification](verification.md#t023-factor-storage--12-october-2026).

## Source-review dispositions

- Native generic token APIs reject reserved framework providers and their whitespace/Unicode aliases; password-only hosts never resolve a Data Protection provider.
- Compact material keys avoid SQL Server clustered-key limits. Backfill explicitly reconciles native-user/product ID collations and byte lengths; session methods require exact byte length on both providers.
- Scoped selections snapshot bindings and require unchanged tracked entities both at entry and use. Verified installation rejects mutation, expiry, transaction substitution, detachment, deletion and reuse.
- Bulk recovery writes reject pending tracked changes and detach clean snapshots, preventing stale tracked rows from restoring spent codes or inserting extra verifiers.
- Test oracles use published RFC 6238 vectors and independently serialized recovery hashes. Noncanonical recovery input is deterministic; no random code shape determines expected failure.
- The shared database wait-graph observer supports both bootstrap and factor tests. Redemption contention must be observed on the actual provider before releasing the winning transaction.

T023 is complete: PR #27 was normally squash-merged as `824220b`. Exact head `e356090` passed all 36 CI jobs, 1,460 ordinary cases, 25 required coverage inputs and nine module floors. The 51 Factors and 45 Bootstrap cases per provider remain separate bounded invocations. [Verification](verification.md#t023-factor-storage--12-october-2026) records commands, results, review dispositions and limits. T024 ceremony rules and T025/T026 session/journey authority remain unimplemented.
