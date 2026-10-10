# OpenIddict Administration Platform PRD Draft

Build a new identity administration platform using OpenIddict and ASP.NET Core Identity, with a React and TypeScript administration interface. Organize application behavior as vertical slices under Features within each project. Use Minimal APIs for custom protocol handlers and management APIs. OpenIddict remains responsible for protocol validation and token production.

Status: Draft for approval, version 0.2, 10 October 2026. Development is not authorized. The product owner is the requesting user; engineering and security owners will be assigned before implementation. The working product name is IdentityPlatform.

## Documentation for approval

Read the PRD first, then the companion designs. Approve version 0.2 as a package or identify amendments before development.

See [package contents and review status](README.md) — downloadable documentation and instruction-only AI skill/subagent drafts for review. No application implementation is included.

- [Skoruba Repository Review and OpenIddict Feature Mapping](https://chatgpt.com/space/page_f81a613dd5b881919f6422f5082d9db1)

- [OpenIddict Vertical Slice Architecture Draft](https://chatgpt.com/space/page_d266311fdc30819193d02a59a5a92dbf)

- [OpenIddict Minimal API Protocol and Security Design Draft](https://chatgpt.com/space/page_f3c39ad884688191a9aa7be28e6f4b48)

- [Greenfield Data and Versioned API Contracts](https://chatgpt.com/space/page_2e65c64a6b888191b3e62028d26e259f)

- [OpenIddict Verification and Operations Plan Draft](https://chatgpt.com/space/page_b4b6e74c9c90819183e0531a699dbe2f)

- [GitHub Spec Kit Implementation Plan and Constitution Draft](https://chatgpt.com/space/page_2f68ecab82188191aabd29a06008611b)

## Confirmed decisions

- Build a fully greenfield system. Duende data migration, import and cutover are outside scope; Skoruba remains a product reference.

- Use React and TypeScript through an Admin BFF, with server-held OAuth tokens and same-origin secure browser sessions.

- Support PostgreSQL and SQL Server equally in the first production release.

- Apply Vertical Slice Architecture using Features per project.

- Use GitHub Spec Kit for implementation after approval of this documentation package.

- Wait for explicit document approval before scaffolding, proofs of concept, application code, migrations, or implementation tests.

## Product outcome

Administrators can register applications, define scopes and API audiences, manage users and roles, inspect grants, revoke access, and investigate changes without editing database records. Application developers can integrate using documented OIDC and OAuth flows. Operators can deploy and recover the platform on either supported database provider. The React admin and STS Razor Pages share a modern enterprise slate/indigo design with light/dark modes.

The reference is Skoruba Duende.IdentityServer.Admin 3.2.0 at commit `06386cc726f1218d45cef82baad3f1692489300c`. Replication means comparable administration journeys and explicitly agreed protocol capabilities. It does not mean preserving every Duende field, database table, REST contract, or screen. The companion repository review records the complete proposed disposition of the observed feature areas.

## Users and responsibilities

| Persona | Main jobs | Default access |
| --- | --- | --- |
| Platform administrator | Bootstrap the system, assign administrators, configure clients and identity policy | Full management, with recent MFA for sensitive changes |
| Application administrator | Register and maintain applications, scopes, audiences | Configuration management; no user or administrator-role management |
| Identity administrator | Manage users, roles, recovery and grants | Identity management; cannot grant platform administration without explicit permission |
| Auditor | Investigate changes and authentication events | Read-only, with sensitive values redacted |
| Operator | Deploy, monitor, rotate keys and restore backups | Operational access separately controlled from application administration |
| End user | Sign in, consent, recover access and manage authenticators | Own account and own grants only |
| Integration developer | Obtain configuration and examples for a supported flow | Own development environment; production access separately granted |

Initial roles are permission bundles. API policies evaluate permissions server-side. Record actor identity for user operations and client identity for approved automation. Initial delegated administration is platform-wide; ownership-scoped or tenant-scoped administration is later scope.

## Release boundaries

Release 1 is a production foundation with the requirements below. Earlier milestones are internal demonstrations and must not be described as production-ready. Release 1.1 expands parity with dynamic external-provider administration, richer session control across relying parties, and reusable templates. OpenID Foundation Basic OP and Config OP certification is the R1 protocol target. Advanced financial-grade profiles require their own design and verification.

Release 1 includes authorization code with PKCE, refresh tokens, client credentials, device authorization, PAR, discovery, JWKS, UserInfo, introspection, revocation and RP-initiated logout. Resource owner password, implicit and hybrid grants are disabled by product policy even if a library supports them. SAML, SCIM, multi-tenant issuers, arbitrary custom grants, token exchange, DPoP, mTLS and FAPI conformance are outside Release 1.

## Primary user journeys

### Register a web application

An application administrator selects a confidential web client template, enters a unique client ID, exact callback URLs and required scopes, and reviews the resulting grant and endpoint permissions. Saving creates an audited configuration. A newly issued shared secret appears once; an asymmetric client credential is supported for integrations that can use it. The integration screen provides configuration with secret placeholders. The developer proves login and API access using that registration.

Acceptance: the same journey passes on both databases; a public client cannot receive a secret or use client credentials; missing PKCE and unregistered redirect URIs fail; the auditor can identify who created the client without seeing its credential.

### Change access safely

An identity administrator disables a user or revokes a grant. The operation displays its effect on login sessions, refresh tokens and existing access tokens. New login and token issuance stop. The platform's Admin API rejects subsequent requests within the published enforcement window. Third-party APIs using offline token validation may continue accepting already issued tokens until expiry.

Acceptance: there is no promise of instantaneous global logout. Cross-process tests prove the documented window and behavior after database or identity-server failure.

### Recover an administrator account

An administrator uses a registered passkey or password plus TOTP, with single-use recovery codes as a fallback. Recovery and authenticator changes require the applicable identity proof, expire quickly and generate audit events. Bootstrap creates one initial administrator through an operator-controlled, single-use process.

Acceptance: no default production password, no reusable bootstrap endpoint, no privileged account creation through public registration, and no removal of the last usable platform administrator without a documented recovery route.

### Investigate a configuration issue

An auditor views a dashboard, follows a failed configuration rule to a client or scope, and compares the recorded changes. An authorized administrator fixes the configuration, reruns the relevant rule and sees the issue resolve. The rules engine evaluates a fixed, versioned set of rules; it does not execute administrator-supplied code.

## Functional requirements

All requirements marked R1 are release blockers. Later requirements are explicit parity backlog items.

| ID | Requirement | Release and acceptance |
| --- | --- | --- |
| FR-01 | Application catalogue with search, paging, details, create, edit, clone, disable and controlled delete | R1. Client IDs are unique and immutable after creation. Clone omits credentials and grants. Referenced records cannot be silently deleted. |
| FR-02 | Guided web, browser, native, machine and device client profiles | R1. Every profile produces only compatible grants, response types, endpoint permissions, redirects and credential settings. |
| FR-03 | Credential issuance, replacement and asymmetric public-key registration | R1. A secret is shown once and hashed through OpenIddict managers. Initial shared-secret replacement invalidates the old secret immediately. Overlapping shared secrets are later scope. |
| FR-04 | API scopes, API audience catalogue and identity scopes with claim-release rules | R1. Requested scopes and audiences are validated, consented and reflected in tokens. Adding a display field alone cannot enable claim release. |
| FR-05 | User search, creation, profile changes, disable, unlock, role and claim assignment, controlled deletion and recovery initiation | R1. ASP.NET Core Identity managers enforce identity rules. Privileged role assignment requires separate permission and recent MFA. |
| FR-06 | Role and permission administration | R1. Server policies enforce every route. Read-only roles cannot mutate through direct HTTP requests. Last-administrator protection is transactional. |
| FR-07 | Login, logout, email confirmation, password reset, optional registration, TOTP, recovery codes and passkey management | R1. Public registration is off by default. Administrators require MFA. Recovery does not expose whether an account exists. Passkey acceptance includes origin, RP ID and user verification checks. |
| FR-08 | Consent and end-user grant management | R1. Users can approve allowed scopes and revoke their own grants. Noninteractive requests return appropriate protocol errors instead of opening UI. |
| FR-09 | Core protocol endpoints and grants described in the release boundary | R1. Positive and negative interoperability scenarios pass. Discovery advertises only enabled capabilities. |
| FR-10 | Administrator visibility into authorizations, token metadata and local login sessions | R1. Filter by user, application, status and time; revoke by grant or subject. Token payloads and token values are never displayed. |
| FR-11 | Administration session and API protection | R1. React uses a same-origin server backend with HttpOnly cookies; tokens remain server-side. The API validates bearer tokens and current permissions independently. |
| FR-12 | Audit history and operational logging | R1. Mutation and sanitized audit intent commit atomically. Record actor, action, target, outcome, UTC time and correlation ID. Secrets, passwords, tokens and recovery material never enter logs. |
| FR-13 | Dashboard, configuration rules and issue lifecycle | R1. Baseline rules detect unsafe redirects, missing PKCE, invalid scope references, expired credentials and excessive lifetimes. Findings link to affected records and resolve after reevaluation. |
| FR-14 | External identity login | R1. Support an operator-configured OIDC provider with safe account linking. Dynamic provider CRUD and social-provider catalogue are R1.1. |
| FR-15 | Key inventory and certificate expiry visibility | R1. Show public metadata and active/retiring status. Key provisioning and rotation use an operator runbook; private-key editing or export through the admin UI is excluded. |
| FR-16 | Versioned management APIs, Scalar, Swagger UI and integration examples | R1. Use explicit Asp.Versioning policies, one authoritative OpenAPI contract per surface/major version, both documentation UIs and generated TypeScript contracts. Keep OAuth/OIDC endpoints unversioned. Use the newest stable compatible tooling; .NET 10 baseline is OpenAPI 3.1, with 3.2 adoption gated by the complete toolchain. |
| FR-17 | Equivalent PostgreSQL and SQL Server behavior | R1. Both providers pass every required persistence and end-to-end journey, fresh install and upgrade test. Neither is an experimental provider. |
| FR-18 | Greenfield installation, configuration, schema evolution, backup and recovery | R1. Demonstrate fresh initialization, protected bootstrap, future product schema upgrades and restore on both providers. Duende migration is excluded. |
| FR-19 | Beautiful, accessible React and STS Razor experiences | R1. Shared slate/indigo design tokens, light/dark modes and responsive screens; English first with externalized strings. WCAG 2.2 AA, manual accessibility review and approved visual regressions cover primary journeys and error/loading/empty states. |
| FR-20 | Session propagation and advanced security parity | R1.1 or separately approved work. Back-channel/front-channel logout across arbitrary clients, dynamic federation, distributed automatic signing-key management and financial-grade profiles receive separate acceptance criteria before implementation. |
| FR-21 | Comprehensive automated tests and permanent regressions | R1. Every first-party project, requirement, endpoint and security invariant has traceable verification. Real E2E flows, both databases, browser matrix, failure/concurrency tests and defect regressions are mandatory. |
| FR-22 | Maintainable, extensible and testable engineering | R1. Cohesive Features, explicit policies and dependencies, stable extension points, analyzers and independent reviews; avoid generic service/repository frameworks. |
| FR-23 | Project AI skills and specialized reviewers | R1. Review/install/evaluate the eight draft skills and eight subagent configurations after approval. Findings include evidence, severity, control IDs and uncertainty; agents do not self-certify the product. |
| FR-24 | Aspire local orchestration and distributed testing | R1. AppHost, ServiceDefaults and distributed tests start isolated provider profiles with real STS/BFF/API/worker boundaries and safe local mail/federation fixtures. |
| FR-25 | Authentication-server assurance and certification evidence | R1. Basic OP/Config OP conformance and submission package; applicable ASVS 5.0.0 L3 verification, OAuth Security BCP, NIST-oriented authentication controls and independent assessment. Public certified claims require completed external certification. |

## UI and interaction requirements

Navigation groups: Overview; Applications; API Audiences; Scopes; Users; Roles; Grants and Sessions; Audit; Configuration Issues; Operations; Developer Docs. Scope type distinguishes identity information from API access. Show only settings supported by the selected client profile. React and STS use the approved shared design system, including light/dark modes and accessible responsive states.

Forms use explicit save/cancel actions, field-level validation and optimistic concurrency. A stale edit produces a conflict view with reload and compare options. Secret issuance uses a dedicated response and one-time display; later reads show metadata only. Destructive actions state the affected records and whether revocation is included. Avoid silent cascades, arbitrary redirect inputs and ambiguous success messages.

## Nonfunctional requirements

These are proposed acceptance targets for approval, not measured capabilities.

| ID | Target | Measurement |
| --- | --- | --- |
| NFR-01 | No outstanding critical/high exploitable security findings at release | Dependency scan, threat-model review, negative protocol tests and independent assessment; unresolved exploitable critical/high findings block R1. |
| NFR-02 | Both database providers are first-class | Identical behavioral test matrix, documented provider versions and provider-specific migration artefacts in CI. |
| NFR-03 | Initial reference workload of 100,000 users, 5,000 applications and 1 million token/audit records | Test each provider on a published setup with two 2-vCPU/4-GB app instances and a separate 4-vCPU/16-GB database; adjust targets through recorded change if sizing evidence differs. |
| NFR-04 | p95 management reads under 500 ms at 20 requests/s; p95 token exchange under 500 ms at 50 requests/s | Warm-service 30-minute runs, under 0.1% unexpected errors, excluding human interaction and external-provider/email latency; report DB and cryptographic settings. |
| NFR-05 | Committed disable/revocation enforced by platform-owned APIs within 5 seconds | Cross-instance tests; external offline-validation APIs have an explicit maximum token lifetime plus clock-skew bound. |
| NFR-06 | Recovery point objective 15 minutes and recovery time objective 60 minutes | Proposed single-region target proven by restore exercise for each provider, including encryption/signing keys and deployment config. |
| NFR-07 | Deployment target of 99.9% monthly availability | Requires redundant instances, a suitable database service and measured monitoring; not a guarantee made by the codebase alone. |
| NFR-08 | No secrets in browser storage, logs, API exports or source control | Browser network/storage inspection, log scanning and secret-scanning checks. |
| NFR-09 | Primary UI journeys accessible and usable | Keyboard and screen-reader review plus automated accessibility checks; application creation completes within 5 minutes for a trained administrator. |
| NFR-10 | Audit and operational data have explicit retention | Proposed defaults: audit 365 days, operational logs 30 days, ended local sessions 30 days; retention is configurable and deployment owners approve legal/business needs. |
| NFR-11 | Complete requirement and critical-invariant traceability | Every accepted criterion maps to tests/evidence; initial floors of 90% lines/80% branches per substantive handwritten module plus mutation and decision coverage. Numeric coverage is not proof of correctness. |
| NFR-12 | Stable automated regression and accessible visual quality | Both-provider CI, critical browser journeys on PRs, full Chromium/Firefox/WebKit nightly/release matrix and reviewed light/dark snapshots; no unexplained skipped or flaky required tests. |
| NFR-13 | Certification-ready engineering and independent evidence | Pinned OIDF conformance plans, applicable ASVS control register, independent security assessment and version/configuration-bound submission records. Formal certification is tracked separately from internal test results. |
| NFR-14 | Reproducible development and contracts | Aspire start/test/teardown succeeds on both provider profiles; locked compatible packages; reproducible versioned OpenAPI and generated clients; Scalar/Swagger parity. |

## Success and release acceptance

Release requires every R1 requirement and critical security scenario to pass on both providers; complete traceable test evidence; independent security assessment; a clean-install demonstration; two-instance revocation/refresh evidence; successful restore drills; and the selected OpenID conformance plans with certification submission evidence. The proposed public general-availability gate requires completion of Basic OP/Config OP certification for the declared configuration; internal release candidates can remain submission-ready while that process is pending. Each requirement links to a Spec Kit feature, tasks and verification evidence. Product review covers the primary journeys across the React and STS themes, accessibility and both documentation UIs.

No team capacity or delivery date is assumed. The roadmap is dependency-based. Dual-provider support, account recovery, user sessions and federation are substantial work and must appear explicitly in task estimates after feature planning.

## Risks and decisions for approval

| Decision | Proposed baseline | Consequence |
| --- | --- | --- |
| A1 Product scope | Approve all R1 requirements and the explicit R1.1 boundary | This is functional replication with an identified parity backlog. |
| A2 Runtime | .NET 10, EF Core 10, OpenIddict 7.7.1 baseline; recheck supported patch versions at kickoff | No production dependency on OpenIddict 8 previews. |
| A3 Architecture | Separate AuthServer, Admin API and Admin BFF, plus a worker; feature-oriented shared packages | Independent deployment boundaries with one platform database per environment. |
| A4 Identity UI | Feature-organized Razor Pages for login, consent and recovery; React for admin | No MVC controllers required. Razor Page handlers are UI handlers, separate from Minimal API protocol endpoints. |
| A5 Tenancy | One issuer and one organization per deployment | SaaS tenant isolation is a future architectural decision. |
| A6 Security | Server-held admin tokens, MFA, signed access tokens, online state checks for platform API | More database work in exchange for a measurable revocation bound. |
| A7 Operations | Initial single-region container deployment and operator-managed keys; Aspire for local/test use | Cloud-specific hosting, HA service selection and legal retention remain deployment decisions. |
| A8 Greenfield | No Duende migration deliverable | Schema upgrades and recovery apply to this product only. |
| A9 UI design | Modern enterprise slate/indigo, light/dark, React plus Razor Pages | Confirmed user preference; shared semantic tokens and separate UI implementations. |
| A10 API tooling | Asp.Versioning, native OpenAPI generation, Scalar and Swagger UI | Stable compatible .NET 10 baseline uses OpenAPI 3.1; revalidate latest versions at kickoff. |
| A11 Quality | Comprehensive automation, regressions, independent reviews and eight skills/reviewers | Tests and evidence are feature work, with both-provider/browser costs in estimates. |
| A12 Assurance | OIDF Basic OP/Config OP target, ASVS 5.0.0 L3, OAuth BCP and NIST-oriented controls | Basic-suite compatibility with mandatory PKCE is an explicit first-milestone blocker; resolve with the Foundation without silent weakening. Independent assessment/submission require owners/resources; ISO/SOC is a separate track. |

Approval must identify this version and any amendments. Approval authorizes implementation within the accepted scope using Spec Kit; it does not itself authorize production deployment, certification publication/payment or external legal declarations. Until approval, activity remains repository research and documentation only.

## Additional approval documents

- [React Admin and STS Design System](https://chatgpt.com/space/page_ab23682976ec81919cc13b0c248f5d6e)
- [Engineering Quality, AI Skills and Subagents](https://chatgpt.com/space/page_5bc43c24ee3c8191909acef85d077b94)
- [Security Assurance and Certification Plan](https://chatgpt.com/space/page_517f6fb763ac8191a0e5609561b1cf71)

Version 0.2 supersedes version 0.1: greenfield scope removes Duende migration; Admin BFF is explicit; testing, Aspire, theme, API documentation/versioning and assurance requirements are expanded. The concrete draft AI files are in the downloadable package and are not active development instructions yet.
