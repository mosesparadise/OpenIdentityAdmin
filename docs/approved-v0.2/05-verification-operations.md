# OpenIddict Verification and Operations Plan Draft

Release evidence must demonstrate protocol behavior, administrative authorization, identity recovery and equivalent database behavior. A passing build alone is insufficient for an identity platform. This plan defines the tests and operational exercises to add after documentation approval; none have been executed against a new implementation.

## Test strategy

Use focused unit tests for policy and state-machine behavior; integration tests through real host boundaries for protocol and management behavior; real PostgreSQL and SQL Server for persistence/concurrency/migrations; and Playwright for the browser journeys. EF InMemory or SQLite does not replace either required provider. Mock external mail/provider boundaries where useful, with a separate real OIDC integration exercise.

| Suite | Required scenarios | Related requirements |
| --- | --- | --- |
| Protocol code flow | Valid code and PKCE, wrong verifier, reused code, mismatched client, invalid redirect, missing permission, unsupported response | FR-02,04,09 |
| Authorization interaction | prompt none/login/consent, max_age, fresh authentication, consent denial, changed requested scopes, lost/expired interaction and no redirect loop | FR-07–09 |
| Refresh | Rotation, replay, concurrent refresh, narrowed scopes, changed claims/roles, disabled user/client, expired session, revoked authorization | FR-03,05,09–11 |
| Machine access | Valid credentials, no user impersonation, credential replacement/expiry, forbidden grant/scope/audience and client assertion checks | FR-02,03,09 |
| Device and PAR | Approval/denial, code expiry/replay, polling/slow_down, wrong client, PAR reference tampering/expiry and required PAR | FR-02,09 |
| UserInfo and resource access | Scope-filtered claims, matching subject, wrong audience/issuer, ID token rejection, no roles/security-stamp leakage | FR-04,09,11 |
| Logout and revocation | Valid/invalid hints and redirects, local/remote failure, CSRF, token vs authorization vs session effects | FR-08–11 |
| Administration | Every route's permission policy, direct HTTP bypass attempts, stale write, duplicate IDs, reserved claims, last-admin race and audit atomicity | FR-01–06,12 |
| BFF | No browser tokens, anti-forgery, forged headers, fixed proxy targets, session expiry, two-node refresh coordination and login/logout behavior | FR-11 |
| Identity | Lockout, enumeration-resistant recovery, single-use codes, TOTP/recovery, passkey RP/origin checks, linking proof and privileged MFA | FR-05–07,14 |
| Configuration rules | Each rule's positive/negative case, issue deduplication/resolution, concurrent workers and bounded job retries | FR-13 |
| Data providers | All native/product mappings, transactions, collations, concurrency, cleanup, migration fresh/upgrade and manager cache behavior | FR-17 |
| Browser journeys | Client registration, role edit, user disable, grant revoke, audit search, one-time secret display, accessibility and empty/error states | FR-01–08,10–13,19 |
| Operations | Multi-instance behavior, TLS proxy, key rotation, backup/restore, DB outage, provider outage, audit/email delivery outage | FR-12,15,17,18 |

R1 requires the pinned OpenID Foundation Basic OP and Config OP conformance plans, the certification evidence workflow and applicable ASVS 5.0.0 L3 independent assessment described in the assurance companion. Passing selected tests does not itself grant certification. Independent security review includes custom protocol, authorization, credential and account-recovery code.

## Two-provider CI matrix

Every persistence-related pull request runs the same semantic tests once against PostgreSQL and once against SQL Server, including application and scope uniqueness, If-Match conflicts, grant revocation, audit transactions and concurrent last-administrator changes. Release CI performs full browser/protocol tests against both complete deployments. Pin supported database versions in the initial feature plan, record images/licensing/resource requirements and update them through an explicit compatibility change.

Initial setup must prove both providers, rather than implementing one and deferring the other. Migration verification creates an empty database, applies all migrations, seeds via supported initialization, upgrades a recorded prior schema fixture (and the previous released version once one exists), verifies the model and tests the upgraded system. Forward migrations are the production strategy; destructive rollback is a restore/runbook exercise, not an assumed Down method.

Generated OpenAPI and TypeScript output must be reproducible; CI rejects unexpected contract drift. Add package-lock/central-package checks, dependency scanning, secret scanning, a license inventory and architecture constraints. A source review must ensure no protocol endpoint silently uses an MVC controller or bypasses OpenIddict response handling.

## Operational deployment baseline

