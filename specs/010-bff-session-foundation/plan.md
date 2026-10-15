# Implementation plan: Durable administrator session foundation

**Planning branch**: `049-bff-session-foundation`; **implementation branch**: `050-bff-ticket-store` | **Date**: 15 October 2026 | **Spec**: [spec.md](spec.md)

## Summary

Feature 010 is the first internal M02-B slice: a durable protected ticket store with expiry, concurrency, revocation and bounded cleanup. Keep browser/management routes disabled. Planning PR #51 is merged. The implementation now exists on `050-bff-ticket-store`; provider verification and exact-head acceptance remain incomplete. See [verification](verification.md) for the separate local results and blockers.

## Technical context

Use the pinned .NET 10/EF Core 10/ASP.NET Core and xUnit v3/MTP stack, both existing provider migration projects, Aspire and strict CI inventories. No dependency upgrade or new production project is needed for the store. Future OpenIddict client/PAR integration needs compatible locked-package evidence.

Use native `AuthenticationTicket`/`TicketSerializer`, not an alternate claims/token format. Protect the serialized ticket and binding/deadline/revision envelope through a dedicated BFF Data Protection boundary. [Data model](data-model.md) and [internal contract](contracts/ticket-store.md) define limits and failure behavior.

Multiple BFF instances share the selected session database and BFF ring. There is no throughput or five-second API-enforcement claim. Retain the existing 30-second database command limit and propagate cancellation. Cleanup handles at most 100 eligible rows per invocation. Preserve existing test execution deadlines; if a new partition is necessary, review its explicit bounded selection and mandatory coverage before execution.

## Constitution check

No exception is proposed. Current user authority advances M02-B implementation within approved v0.2 following merged planning PR #51. Planning acceptance does not declare runtime or activation tasks complete.

- Keep feature-local ownership and the existing hierarchy. BFF cannot reference AuthServer handlers or acquire platform business-write privileges.
- Extract only stable key-ring validation/loading mechanics into narrow Infrastructure code when reuse is necessary; preserve issuer behavior/negative controls. Do not duplicate that subsystem or share configured rings/application names.
- Both providers, additive migrations, least-privilege grants, cancellation, races, atomic audit intent and failure behavior accompany implementation.
- New code must meet documentation, test naming and size rules. Never renew/copy the expired witness allowance.
- Exact-source applicable CI and independent review precede normal squash. Planning PR #51 received prose checks only. The executable implementation requires full checks and supplies no acceptance until those pass.
- Historical findings, human assessment and activation/public-release gates stay open. No expired diagnostic approval is revived.

Recheck these principles against the actual implementation diff before its acceptance.

## Project structure

| Existing path | Planned responsibility |
| --- | --- |
| `src/backend/IdentityPlatform.Admin.Bff/Features/Sessions/` | Native serialization, handle lifecycle, BFF protection/configuration and orchestration; internal registration |
| `src/persistence/IdentityPlatform.Infrastructure/Features/Sessions/Persistence/` | Ticket/audit entities, mappings and narrow persistence operations |
| `src/persistence/IdentityPlatform.Infrastructure/Features/Protection/` | Stable shared protection mechanics only, if extracted; separate caller-owned credentials/purpose |
| Existing AuthServer issuer-protection features | Preserve behavior/configuration and controls when adapting to shared mechanics |
| Existing provider migration projects under `Sessions/` | Additive ticket/audit migrations and snapshots; preserve native client-state tables |
| Existing Migrator and `Infrastructure/Features/Health/SchemaCatalog.cs` | Explicit upgrade/readiness and session-runtime grants |
| `tests/backend/IdentityPlatform.Host.Tests/Features/BffSessions/` | Bounds, native serialization, protection/configuration and route isolation |
| `tests/persistence/IdentityPlatform.Persistence.Tests/Features/NativePersistenceTests.cs` | Provider migration/model/grant contracts |
| `tests/aspire/IdentityPlatform.AppHost.Tests/Features/BffSessions/` | Real provider, restart/two-instance, concurrency and failure behavior |
| `eng/test-matrix.json` and existing verification/coverage configuration | Exact inventory and mandatory coverage inputs; no avoidance exclusions |

No new test project is assumed. Reuse existing fixture ownership. Synthetic test probes must remain in test code and never appear in production routes.

## Delivery sequence

