# Administrator browser session operations handoff

## Scope and activation status

This T022 handoff describes the implemented internal B3 boundary and the work still required before production activation. It is not an activation command, production correction authorization, full B3 acceptance record or independent security assessment. Use the [feature verification record](../../specs/013-administrator-browser-session/verification.md) and its dated source/run/artifact bindings for accepted evidence; candidate tests are not accepted until their exact-source reports and reviews pass.

B3 requires a new password and MFA ceremony for every explicit sign-in. It creates native client state, PAR, authorization code and a canonical protected BFF session only after their respective guarded commits. The BFF holds protocol tokens on the server; browser cookies carry bounded opaque/protected references. Issuer-assisted logout is optional after local commit. A returned browser URL never proves issuer revocation.

Default production Programs remain dormant. Explicit test composition demonstrates the boundary; it does not authorize enabling it on a public route. Preserve separation from M02-C refresh/request processing, M02-D current Admin API authorization and M02-E React. A session status response describes local authentication, not present-day management permission.

## Ownership, configuration and startup

Use the existing provider registration contracts and one-shot Migrator lifecycle. PostgreSQL and SQL Server retain their separate migration assemblies, native histories and runtime provisioning. A replacement relational EF Core provider must satisfy the existing provider/conformance contracts; no browser-session code may choose a provider through hardcoded SQL or switch on these two names.

Platform owns administrator accounts, issuer sessions/continuations, fixed registration and native server protocol state. Sessions owns BFF browser contexts/intents/logout continuations, protected tickets, native client state and their local audit intents. Each runtime uses its separately provisioned least-privilege identity. Do not give a BFF runtime the Migrator credential or Platform business-write privileges to remedy a grant failure. Readiness and migration identity remain mandatory; web startup must not migrate, repair a manifest or broaden grants.

Keep the canonical HTTPS issuer/client identities and exact callback/resume/post-logout destinations consistent with the audited fixed registration. Retain normal TLS trust, host-only Secure/HttpOnly navigation cookies with SameSite=Lax, the native antiforgery cookie with SameSite=Strict, exact Origin/native antiforgery checks and the documented narrow protocol exceptions. Do not add arbitrary return URLs, a credential-bearing logout hint, CORS/bearer shortcuts or certificate-ignore switches. [Registration upgrade](administrator-registration-upgrade.md) records the audited B2-to-B3 transition and old-code/PAR invalidation; startup is not an update mechanism.

### Additive histories

| Store | PostgreSQL migration | SQL Server migration |
| --- | --- | --- |
| Platform issuer continuation | `20261023223630_AdministratorIssuerContinuation` | `20261023223639_AdministratorIssuerContinuation` |
| Sessions browser coordination | `20261024000243_BrowserSessionCoordination` | `20261024000342_BrowserSessionCoordination` |

These extend the existing native protocol and protected-ticket histories. Preserve earlier migration IDs and applied-history rows. No database recreation, history reset, down migration or provider swap is part of this handoff. The accepted coordination/schema and provider migration tests establish additive internal behavior; they do not prove a production upgrade or backup restoration.

On application rollback, stop new requests, drain/join original work and retain the upgraded schema, audit/replay markers and protection material. Confirm that the intended prior application version understands the retained registration revision and schema before considering separately approved use. A code rollback does not reverse committed revocation, restore consumed native state or justify database rollback.

## Key ownership and instance lifecycle

[Native client operations](administrator-native-client.md), [BFF ticket protection](bff-session-store.md) and [issuer protection](issuer-protection.md) define the actual validated configuration. Keep issuer protocol keys, native client assertion/state keys and BFF ticket/browser protection roles distinct. Stable shared durable material and application identity permit independent BFF instances to decrypt proofs/tickets; each instance imports and disposes its own certificate/protection objects. Sharing a live object in a test is not process-independence evidence.

