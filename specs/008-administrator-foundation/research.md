# Research and review decisions

Baseline: main `6c64d34`, .NET/ASP.NET Core 10.0.12 and existing locked dependencies. Repository inspection and independent architecture/security/test reviews informed this design; recommendations are not runtime evidence.

## Existing code boundaries

- Migrator Composition/HostApplicationExtensions -> Features/Setup -> Database/Setup owns one-shot migration. Keep default migration behavior; explicit bootstrap must not migrate or need schema credentials.
- Infrastructure/Persistence/PersistenceRegistration registers UserManager, RoleManager and native stores on the scoped PlatformDbContext. IdentityStoreConfiguration currently sets storage options only; bootstrap must configure its own deliberate password policy instead of inheriting framework defaults.
- SecurityAuditIntent is available for a transaction spanning native manager writes and audit. Do not reference AuthServer-private LoginAudit/PilotBootstrap from Migrator.
- PilotEligibility rejects disabled, role-bearing and MFA-enabled users. Preserve this rule. LocalIdentitySession has no administrator MFA assurance and must not be reused as proof of it.
- SchemaCatalog mirrors generated migrations; update both providers and readiness together.
- scripts/verify.py currently needs explicit exclusion/selection handling for another Aspire suite; merely adding a project would enlarge existing provider jobs and miss strict identity checks.

## First-increment decisions

**Bootstrap operation:** separate explicit Migrator operation, reading bounded UTF-8 JSON from redirected standard input. Alternatives rejected: startup seeding (accidental privilege), command-line/environment passwords (diagnostic exposure), public endpoint (unnecessary attack surface), new production host (unnecessary structure).

**Account:** native Identity user, disabled and enrollment-pending; UserManager creates password and membership, RoleManager creates initial bundles/claims. No email-confirmation assertion, authenticator seed, recovery code, session, token or first-party client is created. First-party BFF client bootstrap belongs to M02-B's real client-security contract.

**Serialization:** migration-seeded singleton, explicit transaction, actual UPDATE before fresh native account/role reads. The transaction owns its database lock until commit/rollback on either provider. No process lock or blanket execution-strategy retries. Later governance must use the same boundary for every mutation affecting administrator usability.

**Receipt:** permanent completion, no cascading deletion with the user. Bootstrap is consumed even if the user later changes password, is disabled/deleted or loses roles. Matching operation ID and normalized original username return already-initialized without checking or changing current credentials/state. Different operation/identity, missing or inconsistent receipt/state, reserved-role collision or existing account fails closed. Never adopt an existing user or repair roles.

**Permission bundles:** exact allowlisted role claims with a version, no wildcard or implicit future permissions. Only PlatformAdministrator is assigned to the initial user. ApplicationAdministrator, IdentityAdministrator and Auditor bundles reflect approved personas; an Operator is an external deployment role, not automatically an application role. Permission evaluation/enforcement is a later increment; storage is not authorization acceptance.

**Atomicity:** user, roles/claims/membership, enrollment, completion and successful audit commit in one transaction. A denied audit INSERT must roll back preceding successful native manager writes. Check every IdentityResult. Correlation is the operation ID; operator reference is self-declared audit metadata, not proof of external operator identity.

## Mandatory constraints for the second/third increments

Pinned upstream ASP.NET Core v10.0.12 source inspection by the security reviewer:

- [AuthenticatorTokenProvider](https://github.com/dotnet/aspnetcore/blob/v10.0.12/src/Identity/Extensions.Core/src/AuthenticatorTokenProvider.cs): native validation accepts five 30-second counters and persists no replay state. Prove strict code shape and durable cross-node replay denial across the accepted window.
- [UserStoreBase](https://github.com/dotnet/aspnetcore/blob/v10.0.12/src/Identity/Extensions.Stores/src/UserStoreBase.cs): default authenticator values and joined recovery codes are not application-protected secrets. Define protected store adapters, key purpose/ownership and recovery verifiers before use.
- [SignInManager](https://github.com/dotnet/aspnetcore/blob/v10.0.12/src/Identity/Core/src/SignInManager.cs): password success is not MFA; recovery needs additional current-state and attempt checks; do not issue a usable cookie before the outer security/audit transaction commits.
- [UserManager](https://github.com/dotnet/aspnetcore/blob/v10.0.12/src/Identity/Extensions.Core/src/UserManager.cs): check failed concurrency results; role changes do not inherently establish immediate permission invalidation.

Before increment 2, finish the enrollment ceremony, protected secret storage, challenge expiry/attempt limits, MFA freshness, all-authenticators-lost behavior and session-invalidation contract. Before increment 3, define the exact usable-admin predicate and which authorized recovery route can satisfy an exception. Pending enrollment is not usable; lockout defenses may never be bypassed to preserve a count. These are future design tasks, not permission to implement guessed behavior now.

Passkeys, public/email recovery, support-assisted reset and remember-device policy are outside these first increments. Full R1 and AAL-oriented assurance remain incomplete.

## Second-increment design resolution — 12 October 2026

[MFA design](mfa-design.md) resolves the previously open T016 decisions. Independent security source review rechecked pinned ASP.NET Core 10.0.12 AuthenticatorTokenProvider, UserStoreBase and UserManager: native TOTP is Boolean/wall-clock/five-counter validation without replay persistence; native default stores persist raw authenticator/recovery values; CreateTwoFactorRecoveryCode is a protected virtual generation hook. Use the specified protected adapters and durable replay ledger, not an invented matched-counter result or default raw token storage. No package upgrade is needed.

The existing account composition is pilot-owned, so administrator registration/routes/schemes and timeout/protection setup must be made independent without enabling the pilot. The shared persistence path also serves Migrator: AuthServer factor protection cannot become an accidental bootstrap dependency. Initial review found generation continuity, lock order, lockout expiry, staged-key installation and retained-backlog gaps; the final design corrects them. These are reviewed design decisions, not evidence that the planned code exists.