1. Deliver this reviewed specification/dependency packet as a prose-only PR.
2. Implement the complete store slice, including key ownership, both migrations, atomic intent, failure/race regressions and operations guidance; demonstrate unchanged production route inventory.
3. Resolve the executable-delivery dependency in [research](research.md). PR #50 is not a BFF code dependency, but its exception retirement is a shared quality-gate dependency. Do not merge executable work around the failed gate.
4. Prepare distinct administrator issuer/client and browser integration from the later packages below. They inherit no runtime acceptance from this store.

## Later M02-B–E work packages

| Package | Concrete deliverable | Acceptance dependency |
| --- | --- | --- |
| B2 — Administrator issuer/client | Distinct current account/session/MFA admission; fixed manager-backed client/scope/audience registration; native code/S256/PAR and `private_key_jwt` integration | Prove pinned library support and both-provider denial/replay matrix; never broaden pilot eligibility. Pull only the required fixed protocol subset forward from M03/M04; catalogue UI is not prerequisite. |
| B3 — Browser session journey | Login, exact callback, durable native client state, secure host cookie, fresh handle, local and accurately bounded remote logout | Accepted B1/B2; route-specific CSRF/correlation/nonce/redirect/TLS checks, safe themed error states and real three-engine evidence |
| C — Refresh/request boundary | Two-node serialization, no-reuse replay policy, response loss, same-origin/CSRF and fixed proxy/header rules | Native issuer refresh/revocation and live eligibility; no browser tokens. Ticket CAS is not OAuth refresh serialization. |
| D — Independent Admin API | Remote issuer/audience/type plus token/authorization-store validation and current account/session/permission checks | Measured both-provider, cross-instance five-second enforcement, fail-closed storage and no direct-HTTP bypass |
| E — React shell | Generated BFF browser contract/client and same-origin Aspire UI | Accepted BFF/API boundaries, real browser-to-issuer journeys and no browser token storage |

CSRF/fixation/origin protection accompanies the first route that needs it; placement under M02-C does not permit an unprotected B3 route. Local logout commits ticket invalidation even when remote logout fails and reports the remaining consequence accurately. Refresh issuance stays disabled until its safe lifecycle exists. Missing remote logout remains open M02-B work, not a completed feature claim.

## Verification

## Implementation design — 15 October 2026

Implementation branch `050-bff-ticket-store` starts at the planning squash
`ab281729fc1a44c188359faac620105d50376aec`. The source inventory below refines
T005 without activating a browser consumer.

- `AdminSessionTicket` and `AdminSessionAudit` are internal Infrastructure
  entities, configured by `AdminSessionModel` from `AdminSessionDbContext`.
  Persist UTC instants as UTC ticks (`bigint`) on both providers, avoiding
  PostgreSQL microsecond rounding at equality boundaries. Digest/identifier
  columns use provider-specific ordinal collations. Keep native OpenIddict
  tables and their existing model registration intact. Audit has no ticket
  foreign key or cascade. Index idle expiry, absolute expiry, revocation and
  subject/source binding. Audit operation/outcome are finite integers.
- `SessionTicketPersistence` owns short explicit database transactions and
  parameterized EF queries. Create inserts ticket and audit in one commit.
  Renew uses a conditional update on digest, expected revision, unrevoked and
  still-live state, then inserts audit before commit. It never upserts/retries.
  Revoke conditionally sets the current unrevoked row's revocation timestamp
  without a caller revision; its update lock protects the subsequent revision
  read/audit until commit. Revocation need not increment revision: the permanent
  denial predicate wins over every renewal, including `long.MaxValue`.
  Cleanup selects at most 100 digests and rechecks revoked/expired predicates
  in the delete. No audit deletion is implemented.
  After renewal's conditional update obtains write ownership, resample the
  injected clock and recheck the original and replacement windows before audit
  and commit; rollback if lock waiting crossed either original deadline or
  observed clock rollback. Create similarly rechecks after insertion. Load
  samples time after retrieval/decryption before returning. Repeat mutation
  eligibility checks immediately before commit after awaited audit insertion.
  Provider barrier tests hold the row lock or audit write across
  the original deadline, proving a queued renewal cannot revive expired state.
  Observe the native blocked write before advancing time and require unchanged
  revision/payload plus no audit commit. Independent in-process providers prove
  store restart, not a process-crash recovery claim. Dispose the dedicated
  protection provider before wrappers, including partial initialization failure.