Deploy AuthServer, Admin API, Admin BFF and Worker as separately configured processes/containers. Bind each environment to a canonical HTTPS issuer. Expose the admin surface only to its intended audience/network. Use trusted proxy lists, secure cookie settings, HSTS where applicable and distinct secrets per environment. Developer launch profiles must never become production credential sources.

Persist Data Protection key rings and BFF sessions across instances/restarts. Protect Data Protection keys at rest and keep application discriminators separate between BFF and AuthServer unless a justified shared-cookie design is approved. Store token signing and encryption credentials separately from those rings. Monitor certificate expiry and rehearse overlap: publish a new signing key before use, retain retiring public/decryption keys long enough, then retire deliberately.

Run migrations once as a release job with schema credentials. Web hosts start with runtime credentials and check schema compatibility. Use backward-compatible expansion followed by contraction in a later release where rolling deployment requires it. Bootstrap creates an initial admin and first-party client only from operator-supplied one-time inputs; it is idempotent and auditable, with no well-known production password.

Readiness checks cover required database/schema and credential availability; liveness checks avoid dependence on transient external services. Report email/provider degradation separately so a temporary mail failure does not restart the identity service continuously. Health responses do not disclose connection strings or private infrastructure data to anonymous callers.

## Observability and recovery

Collect request duration/error rate, token-grant outcomes, login/MFA failures, client-auth failures, refresh replay events, DB latency, job backlog, audit delivery failures, key expiry and configuration-change failures. Use low-cardinality labels; do not place raw user IDs, client-provided strings or token values in unrestricted metric labels. Correlate events with opaque request IDs and protect sensitive logs.

Alert on sustained elevated authentication errors, stuck audit/outbox queues, repeated privilege denials, absent worker progress, certificate expiry and failing backups. Distinguish user mistakes from service faults. Audit failures for administrative changes must not be silently discarded; the transaction either records its intent or does not commit the mutation.

The restore exercise covers the database, external key references/material, Data Protection rings, configuration and deployed version. Test restoration separately for both providers. Revalidate the audit trail and token/session state after restoring an older snapshot: a previously revoked token may reappear as valid in restored data, so revoke affected grants/sessions or rotate appropriate credentials and document reauthentication. Demonstrate the proposed 15-minute RPO and 60-minute RTO using actual recorded times.

## Performance verification

Use the PRD's published dataset, resource allocation and request rates. Include database-backed token/authorization checks in token-validation measurements; do not disable them to meet latency numbers. Report warm-up, concurrency, duration, connection-pool settings, token algorithms, refresh mix and p50/p95/p99 latency with errors for each provider. Include a two-node five-second revocation/permission-change test during load.

If the reference hardware or load proves unrealistic, revise the approved performance target with evidence. Caching cannot be introduced solely to improve a benchmark while losing the stated cross-process enforcement behavior.

## Release checklist

- Every R1 requirement links to passing verification evidence for both providers.

- Every protocol response and critical identity flow has positive and negative coverage.

- No unexplained OpenAPI drift, missing migration, unsupported dependency or outstanding critical/high exploitable finding remains.

- The four primary product journeys pass product review, including accessibility checks.

- Clean install, rolling restart, key rotation and restore runbooks are demonstrated.

- All deployment-specific choices are recorded: DNS/issuer, provider/version, secret store, mail/provider settings, retention, backups and incident owners.

- Spec Kit analysis and convergence report no unbuilt approved requirements; identified gaps remain tasks, not silently deferred scope.

- Production deployment, public certification submission/payment and legal declarations have their own authorized execution plan.

## Comprehensive coverage and regression policy

Every first-party project, feature, exposed endpoint, background job, startup path and security invariant must appear in the coverage register. Link each approved FR/NFR and acceptance criterion to test IDs, level, provider/browser applicability, CI job and evidence. Require 100% traceability of accepted criteria and critical security decisions, with explicit applicability decisions instead of silently untested areas. This is a coverage obligation, not a promise that finite tests prove absence of defects.