Retain prior validation/decryption material for all outstanding original lifetimes and retained dependencies. A wrapper/key deletion that makes old proof or ticket material unreadable denies authentication; do not regenerate a key under the same identity or retry consumed callback state. Rotate assertion material only with an audited matching fixed registration transition. No fleet-wide rotation/retirement command or production schedule is delivered here.

Stop admission and join requests, protocol children and service roots before disposing credentials or database leases. Cleanup failure or uncertain publication is not permission to release keys early or to publish staged authority. Actual separate-process/imported-key tests and key-loss controls are linked in [lifecycle verification](../../specs/013-administrator-browser-session/lifecycle-verification.md); their scope is disposable stores and synthetic retained material, not production key custody.

## Failure handling and honest outcomes

| Observation | Meaning and permitted recovery |
| --- | --- |
| Definite precommit/save rollback | No success is claimed. Preserve the original state evidence; a later explicit sign-in/logout action may create a new bounded operation. |
| Callback/code consumed but local ticket missing | Fail closed. A new explicit password/MFA ceremony is required; do not replay code, restore consumed state or synthesize a cookie. |
| Commit acknowledgement lost | The client cannot infer rollback. Preserve the finite unknown outcome and original correlation evidence; no automatic mutation retry. |
| Local logout committed, response not received | Local invalidation is durable. The implemented explicit bounded reconciliation observes the existing tombstone without another commit, audit, renewal or issuer call. |
| Local logout committed, remote preparation unavailable | Local success remains true. Do not resurrect its ticket to retry issuer logout. |
| Issuer confirmation cancelled, rejected or failed | Local logout remains true; issuer status is not confirmed. Only the protected issuer POST may revoke its exact source. |
| Issuer commit succeeded but return lost | Issuer and local effects stand independently. A later valid original return can record navigation once; it remains unverified status. |
| Return expired, mismatched or replayed | Deny without extending correlation, issuing authority or changing issuer effects. |

The original deadlines apply across awaits, commit, cleanup and final cookie/link publication. A later tombstone or ticket deadline cannot replace the incoming browser proof bound. A new valid form is not authorization to revive a consumed operation. No refresh capability is implied by an active browser ticket.

Use finite outcome/phase categories and generated local correlation identifiers with source/run/provider/version metadata. Never log raw URLs/query strings, codes, assertions, state, tokens, factors, cookies, protected ticket bytes, connection strings, private keys or native exception messages. Screenshots must use reviewed fixed-content surfaces and exclude credential pages, browser chrome and visible personal data.

## Cleanup, retention and capacity

`BrowserSessionCleanup.RunAsync` is a callable feature-local mechanism. It runs under the same owned browser transaction ordering as writers. One invocation removes at most **100 intents, 100 logout continuations and 100 contexts**, in child-before-parent order; “100 per category” is not a total batch of 100. Equality at `RetainUntil` is retained; deletion requires a later clock and no live ticket/child dependency. Rollback preserves the whole transaction. Browser operation validation permits at most the defined 30-second rollback tolerance using effective observed time; cleanup rejects backward movement after candidate selection. Cancellation, unavailable storage or exhausted capacity deny the affected operation. Failure to reserve optional remote capacity preserves the already committed local logout.

The coordination limits are 1,024 live and 10,000 retained entries in their defined inventories. Retained consumed/expired markers still occupy capacity until eligible cleanup. Never delete live rows, audit intent or replay tombstones manually to make space. B1 tickets, native client/server state, issuer continuation and audit retention have distinct ownership and dependencies; use their feature-local policies rather than one generic date filter.

**Production scheduling is not implemented.** No BFF/Worker scheduling caller for the B3 or B1 cleanup mechanism has been delivered. Before activation, assign an operations owner and implement/review bounded scheduling, cancellation, failure reporting, capacity observation and dependency-safe ordering on both providers. The actual orphan-callback, retention equality and admission-race controls establish callable semantics; they do not establish a running production service or retention policy approval.

## Restore and replay invalidation