- BFF `SessionTicketStore`, `SessionTicketCodec`, `SessionTicketPolicy` and
  `SessionHandle` own orchestration, native binding/protection, clock policy
  and canonical random references respectively. An internal result category
  distinguishes rejected/unavailable from operational failure. Cancellation
  propagates; raw exceptions/private inputs are never logged or returned.
  Every method accepts cancellation for database work. No automatic mutation
  retries or commit-outcome inference is permitted.
- The protected envelope contains every persisted authentication metadata
  field except the additional database-only revocation denial, plus the native
  ticket. Version and byte limits are checked before native deserialization.
  The clock retains tick precision; native `AuthenticationProperties` use their
  framework RFC1123 whole-second representation. Compare those properties to
  the corresponding envelope instants truncated to seconds, while enforcing
  actual expiry/rollback against full UTC ticks. This wire precision never
  widens store eligibility; native expiry can be less than one second earlier.
  Fractional-second round-trip tests cover that conservative projection.
  Preserve other native properties and tokens in a
  private serialized copy, without mutating the caller's ticket.
- Extract `PrivateStorage`, `EncryptedKeyRepository`, bounded password-encrypted
  PKCS#12 loading, and generic registration/startup verification into
  `Infrastructure/Features/Protection`. Keep issuer certificate-role/validity
  policy and ownership in AuthServer; translate shared fixed failures through
  its existing safe exception. Split long native XML validation into cohesive
  methods, document moved methods and retire only their actual legacy entries.
  Existing issuer negative tests remain mandatory regressions.
- BFF owns an explicitly disposable `SessionProtection` container with a
  dedicated service provider and wrapper credentials. Configuration is
  `Bff:Sessions:Protection:{ApplicationId,KeyRingPath,Current:{Path,Password},Previous}`.
  ApplicationId is a bounded deployment identifier; application discriminator
  `IdentityPlatform.Admin.Bff|<id>` and ticket purpose/version are code-owned.
  Up to four retained wrappers, private Linux paths, encrypted native ring,
  active usable RSA wrapper, retained decryptors and startup probes are required.
  No fallback and no implicit issuer-ring reuse. Registration remains an
  internal opt-in helper unused by default production host composition.
- Add IVT only for BFF and existing test/migration assemblies where needed;
  no production project or package upgrade. Existing session grants cover new
  tables after the one-shot Migrator; prove this with runtime credentials.
- Host tests use the existing synthetic `IssuerMaterialFixture` for independent
  BFF ring material and native serialization/protection/policy negative cases.
  Persistence tests exercise both additive migrations and grants. AppHost tests
  use the existing Aspire harness and separate service providers sharing the
  real session database/ring for restart, competing renewal, both revoke
  interleavings, deletion races, rollback, cancellation and unknown outcomes.
  Test interceptors/barriers remain test-only; production endpoint inventory is
  unchanged. Keep every existing deadline and native test identity.
- Inventory consumers are `eng/test-matrix.json`, `scripts/test_selection.py`,
  `scripts/verify.py`, `eng/coverage-policy.json`,
  `scripts/check_coverage_inputs.py` and the unchanged full workflow. Reconcile
  actual discovered identities and coverage; no new exclusion or allowance.

Independent architecture/test and security review of this design precedes source
edits. Runtime completion and executable merge still require their own evidence.

[Quickstart](quickstart.md) gives prerequisites/evidence capture. [Tasks](tasks.md) maps requirements to source and tests. [Verification](verification.md) separates planning checks from unexecuted runtime acceptance. Independent architecture/test/security review applies to both packet and source.


## Bounded native execution refinement — 15 October 2026

The observer candidate completed twelve SQL AppHost cases in 622.606 seconds but
could not report all fifteen before the unchanged 660-second invocation bound.
The existing eight cases alone took 458.475 seconds. The mandatory workload now
uses two sequential invocations within the same foundation job: Foundation
(eight cases) and BffSessions (seven). Both providers retain fifteen exact native
identities; native discovery proves the groups disjoint and exhaustive before
execution, and strict TRX counters/row/definition/entry bindings validate each
separate report. No optional group selector is added.

Each invocation still has 660 seconds and the job still has 20 minutes including
preparation; the two maximum durations cannot both fit that job. This is an
explicit change to invocation grouping, not a promise of aggregate completion.
The same parent coverage collector and 55 required inputs retain all modules and
floors. Unit, production, native case, fixture and security behavior is unchanged.
Independent architecture/test and security reviews pass; runtime acceptance is
still required. PR #50's accepted allowance retirement and bounded exact-image
preparation are integrated from main without repeating its implementation.
