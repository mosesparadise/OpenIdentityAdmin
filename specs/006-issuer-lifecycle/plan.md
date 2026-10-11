# Implementation Plan: Issuer lifecycle continuity

**Branch**: `014-issuer-lifecycle` | **Date**: 11 October 2026 | **Spec**: [spec.md](spec.md)

## Summary and pending choices

M01-E is approved product scope. The user explicitly requested questions about new project/file structure. Before implementation, confirm:

1. **Recommended:** `tests/aspire/IdentityPlatform.Issuer.Lifecycle.Tests`, grouped under the Aspire test solution folder, with `Features/Restart`, later `Features/Distribution` and `Features/KeyRotation`, and `Fixtures`. Alternative: keep lifecycle cases in the current Issuer.Tests project and create explicit isolated execution partitions.
2. **Recommended:** three sequential PRs, restart continuity → two-instance behavior → key overlap/retirement, each independently reviewed, verified and normally squash-merged before the next begins. Alternative: one combined M01-E PR.

These recommendations are proposals, not assumed answers. The current branch contains design documentation only. No production topology/configuration, project or test-runner change is authorized by a preselected question option. Once confirmed, routine implementation remains covered by existing approval; a new security-scope exception would require its own concrete amendment.

## Technical context and constitution check

Preserve pinned .NET/EF 10, OpenIddict 7.7.1, Aspire 13.6.1, xUnit v3/native MTP, both PostgreSQL and SQL Server, current TLS/PKCE/antiforgery/issuer rules and all nine 90% line/80% branch floors. Keep production Programs/composition/Features unchanged unless a demonstrated lifecycle defect requires a focused correction. No runtime key generation, generic repositories, custom token redemption or automatic security-operation retries.

The current HTTP issuer partition contains 292 cases per provider under a 660-second invocation deadline; its latest SQL duration was 523.495s, while earlier accepted checkpoints took 577.695s. Additional lifecycle operations should have their own bounded invocation, not consume uncertain headroom or enlarge the existing deadline. All old partitions and exact discovered/executed identities remain mandatory. Separate child-process coverage artifacts merge with the existing inputs; lifecycle tests cannot disappear from evidence selection.

Constitution: existing approval and ISS-003 cover the work; Features/test grouping, native ownership, real providers, protected evidence, Aspire, explicit gaps and independent reviewers apply. This is local/CI verification, not production deployment, backup recovery, human accessibility acceptance or certification. New structure/sequencing decisions remain pending. The specified full lifecycle goal is retained regardless of PR split.

## First increment: restart continuity

### Resource topology and ownership

Reuse the proven genuine HTTPS relying-party/login/token-validation fixtures and production AppHost graph. Keep one DistributedApplication, its database containers, least-privileged runtime connections, original RP process, private credential files and DP ring alive; replace only the `auth` child process. Bootstrap/Migrator must not rerun or repair state. Keep the canonical external issuer endpoint and registered callback byte-for-byte stable even if Aspire changes the child target port.

A test-only lifecycle helper captures the original PID **and start time**, awaits explicit stop completion and old process exit, starts the resource, then requires a new process generation and actual readiness. A stale Healthy notification, successful command result or a reused PID alone is insufficient. Capture metadata/JWKS and resource identities before/after. Reuse production startup, not TestServer or a newly initialized database. No protocol/request URL or credentials appear in failure output.

Existing fixture cleanup captures process generations only once. Refactor test-only lifecycle evidence to accumulate old/new generations and attempt final capture on restart failure before disposal. Keep the order: stop/dispose hosts → release test services/RP/credential objects → remove private material → assert every captured container/process removed. Partial startup/stop/start/health timeout must still attempt all resources and produce bounded safe diagnostics. Existing fixture regression tests continue to cover the original path.

### Avoid false-positive continuity

`IssuerEnvironment.AuthorizeAsync` may automatically submit credentials when it sees a login page and updates expected session metadata. It is suitable for **pre-restart setup and explicit fresh recovery only**. It must not be the existing-cookie continuity oracle.

Capture the exact pre-restart ticket and original subject/session/authentication time. Drive original `prompt=none` manually after restart, prohibit form navigation/credential POST and replacement application tickets, and independently validate returned tokens against the captured identity. For pending-form continuity, submit the original antiforgery token and original browser cookies directly after restart; do not refresh the form first. For completed continuation, follow the exact retained server-provided continuation once and reject reuse/foreign-browser presentation. Compare relevant persisted state rather than treating an HTTP 200 as success.

### Proposed restart case inventory