A restored backup can resurrect revoked tickets/generations, used intents/correlations, issuer sources, grants, assertion consumption and native code/PAR/client state. Restore both stores into an isolated disabled boundary. Preserve the original backup identifiers, restore points, source versions and retained key inventory without copying secret material into evidence.

Before any separately authorized re-enablement, a reviewed provider-aware procedure must invalidate affected browser generations/tickets, issuer source sessions and continuations, fixed profile authority and pending/consumed protocol artifacts in **both Platform and Sessions**. Reconcile inconsistent restore points and key availability. Do not assume elapsed time alone or restored audit records prove safe replay protection; clock bounds and dependencies must be considered together. Retain relevant audit history and keys until the reviewed retention/disposition decision.

There is **no automatic both-store restore-invalidation command**. `BrowserRestoreTests` demonstrates selective real row resurrection and explicit invalidation in disposable databases; `BrowserOrphanCleanupTests` exercises a genuine consumed callback orphan. Neither is a database-engine backup/restore drill, disaster-recovery exercise, automated fleet recovery or production procedure. Those remain activation prerequisites with a named operator and human-reviewed evidence.

## Privilege and audit obligations

Actual PostgreSQL/SQL Server tests exercise denied audit inserts and atomic rollback, exact source/ticket revocation, local actor provenance, permission boundaries and retained migration histories. A successful HTTP response alone is not sufficient evidence: inspect the committed owning-store effects and exact audit revision/correlation. Runtime account grants must remain scoped to their owned schema/functions; provisioning must preserve separate Migrator and runtime credentials.

Browser `CorrelationId` links the local context/generation, intent/logout and local audit transitions. **There is no implemented cross-store BFF-to-issuer correlation linkage.** The issuer continuation and logout ceremony use their own correlation identifiers; do not present them as the same operation merely because time, synthetic subject or browser navigation matches. The contract's linked-audit obligation remains open under the remaining B3 integration work. Before activation, implement and review a non-authorizing correlation transport/provenance design without turning browser-supplied correlation into authentication authority or leaking personal/protocol data.

Independent human security, accessibility, privilege, key-custody, retention, recovery and production operational observations remain required. AI source review and disposable test execution do not close those rows.

## Evidence and remaining work

Use these accepted records for their exact bounded claims:

- [Coordination and additive schemas](../../specs/013-administrator-browser-session/browser-coordination.md), [issuer continuation](../../specs/013-administrator-browser-session/issuer-completion.md) and [registration upgrade](administrator-registration-upgrade.md).
- [Protected login](../../specs/013-administrator-browser-session/protected-browser-login.md), [native callback](../../specs/013-administrator-browser-session/product-browser-callback.md) and [session use](../../specs/013-administrator-browser-session/session-use.md).
- [Local logout](../../specs/013-administrator-browser-session/local-logout.md), [issuer logout](../../specs/013-administrator-browser-session/issuer-logout.md), [BFF continuation](../../specs/013-administrator-browser-session/logout-continuation.md) and [lifecycle verification](../../specs/013-administrator-browser-session/lifecycle-verification.md).
- [Initial accepted HTTPS journeys](../../specs/013-administrator-browser-session/https-browser-logout.md) and [remaining browser matrix](../../specs/013-administrator-browser-session/logout-browser-matrix.md), whose current candidate/delivery disposition must be checked before claiming execution.

T022 completes an accurate handoff only after review and documentation acceptance. It does not close T003/T004 remaining integration, T012/T016 full browser/security coverage, T023/T024 coherent B3 acceptance, production cleanup scheduling, cross-store audit linkage, restore automation, fleet key procedures or manual product acceptance. Firefox and the two exact WebKit certificate cases remain deferred under A-009, not passed. Preserve all six historical findings, 48 human rows, 345 not-assessed controls, PR #69/issue #70, A-009 restoration and production/public-release gates. No expired diagnostic approval is revived.
