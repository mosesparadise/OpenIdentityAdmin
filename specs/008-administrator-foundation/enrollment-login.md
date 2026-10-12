# T025 — enrollment, MFA login and account sessions

Status: implementation in progress on accepted T024. This packet applies the approved [MFA design](mfa-design.md); it does not claim runtime acceptance or introduce a new scope approval.

Preserve the existing projects and feature hierarchy. AuthServer gains Enrollment/Mfa journeys and an account-status destination with feature-local Setup and small Razor handlers. Infrastructure owns native ceremony decisions, generation allocation and authoritative session validation. Shared browser/form/cookie mechanics remain under Accounts/Shared. No controller, management grant, new host or generic repository is added.

## Reviewed implementation boundaries

- Add a nonnegative durable `LastAllocatedFactorGeneration`, allocated under the account lock with checked increment. Both migrations backfill at least the maximum active, staged and replay generation. Committed abandoned stages never reuse a generation.
- Capture nullable `ExpectedEnrollmentRevision` on interactions; old null rows do not authorize a journey. Recheck exact consumed bootstrap ownership, Pending revision, disabled/unsuspended/native-unlocked state and absence of an active factor before activation. Never temporarily enable an account for password checking.
- Password success admits only a browser-bound five-minute interaction. Stage a native manager-generated key for the allocated destination. Session capacity precedes TOTP consumption. Verify/reserve the native proof, complete the original budget capability, install the exact staged key, and then change security state. Preserve its accepted replay reservation. Save recovery-generation changes before the unchanged-entity native recovery lease.
- Cookie issuance and one-time recovery-code display occur only after audited commit and transaction disposal. Password-only proof, uncommitted database state and arbitrary protected tickets cannot authorize issuance. No secret-bearing TempData, logs or persistent plaintext response cache.
- Session validation is a distinct bounded ownership mode. It rereads account, enrollment, factor, stamp, generations, native/product lockouts and stored session on every protected request. It must not call the budget-opening helper, clear counters or emit authentication-success audits. A narrow commit permits only monotonic activity/watermark metadata; unrelated authority changes fail. Thirty-minute idle and eight-hour absolute expiry retain actual authentication times.
- The account session authorizes only its own status/security surface. Recovery, replacement, regeneration and logout remain unavailable until T026. Management API/BFF/docs and OAuth administrator authority remain later milestones.

## Local composition and verification

AppHost needs independent account activation because existing protection forwarding is pilot-only. Use Accounts/Setup and one graph-owned issuer/protection descriptor shared with Pilot. Create secret parameters once, preserve the canonical HTTPS issuer and complete protection subtree, and forward account activation only to the main AuthServer. Accounts-only mode performs no bootstrap; initialization remains the protected explicit Migrator operation. Test all four feature combinations, invalid settings, parameter uniqueness and recipient isolation.

Pull forward only T027's necessary Administrator HTTP/browser partitions. Preserve existing issuer invocations and the 660-second invocation bound. The six provider/browser cells use real TLS and all three engines; all additional coverage inputs are mandatory. Use the existing dedicated Administrator project and real isolated databases/hosts. Reuse stable fixtures and safe browser diagnostics; no secret-page screenshots, traces, videos, HTML dumps or network payload capture.

Tests cover MFA-01–06/09–13 as implemented: native random-key enrollment and destination continuity, receipt/revision eligibility, strict forms/CSRF/bindings, distributed replay and observed state races, capacity before consumption, truthful session times and every-request authority, audit/native/dependency/cancellation failures, lost responses, secret-safe UI/accessibility and both-provider migrations. Source reviews cover architecture/security, test correctness, capacity and UI. T027/T028/T029 remain umbrella tasks until all journeys, including T026, are accepted.

## Review dispositions — implementation draft

Architecture/security review identified and corrected helper context ownership, private grant construction and atomic one-shot issuance, native manager operation ownership, final expiry sampling after awaited work, monotonic small-clock rollback handling, and activation's exact user/governance binding. Each concrete issue has a provider regression. The last production source review found no remaining blocker; this is not runtime acceptance.

Capacity review identified dummy password hashing after unknown/ineligible account capacity exhaustion. Admission now checks retained installation and browser limits before real or dummy password work, with a final locked allocation check. Its source rereview accepted the correction. No operation or CI timeout was increased.

Test review added actual HTTP ordinary login, every-request revocation, observed two-node completion contention, audit-failure response withholding, lost-response/repost denial, independent migration maxima, exact ownership/revision controls, persisted session binding mutations and verified independent RFC vectors. Test selections validate every story and browser identity; full coverage requires all 35 selected inputs.

AppHost review corrected the existing pilot secret-family assertion and added all four flag combinations on both providers, complete protection forwarding, unique/shared secret references, malformed account settings and actual unmodified graph startup. The focused two-node fixture remains separate from standard graph evidence.

UX review added a neutral account badge, genuine keyboard traversal, invalid-factor focus/accessibility checks and browser recovery-page history checks. Secret elements are removed on pagehide when scripts are available, and restored sensitive documents navigate to current account status. Native no-script/history behavior still requires each browser's actual run; no inferred pass is recorded.

Local evidence so far: full solution build with zero warnings/errors; 34 PostgreSQL account cases and 31 AppHost composition cases passed with zero skips. The exact-inventory Accounts command passed all 34 cases with zero skips in 224.529 seconds; all 367 unit cases and 118 Python controls also passed. Browser-specific changes and final cleanup/format corrections still require exact-head CI. SQL Server and browser execution are delegated to the mandatory exact-head CI because this execution environment cannot download their required images/browser binaries. The C# inventory has zero new findings and removes 19 exact legacy entries (1,890 remain). No new allowance or exclusion was added. Final CI, browser/SQL evidence and PR merge are pending.