| Boundary | Required coverage |
| --- | --- |
| Core feature policy | Unit and targeted property-based tests for permissions, claim release, normalization, scope intersection, expiry and state transitions |
| AuthServer and Razor Pages | Host integration tests for middleware/schemes, protocol and form behavior; actual browser tests for login, consent, MFA, passkeys, recovery, device confirmation and logout |
| Admin API and Contracts | Success and applicable unauthenticated, forbidden, invalid, missing, duplicate, stale-write, concurrency, cancellation and dependency-failure cases for every endpoint; serialization and versioned contract regression |
| Admin BFF | Cookie/CSRF/proxy boundaries, server-held tokens, callback binding, session expiry, concurrent refresh, multiple tabs, forged headers and no browser credential leakage |
| React features | Vitest and Testing Library component tests with user interactions, forms, state/error handling and permission display; Playwright for integration with real hosts |
| Infrastructure and both providers | Native manager persistence, collation, indexes, transactions, uniqueness, optimistic concurrency, worker leases and cross-process cache visibility on real PostgreSQL and SQL Server |
| Worker and integrations | Retry/idempotency, poisoned work, partial failures, shutdown/cancellation, leasing, cleanup eligibility and controlled email/federation boundaries |
| Migrator and bootstrap | Empty install, idempotent initialization, failure/restart, protected bootstrap, schema mismatch and future product upgrades on both providers |
| AppHost and ServiceDefaults | Resource graph, endpoint/issuer propagation, readiness, health, telemetry redaction, cleanup and deliberate retry/timeouts |
| Design system and docs UIs | React/Razor theme fidelity, responsive states, accessibility, visual snapshots and equivalent Scalar/Swagger versioned contracts |
| Deployment and operations | Restart, key rotation, secrets/config validation, rate limits, resource exhaustion, backup/restore and recovery behavior |

Use WebApplicationFactory for in-process host seams and middleware tests. Use Aspire-hosted tests for actual process/resource boundaries; neither substitutes for the other. Out-of-process child services need explicit coverage instrumentation if their execution contributes to reported coverage.

Automated E2E tests exercise browser → Admin BFF → Admin API → STS and the two databases through separate complete provider profiles. Include account recovery through a local mail sink and federation through a controlled OIDC fixture. Authentication shortcuts may prepare unrelated scenarios but do not replace real login, consent, MFA and logout tests. Use virtual WebAuthn authenticators where supported and retain explicit browser/OS checks for platform behavior automation cannot reproduce. Never weaken production authentication to make tests easier.

Test boundaries include exact expirations with a controllable clock where feasible, back/refresh/multiple tabs, slow/lost network, duplicate submit, stale edits, disabled users/clients, lost one-time-secret responses, worker races and two-node session/refresh/revocation behavior. Generate synthetic data and isolate every fixture/run.

Every reproducible defect receives a regression that demonstrably fails before its fix and passes afterward. Security and concurrency regressions remain permanent gates. Retries expose flakiness; a pass on retry is not a clean reliable result. Quarantine requires an owner, issue, expiry and equivalent blocking coverage for critical behavior. No unresolved failing or skipped required test may ship.

Proposed minimum execution-coverage floors are 90% lines and 80% branches for handwritten first-party code, enforced per substantive project/module so a well-covered area cannot hide an untested one. Security-critical decisions require explicit allow/deny/boundary scenarios and targeted mutation testing even when numeric floors pass. Generated clients, generated migration boilerplate and third-party code may be excluded with a documented reason; their contract/migration behavior still needs tests. Coverage does not replace meaningful assertions, review or threat analysis.

## Automation cadence and evidence

| Stage | Required gates |
| --- | --- |
| Every pull request | Build/typecheck/analyzers/formatting; unit/component/architecture checks; affected protocol/contract/provider integrations on both databases; critical Chromium journeys against both complete stacks; security/dependency/secret checks |
| Nightly and dependency changes | Full Chromium/Firefox/WebKit × provider matrix, visual/accessibility checks, fault/concurrency suites and targeted mutation testing; compatible-toolchain regression |
| Release candidate | Complete requirements matrix, all mandatory tests without unexplained skips, published performance workload, key rotation, both-provider backup/restore, independent security review and current compliance evidence |

Store sanitized reports, coverage, Playwright traces/screenshots and startup logs as access-controlled CI artefacts with defined retention. Browser traces, videos and DOM snapshots may contain one-time secrets, recovery codes or cookies; use synthetic accounts, minimize recording of sensitive steps, redact where feasible and restrict raw artefacts. Baseline visual changes require review. A dependency bump and an API-version change both require regression evidence.

Release-candidate E2E also runs the published container/assets and production configuration on both providers, including reverse proxy, CSP, cookies, anti-forgery, authorization and disabled debug/public-doc settings. Passing only an Aspire development profile is insufficient. Leakage assertions distinguish prohibited BFF OAuth/client-authentication credentials from authorized one-time display of a newly issued managed-application secret; the latter must never persist in browser storage, logs or caches.


## Aspire local development and distributed testing

Add IdentityPlatform.AppHost, IdentityPlatform.ServiceDefaults and IdentityPlatform.AppHost.Tests after approval. AppHost orchestrates the selected SQL Server or PostgreSQL profile, schema/seed job, AuthServer, Admin API, Admin BFF, React development workflow, Worker, local mail sink and controlled OIDC test provider. Aspire is required local/test tooling; this decision does not choose a production hosting platform.

