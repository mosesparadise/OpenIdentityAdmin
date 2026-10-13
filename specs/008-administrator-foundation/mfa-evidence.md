# T028 — administrator MFA behavioral evidence

Status: implementation and local verification in progress; exact-head full CI and reviewed squash acceptance pending. Human observations are not supplied by this packet.

The user authorized T027–T029 as sequential reviewed, squash-merged PRs. T027 is accepted in PR #33 (`ab8b62d`). This packet preserves the approved Administrator project, Features hierarchy, native managers, production transaction boundaries, both database providers, all three browser engines and the existing invocation deadlines. No new production authentication behavior or host is introduced.

## Reconciled acceptance inventory

The original [MFA-01–13](mfa-design.md#required-test-inventory-before-implementation-acceptance) obligations are implemented across T022–T026 and these additions. Existing accepted cases remain mandatory; a new count does not supersede their evidence.

| Family | Existing evidence retained | T028 additions or strengthened oracle |
| --- | --- | --- |
| MFA-01 composition | Host Accounts Composition/Isolation/Protection/Startup tests, AppHost AccountsComposition, Administrator LocalComposition | Entire existing four-combination route/scheme/protection contract remains required; no new authentication shortcut |
| MFA-02 enrollment | Enrollment/EnrollmentEligibility/Admission/Generation/Atomicity and enrollment browser journeys | Cookie reception requires independent committed session/audit backing; genuine native clipboard enrollment and explicit setup/code-saving text |
| MFA-03 protected native factors | Factors Store/Protection/Denial/Recovery/Verification/Migration, owner and generation tests | Actual AuthServer executable restart retains original issuer, populated protection files, cookie and native factor/recovery usability |
| MFA-04 login/freshness | Accounts Session/Freshness/AuthorityState/Form, native RFC oracle controls | Seven native UTC offsets: five accepted counters and both adjacent rejected counters; real malformed OTP forms; observed waiting freshness boundary with interaction and browser expiry kept independently live |
| MFA-05 distributed replay | Observed two-node login/recovery contention, enrollment-to-login replay, recovery verifier snapshots | Exact predecessor session and interaction binding comparisons; winner consumption and loser failed-attempt metadata distinguished; accepted/denied factor audit deltas counted separately; used OTP remains natively valid when denied after process restart |
| MFA-06 budgets/admission | Ceremony Budget/Admission/StateRace/Capacity, account admission and capacity cases | Alternating real nodes and fresh browser jars retain cumulative failures despite correct passwords; fifth failure locks both nodes; unknown, separately disabled and locked accounts share fixed generic denial |
| MFA-07 restricted recovery | Recovery Admission/Flow/Boundary/Capacity/HTTP/Contention/LostResponse | Genuine native optimistic-concurrency rejection after conditional redemption, actual process/database-session loss, and seeded old-cookie authority restoration detected through a protected real page |
| MFA-08 security/logout | Security Flow/Atomicity/Capacity/LiveCapacity/Capability/Contention/CSRF/LostResponse, real security browsers | Fresh password/native old-factor login while voluntary replacement is abandoned, with unchanged active protected factor bytes |
| MFA-09 atomic failures | Audit permission failures, native validation errors, post-write cancellation/freshness, real unreceived committed responses and no redisplay | Actual zero-row EF concurrency failure; audit INSERT observed blocked after native writes; headers-only request still incomplete before actual AuthServer death or termination of its observed database session; exact independent rollback and explicit healthy-node retry |
| MFA-10 state races | Shared governance order, cross-browser/swap/expiry and current-session invalidation | Actual waiting login completion versus suspension, native disable/lockout/stamp, authentication/factor generation, enrollment revision, interaction expiry and isolated password freshness; unchanged post-mutation state and fresh positive controls |
| MFA-11 clock/cleanup | Watermark/replay/budget capacity and pruning controls, competing cleaners, blocked cancellation/deadline | Cancellation after a positive physical DELETE but before category commit; cleaner versus actual admission and completion waits; repeated expiry with cleanup absent, independently full interaction/browser/session storage, preserved genuine sessions and nonallocating transitions |
| MFA-12 UI/evidence | All light/dark, narrow/desktop, keyboard/focus/error, no-script, axe, history and diagnostic-canary tests | Actual native copy/paste of password/TOTP/recovery code with trusted paste events, in-memory equality and accepted forms; verified clipboard overwrite; context-wide canonical-origin observation and autocomplete compatibility |
| MFA-13 upgrade/operations | Both-provider bootstrap/factor/recovery migrations, least-privilege runtime, readiness and startup checks | Process continuity/failure evidence above; operational handoff and telemetry review belong to T029 |

Cases live in [Administrator Features](../../tests/aspire/IdentityPlatform.Administrator.Tests/Features). Exact names and provider/browser traits are reviewed in [the mandatory catalogue](../../eng/administrator-test-matrix.json). The native catalogue contains 500 identities, retaining every previous identity: 250/provider, comprising 237 HTTP and 13 browser cases. HTTP counts/provider are Bootstrap 45, Factors 58, Ceremonies 41, Accounts 53, Recovery 19 and Security 21. Each engine executes Accounts 8 plus Security 5 per provider. The complete ordinary .NET matrix requires 1,868 nonmutation cases, including 61 contract cases; Node/tooling/mutation controls are separate.

No story was added, no existing case removed, no deadline enlarged and no coverage input omitted. There are still 45 mandatory merged coverage inputs and nine module floors. T027 validates the entire catalogue before selecting each invocation.

## Fault and clock evidence boundaries

Native TOTP tests schedule their first and only verification in a stable real UTC counter. They compute independent RFC codes and verify the observed counter interval; they never retry authentication to manufacture a pass. Injected policy time governs product freshness/retention tests, not Identity's native provider clock. Restart replay is checked while the original OTP remains native-valid, preventing expiry from masking missing replay protection.

Concurrency injection changes only EF's expected original ConcurrencyStamp, after observing an actual redeemed verifier in the transaction. The real database update affects zero rows; the real EF concurrency callback is observed and the native store/manager returns its failure. No synthetic IdentityResult replaces this path. Rollback preserves complete user/factor/session/replay/recovery/interaction/audit state, then an explicit retry uses the same original code.

Process and database faults first observe the real audit INSERT waiting behind a test-owned lock in the leased database. The HTTP request uses ResponseHeadersRead and must still be incomplete before interruption. Only the exact owned AuthServer executable generation or independently identified blocked runtime database session is terminated. The test drains its request, reads committed state independently and explicitly retries through the healthy node. These tests establish precommit rollback and no delivered authority at the observed boundary, not deployment disaster-recovery guarantees.

Cleanup commits four categories independently. The post-delete cancellation case interrupts the first category and compares security plus browser/admission/receipt metadata; it does not claim earlier committed categories roll back. The completion race permits the independently committed consumed-interaction category before the paused session category. Gates have their own bounded observation deadline, detect early operation completion, and always release/drain owned operations before fixture disposal.

## Behavioral mutation controls

The six existing administrator mutants remain required. Two additional reviewed substitutions bring the inventory to eight/provider:

- `administrator-precommit-cookie` replaces the completion's commit with SaveChanges, allowing a cookie to escape after transaction rollback. The marked assertion requires independently committed session/audit backing after actual cookie reception. Separate blocked-INSERT tests establish the temporal no-early-response boundary.
- `administrator-recovery-cookie` makes an actual pre-recovery cookie regain full account-page authority during RecoveryRequired. The real protected HTTP page must reject it with a fixed marker. This mutant restores old-cookie authority; it does not simulate issuing a new cookie directly from a recovery code.

Every mutant must fail exactly its reviewed assertion with matching native identities and no unrelated failures; original/restored controls and source hashes must pass. No manufactured Grant or authentication stub is used. Full required runtime evidence remains pending until recorded in the PR and verification log.

## Reviews and local findings

Independent reviews cover native/fault evidence, architecture/mutations, cleanup/capacity, UI/evidence and strict test tooling. Corrected findings include secret-safe cookie assertions, isolated browser/password expiry, bounded gate observation/early failure, verified clipboard overwrite and context-wide origin observation. Exact predecessor/interaction witnesses cannot be empty. Documentation accurately distinguishes the two mutation behaviors and the native clock.

Local native-window/waiting-state checks passed 16 cases. The first concurrency observer used EF's general failure callback; its corrected dedicated concurrency callback then passed in the eight-case process/concurrency/budget/form run. The initial cleanup/security run passed seven cases; its strengthened login oracle incorrectly expected failed-attempt metadata to increment interaction Revision. The correction preserves the documented failed attempt while requiring every other loser field unchanged. These failed attempts are diagnostic evidence, not promoted to acceptance. The corrected 13-case PostgreSQL rerun passed with zero skips; all 133 Python tooling controls passed. Build/static quality and strict discovery pass without new allowances. Full provider/browser/mutation/coverage CI remains required for exact-head acceptance.

## Human and external obligations

Trusted native paste and autocomplete hints do not establish saved-password-manager autofill or screen-reader usability. Windows/WSL2 human execution, assessor/platform/browser/authenticator/password-manager/assistive-technology versions and actual safe results remain required. T029 prepares the reproducible runbook and result template. Missing human results remain not run, not passed; T028's whole-increment acceptance and T017/T018 stay open while those obligations remain.

ADMIN-003–006/009–012 and FR-07/12/17–19/21–24 are traced through the families above. T019–T021 current authority/last-administrator enforcement, M02-B–D management API/BFF, M05 passkeys, M01-F vendor activation and human/external security/conformance/public-release gates are separate. AI engineering review and automated coverage do not replace them.

Tooling rename for changed code: `test_reviewed_manifest_targets_exist_exactly_once` → `test_Given_ReviewedMutationManifests_When_TargetsAreResolved_Then_ExactInventoriesAndUniqueSubstitutionsAreRequired`; assertion semantics are retained and the reviewed administrator inventory is now eight.

Partition regression rename for the changed independent total: `test_scheduled_partitions_preserve_every_provider_case_without_overlap` → `test_Given_ScheduledPartitions_When_Combined_Then_EveryReviewedProviderCaseAppearsExactlyOnce`; the provider total is now 580 (302 standard + 41 lifecycle + 237 Administrator). Mutation markers identify only the demonstrated bypass: successful protected recovery-page authority, or a received enrollment cookie with neither committed session nor audit; unrelated statuses/storage mismatches fail unmarked assertions.