| Case | Boundary and oracle | Requirement |
| --- | --- | --- |
| RST-01 | Eligible pre-restart cookie → manual silent authorization on replacement; exact original identity/auth_time, no credential entry/session replacement; genuine validated tokens. | LIFE-001/002/004 |
| RST-02 | Original pending form/antiforgery and cookies → replacement; genuine completion/exchange succeeds once. Foreign browser fails with no session/token side effect. | LIFE-002/008 |
| RST-03 | Original completed interaction and continuation → replacement; same completed subject/session retained; one-use/foreign-browser controls deny. | LIFE-002/003 |
| RST-04 | Pre-stop unredeemed code → replacement; exact original code/proof successfully redeems once; wrong proof remains denied. | LIFE-003 |
| RST-05 | Redeemed pre-stop code → replacement while still cryptographically unexpired; native replay denial and linked persisted revocation, not an expiry-only invalid_grant. | LIFE-003/005 |
| RST-06 | Existing public tokens → independently validate using public metadata obtained after replacement; compare public signing keys and original issuer/audience/nonce/lifetime. Tampered/wrong-key control fails. | LIFE-004 |
| RST-07 | Persisted revoked session, expired session and representative disabled-account state → replacement; original cookies fail silent issuance. Restore only the reversible account restriction and prove explicit fresh recovery. | LIFE-005 |
| RST-08 | Original authorization code reaches its actual unchanged expiry across restart and remains denied; a newly obtained valid flow succeeds. Preserve initial expiry observation, no clock/TTL relaxation. | LIFE-003/005 |
| RST-09 | Lifecycle helper requires old exit/new generation, stable origin/store identities and safe cleanup; seeded/no-op or stale-state observation cannot satisfy its predicate. | LIFE-001/008/009 |

This is an inventory, not a promised TRX count; parameterization and provider discovery will determine exact required identities before acceptance. Prefer narrow tests and isolated browser cohorts; no unrelated state mutation runs concurrently with restart. Retain positive controls and precise denial semantics.

### Time and identity evidence

Prepare longer-lived cookie/form/token cohorts before the shortest-lived artifacts; issue the live and previously redeemed code cohorts immediately before their restart. Inspect the native token entry's original creation/expiration/status where possible, correlating it uniquely to that case. Measure elapsed time monotonically and require a strict remaining-validity margin for success/replay controls. The 60-second lifetime is unchanged. If setup/restart overruns, the case fails; do not reissue a code, retry a security mutation, extend expiry or classify an expired replay as success. Expiry tests wait against the original recorded lifetime rather than resetting it.

Stop/start/readiness operations have explicit cancellation budgets bounded inside the lifecycle invocation. Measure and review those budgets during the first real-provider experiment; preserve existing 660-second provider invocation bounds. Restart observations are test-run measurements, not a production RTO/SLA.

## Proposed project and harness reuse

If the dedicated project is selected, follow the current solution's Aspire test grouping and project properties. Avoid copying business or cryptographic logic. Reuse only the narrow RP/form/environment support required by the lifecycle tests, with its ownership/refactoring and dependent regression made explicit before editing. Options under review are source-linked fixture support (existing repository convention) versus an extracted test-support component; do not reference an executable test project merely to inherit its full suite/dependencies. No new shared library or fixture movement is decided without resolving the user's structural choice.

Each provider has a distinct lifecycle invocation/result directory and child-process coverage input. Extend `eng/test-matrix.json`, the appropriate runner, exact discovery/execution verification and CI required artifact inventory together. Test the selection/evidence gates with missing, duplicate, skipped and wrong-provider reports. Preserve the ordinary issuer/browser partition and all nine floors. Do not increase the original HTTP deadline or silently select lifecycle cases out of CI.

## Later increments retained

**Distribution:** write a concrete trusted HTTPS canonical-origin routing contract and node-affinity/provenance proof before code; no implicit trust of spoofed forwarding headers. Run two actual issuer processes with shared least-privileged stores/DP material. Specify cross-node form/cookie/code/replay/current-state/failure cases and race coordination; no in-process substitute.

**Key rotation:** current production configuration has one signing and one encryption certificate, while only DP wrapping supports previous decryptors. Write and independently review the compatible configuration/activation contract first. Do not use OpenIddict certificate registration order as policy. Define public prepublication, explicit active selection, historical decryptors, maximum artifact lifetimes, clock skew/JWKS cache windows, restart sequencing, safe negative cases and operator rollback limits. No active/staged/retiring production syntax is invented in this initial plan.

## Validation and delivery

Before implementation: resolve structural/sequencing choices, complete the spec checklist, run Spec Kit prerequisites and independent architecture/security/test analysis. For each increment: compile/analyzers/format, meaningful helper tests, exact real-provider lifecycle cases, existing provider/browser regression, child coverage and normal supply/assurance checks. Source-only review never establishes lifecycle behavior.

Record exact commands, case identities/counts, provider versions, source/merge tree, observed durations, failure corrections, reviewer dispositions and cleanup evidence in `verification.md`. Update the roadmap, issuer backlog and runbook after actual acceptance. Keep T021 manual assessment, remaining M01-E stories, M01-F and all public-release gates explicit. Use Conventional Commit PR titles and normal expected-head squash merges; no self-approval or protection bypass.