The browser enters through the BFF's HTTPS origin. In development, proxy the React dev assets through that origin or configure a single documented equivalent without moving OAuth tokens into JavaScript. Register exact callback/logout URLs and a consistent canonical issuer across AuthServer, BFF and API. Local certificates must be trusted appropriately; do not disable issuer, audience, TLS or PKCE validation as a testing convenience.

Use Aspire.Hosting.Testing and DistributedApplicationTestingBuilder to build/start the AppHost, wait for actual resource health with bounded cancellation/deadlines, resolve HTTP clients/endpoints, then exercise real service boundaries and dispose the application/resources. Running is not equivalent to ready. Tests use isolated ephemeral databases/volumes and bounded parallelism; development persistent volumes are never reused by CI.

Aspire tests may randomize proxied ports. Resolve the test issuer and callback addresses before configuring clients and keep them consistent for that environment. Never compensate with wildcard redirect URIs or disabled HTTPS checks. AppHost dependency injection does not replace services inside child processes; use host integration tests for those seams and explicit test resource/configuration profiles for distributed tests.

ServiceDefaults centralizes safe telemetry, health and service-discovery conventions with explicit opt-in registration. Do not apply generic HTTP retries to authorization-code exchange, refresh-token rotation, credential changes or other non-idempotent security operations. Fault tests intentionally stop/restart a dependency and prove the expected failure/recovery behavior without retries hiding failures.

CI requires a supported container runtime, image access and enough CPU/memory for each full stack. Record the compatible stable Aspire SDK/packages, runtime and database images at kickoff. Collect sanitized diagnostics on startup failures, enforce timeouts and teardown, and verify both provider profiles locally and in CI. A local dashboard is useful diagnostics and must not expose credentials or become an unauthenticated production management surface.

Primary references: [Aspire test overview](https://github.com/microsoft/aspire.dev/blob/main/src/frontend/src/content/docs/testing/overview.mdx), [first distributed test](https://github.com/microsoft/aspire.dev/blob/main/src/frontend/src/content/docs/testing/write-your-first-test.mdx), [resource access](https://github.com/microsoft/aspire.dev/blob/main/src/frontend/src/content/docs/testing/accessing-resources.mdx), [CI guidance](https://github.com/microsoft/aspire.dev/blob/main/src/frontend/src/content/docs/testing/testing-in-ci.mdx). Pin and validate APIs against the selected release; current documentation is not a substitute for a tested dependency lock.


## Complete requirement coverage allocation

| Requirement group | Mandatory evidence owner and suites |
| --- | --- |
| FR-01–04 | Application/scope features: API/component/browser, policy/property, credential, claim, concurrency and both-provider tests |
| FR-05–08,14 | Identity/consent/federation: account lifecycle, every role boundary, recovery/MFA/passkeys, external linking, browser and protocol interaction tests |
| FR-09–11 | Protocol/BFF/access lifecycle: full endpoint/grant matrix, OIDF plans, proxy/CSRF, two-node refresh/revocation/session tests |
| FR-12,13,15 | Audit/rules/keys: atomicity/redaction, job failures/races, every rule positive/negative, inventory/expiry/rotation and operations UI |
| FR-16 | API governance: version/error/header semantics, schema/client diff checks, both documentation UIs and tested integration examples |
| FR-17,18,24 | Persistence/platform: both-provider fresh schema/bootstrap/upgrade/restore, Aspire startup/health/teardown and service-default behavior |
| FR-19 | React/Razor/design: states, responsive theme snapshots, keyboard/screen reader, automated accessibility and browser matrix |
| FR-21–23 | Engineering/test/AI: coverage register, regression proof, architecture/analyzers, seeded reviewer evaluations and finding disposition |
| FR-25 | Security owner: OIDF evidence, applicable ASVS register, independent assessment and submission status |
| NFR-01,08,13 | Security/compliance: scans, threat/abuse tests, no-secret proofs, controls and independent assessment |
| NFR-02–07,10,14 | Platform/operations: provider equivalence, measured dataset/load, revocation under load, restore/availability monitors, retention and reproducibility |
| NFR-09,11,12 | UX/test owners: timed primary journeys, accessibility, complete traceability and reliable regression/visual matrix |

FR-20 is the explicit later-release backlog, not an untested R1 promise. This allocation is a planning matrix; feature plans expand it into individual scenario/test IDs and results. Required coverage applies to all implemented routes and jobs, including those absent from illustrative contract tables. Performance, conformance and manual evidence retain their own result types and are not counted as unit-test coverage.
