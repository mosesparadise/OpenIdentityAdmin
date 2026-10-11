# Implementation Plan: Issuer lifecycle continuity

**Branch**: `014-issuer-lifecycle` | **Date**: 11 October 2026 | **Spec**: [spec.md](spec.md)

## Confirmed delivery

On 11 October 2026 the user selected the dedicated `tests/aspire/IdentityPlatform.Issuer.Lifecycle.Tests` project under the Aspire test solution folder, with `Features/Restart`, later `Features/Distribution` and `Features/KeyRotation`, and shared `Fixtures`. The user selected three sequential PRs: restart continuity → two-instance behavior → key overlap/retirement. Each increment is independently reviewed, verified and normally squash-merged before the next begins. PR #17 delivers restart only; M01-E remains partial afterward.

Existing v0.2 approval covers routine implementation. Any new material security-scope exception requires its own concrete amendment.

## Technical context and constitution check

Preserve pinned .NET/EF 10, OpenIddict 7.7.1, Aspire 13.6.1, xUnit v3/native MTP, both PostgreSQL and SQL Server, current TLS/PKCE/antiforgery/issuer rules and all nine 90% line/80% branch floors. Keep production Programs/composition/Features unchanged unless a demonstrated lifecycle defect requires a focused correction. No runtime-generated issuer credentials or fallback keys (normal encrypted Data Protection ring creation remains supported), generic repositories, custom token redemption or automatic security-operation retries.

The current HTTP issuer partition contains 292 cases per provider under a 660-second invocation deadline; its latest SQL duration was 523.495s, while earlier accepted checkpoints took 577.695s. Additional lifecycle operations should have their own bounded invocation, not consume uncertain headroom or enlarge the existing deadline. All old partitions and exact discovered/executed identities remain mandatory. Separate child-process coverage artifacts merge with the existing inputs; lifecycle tests cannot disappear from evidence selection.

Constitution: existing approval and ISS-003 cover the work; Features/test grouping, native ownership, real providers, protected evidence, Aspire, explicit gaps and independent reviewers apply. This is local/CI verification, not production deployment, backup recovery, human accessibility acceptance or certification. The user confirmed structure/sequencing. The specified full lifecycle goal is retained regardless of PR split.

## First increment: restart continuity

### Resource topology and ownership

Reuse the proven genuine HTTPS relying-party/login/token-validation fixtures and production AppHost graph. Keep one DistributedApplication, its database containers, least-privileged runtime connections, original RP process, private credential files and DP ring alive; replace only the `auth` child process. Bootstrap/Migrator must not rerun or repair state. Keep the canonical external issuer endpoint and registered callback byte-for-byte stable even if Aspire changes the child target port.

A test-only lifecycle helper captures the original launcher PID **and start time**, plus the actual AuthServer child and transitive descendants using Linux `/proc` PID/start-tick identities, awaits explicit stop completion and old process exit, starts the resource, then requires a new process generation and actual readiness. A stale Healthy notification, successful command result or a reused PID alone is insufficient. Capture metadata/JWKS and resource identities before/after. Reuse production startup, not TestServer or a newly initialized database. No protocol/request URL or credentials appear in failure output.

Existing fixture cleanup captures process generations only once. Refactor test-only lifecycle evidence to accumulate old/new generations and attempt final capture on restart failure before disposal. Serialize accumulator merges and final snapshots; concurrent notification/probe captures must not lose a generation. Treat zombie/dead processes as non-executing and fail on unreadable/malformed evidence. Preserve every captured child through bounded termination checks. Keep the order: stop/dispose hosts → release test services/RP/credential objects → remove private material → assert every captured container/process removed. Partial startup/stop/start/health timeout must still attempt all resources and produce bounded safe diagnostics. Existing fixture regression tests continue to cover the original path.

### Avoid false-positive continuity

`IssuerEnvironment.AuthorizeAsync` may automatically submit credentials when it sees a login page and updates expected session metadata. It is suitable for **pre-restart setup and explicit fresh recovery only**. It must not be the existing-cookie continuity oracle.

Capture the exact pre-restart ticket and original subject/session/authentication time. Drive original `prompt=none` manually after restart, prohibit form navigation/credential POST and replacement application tickets, and independently validate returned tokens against the captured identity. For pending-form continuity, submit the original antiforgery token and original browser cookies directly after restart; do not refresh the form first. For completed continuation, follow the exact retained server-provided continuation once and reject reuse/foreign-browser presentation. Compare relevant persisted state rather than treating an HTTP 200 as success.

### Proposed restart case inventory

| Case | Boundary and oracle | Requirement |
| --- | --- | --- |
| RST-01 | Eligible pre-restart cookie → manual silent authorization on replacement; exact original identity/auth_time, no credential entry/session replacement; genuine validated tokens. | LIFE-001/002/004 |
| RST-02 | Original pending form/antiforgery and cookies → replacement; genuine completion/exchange succeeds once. Tampered antiforgery and a foreign browser with its own valid form proof fail before owner consumption, with no session/token/interaction side effect; original-owner completion then succeeds. | LIFE-002/008 |
| RST-03 | Original completed interaction and continuation → replacement; same completed subject/session retained; foreign-browser control denies before owner consumption and preserves the owner's original continuation; reuse afterward also denies. | LIFE-002/003 |
| RST-04 | Pre-stop unredeemed code → replacement; exact original code/proof successfully redeems once; wrong proof, wrong client and wrong redirect remain denied on live bound codes, each followed by exact valid recovery. | LIFE-003 |
| RST-05 | Redeemed pre-stop code → replacement while still cryptographically unexpired; native replay denial and linked persisted revocation, not an expiry-only invalid_grant. | LIFE-003/005 |
| RST-06 | Existing public tokens → independently validate using public metadata obtained after replacement; compare public signing keys and original issuer/audience/nonce/lifetime. Tampered/wrong-key control fails. | LIFE-004 |
| RST-07 | Persisted revoked session, expired session and representative disabled-account state → replacement; original cookies fail silent issuance. Restore only the reversible account restriction and prove explicit fresh recovery. | LIFE-005 |
| RST-08 | Original authorization code reaches its actual unchanged expiry across restart and remains denied; a newly obtained valid flow succeeds. Preserve initial expiry observation, no clock/TTL relaxation. | LIFE-003/005 |
| RST-09 | Lifecycle helper requires old exit/new generation, stable origin/store identities and safe cleanup; seeded/no-op or stale-state observation cannot satisfy its predicate. | LIFE-001/008/009 |

This is an inventory, not a promised TRX count; parameterization and provider discovery will determine exact required identities before acceptance. Prefer narrow tests and isolated browser cohorts; no unrelated state mutation runs concurrently with restart. Retain positive controls and precise denial semantics.

### Time and identity evidence

Prepare longer-lived cookie/form/token cohorts before the shortest-lived artifacts; issue the live and previously redeemed code cohorts immediately before their restart. Inspect the native token entry's original creation/expiration/status where possible, correlating it uniquely to that case. Measure elapsed time monotonically and require a strict remaining-validity margin for success/replay controls. The 60-second lifetime is unchanged. If setup/restart overruns, the case fails; do not reissue a code, retry a security mutation, extend expiry or classify an expired replay as success. Expiry tests wait against the original recorded lifetime rather than resetting it.

Stop/start/readiness operations have explicit cancellation budgets bounded inside the lifecycle invocation. Measure and review those budgets during the first real-provider experiment; preserve existing 660-second provider invocation bounds. Restart observations are test-run measurements, not a production RTO/SLA.

## Confirmed project and harness reuse

Add the dedicated project to `/tests/aspire/` solution grouping and use existing native MTP properties/locked dependencies. Split the reusable `IssuerEnvironment.cs` from `IssuerFixture.cs` collection declarations. Source-link that environment, `TestRelyingParty.cs`, `TestLogin.cs`, `IssuerMaterialFixture.cs`, `SafeAssertions.cs`, `ProcessTreeEvidence.cs` and `LifecycleEvidence.cs` into the new project, retaining the source-owning namespaces. No executable test-project reference, browser suite/package or new generic support library is needed. A partial lifecycle helper is compiled only by the new project.

Register matrix project `Issuer.Lifecycle` in each existing provider profile. Run it as its own <=660-second invocation under the existing provider child-process coverage collection; retain the existing nine coverage input artifacts. Strict discovered provider partitions and executed identity checks cover this project, with zero browser cases. Require both provider catalogues at their reviewed counts. Validate row outcomes, unique test/result identities, definition/result correspondence, summary and run errors as well as total/passed counters; reject inconsistent TRX. Extend selection tests for missing/duplicate/skipped/wrong-provider evidence, retaining every old partition/case/floor. Existing 20-minute provider CI job budget is unchanged and must accommodate the measured increment; failure requires investigation, not an automatic budget increase.

## Later increments retained

**Distribution:** write a concrete trusted HTTPS canonical-origin routing contract and node-affinity/provenance proof before code; no implicit trust of spoofed forwarding headers. Run two actual issuer processes with shared least-privileged stores/DP material. Specify cross-node form/cookie/code/replay/current-state/failure cases and race coordination; no in-process substitute.

**Key rotation:** current production configuration has one signing and one encryption certificate, while only DP wrapping supports previous decryptors. Write and independently review the compatible configuration/activation contract first. Do not use OpenIddict certificate registration order as policy. Define public prepublication, explicit active selection, historical decryptors, maximum artifact lifetimes, clock skew/JWKS cache windows, restart sequencing, safe negative cases and operator rollback limits. No active/staged/retiring production syntax is invented in this initial plan.

## Validation and delivery

Before implementation: record the confirmed structural/sequencing choices, complete the spec checklist, run Spec Kit prerequisites and independent architecture/security/test analysis. For each increment: compile/analyzers/format, meaningful helper tests, exact real-provider lifecycle cases, existing provider/browser regression, child coverage and normal supply/assurance checks. Source-only review never establishes lifecycle behavior.

Record exact commands, case identities/counts, provider versions, source/merge tree, observed durations, failure corrections, reviewer dispositions and cleanup evidence in `verification.md`. Update the roadmap, issuer backlog and runbook after actual acceptance. Keep T021 manual assessment, remaining M01-E stories, M01-F and all public-release gates explicit. Use Conventional Commit PR titles and normal expected-head squash merges; no self-approval or protection bypass.
