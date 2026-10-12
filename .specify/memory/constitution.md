# IdentityPlatform Constitution

## Core Principles

### 1. Approved scope and evidence

Approved requirements govern implementation. Every release requirement traces to a feature specification, acceptance criteria, tasks and verification evidence. Development does not begin before document approval.

### 2. Features within projects

Use Features per project. Keep a use case's transport, validation, execution and tests cohesive. Shared infrastructure is limited to stable reused mechanisms; avoid global business-service layers and generic repositories.

### 3. OpenIddict protocol ownership

OpenIddict owns protocol mechanics. Minimal API handlers provide application decisions and complete through the appropriate OpenIddict scheme. Do not handcraft tokens or bypass protocol validation.

### 4. Identity and current authorization

Identity and administrative authorization are explicit. ASP.NET Core Identity owns account credentials; current permissions govern management actions. Protect administrator recovery and last-administrator transitions.

### 5. Server-held browser tokens

Browser tokens remain server-side. React talks to the same-origin BFF using protected sessions and anti-forgery; management API authorization remains independent.

### 6. Equal database support

PostgreSQL and SQL Server have equal release status. Every persistence feature must include both migrations and meaningful integration verification against both providers.

### 7. Secrets and manager-owned state

Secrets and sensitive identity state are minimized. Use manager-based credential handling, durable external key management, least-privilege storage and sanitized audit/logging.

### 8. Measurable revocation and session effects

State-change effects are measurable. Specify behavior for cookies, token entries, authorizations, refresh and external offline validation separately. Retain cross-instance enforcement tests.

### 9. Operability within the feature

Operability is part of the feature. Include relevant health/metrics, failure behavior, schema evolution, recovery and runbook changes with the feature.

### 10. Meaningful boundary verification

Prove behavior at the correct boundary. Prefer security, protocol, database and end-to-end tests over tests that only mirror implementation. No unverified security or conformance claims.

### 11. Stable compatible dependencies

Use stable, pinned dependencies and reviewed updates. Preview framework or protocol packages require an explicit approved exception. Keep third-party notices and a dependency/license inventory.

### 12. Visible scope changes

Scope changes are visible. Unbuilt requirements and discovered limitations remain documented gaps. Automated convergence does not authorize adding unrelated scope.

### 13. Comprehensive regression coverage

Comprehensive verification accompanies every feature: requirement/invariant traceability, both-provider tests, browser E2E, failure/concurrency coverage and permanent reproducible-defect regressions. Numeric coverage is a floor, not a substitute for assertions.

### 14. Aspire composition

Aspire is the standard local/distributed test composition, with real TLS/issuer validation, ephemeral fixtures, health deadlines and no blanket retries of security mutations.

### 15. Versioned API contracts

Admin API versions are explicit; one supported contract per API surface drives Scalar/Swagger, with React generated from the BFF browser contract. Keep protocol routes separate and use the newest stable compatible package set.

### 16. Shared accessible design

React and Razor share slate/indigo light/dark design tokens; responsive states, WCAG 2.2 AA and visual/manual accessibility review are acceptance criteria.

### 17. Security assurance claims

Applicable ASVS L3 and protocol control evidence is mandatory. Target Basic OP/Config OP certification for the released configuration; never equate AI findings or internal tests with external certification.

### 18. Independent AI review

Use the reviewed project-specific AI skills and relevant independent reviewers. Preserve source/test evidence, inherited model choices, host permissions and the main agent's integration accountability.

### 19. Principal-engineer maintainability

Engineers and AI contributors MUST produce clean, maintainable, testable and easy-to-understand
code. Methods and classes MUST have a cohesive responsibility and remain small enough to review
without reconstructing unrelated behavior. Extract meaningful operations from lengthy methods;
do not disguise complexity with arbitrary partial classes or pass-through wrappers. Reuse stable
mechanisms across projects instead of copying large implementations. Keep domain ownership local
and avoid speculative abstractions. Review dependency direction, failure semantics, cancellation,
resource ownership and extension points with the implementation, not in a later cleanup phase.
Review thresholds are 40 logical lines per method and 300 per class. Exceeding a threshold requires
refactoring or a documented, scoped exception with rationale, owner and review expiry.

### 20. Given-When-Then scenarios

Handwritten test methods MUST describe the precondition, action and observable result using
`Given_<condition>_When_<action>_Then_<outcome>`. Other test languages MUST use the equivalent
readable Given/When/Then scenario description. Names MUST express behavior, not generic placeholders.
Renames MUST preserve assertions and update exact discovery inventories, filters, mutation targets
and requirement links. Historical evidence remains tied to its original commit and test identity.

### 21. Explicit values and validated configuration

Unexplained strings and integers that encode domain rules, protocol identifiers, limits, durations,
routes or operational policy MUST NOT be scattered through executable code. Use framework/library
constants, feature-owned named values, value types or validated configuration according to ownership.
Do not create a global constants dumping ground. Independently specified test expectations MUST
remain independent of implementation values. Security invariants MUST NOT become deployment-tunable
merely to remove literals. OpenAPI deployment/presentation settings MUST come from appsettings-backed,
validated options; supported API contracts and security constraints remain explicit in code.

### 22. Useful code documentation

Every handwritten method MUST have meaningful documentation of its purpose and contract. Document
parameters, results, failure/cancellation behavior, ownership and security-sensitive side effects
where relevant. Externally consumed methods and classes MUST include detailed documentation and
safe usage examples. Documentation MUST explain behavior rather than restating a symbol's name,
and MUST evolve with code. Use XML documentation for C# methods. Do not add secrets or real
identities to examples. Native generated migrations/snapshots are excluded; test methods require
meaningful documentation but not public-API example boilerplate. Other exclusions must be explicit.

### 23. Proportional, fail-closed verification

Documentation-only changes MAY omit expensive application test suites only when complete change
evidence proves that all changed paths are eligible documentation. Retain relevant documentation,
policy, instruction and secret checks, plus a visible final acceptance gate. Mixed changes,
configuration, dependencies, executable tooling, workflow changes and uncertain classifications
MUST receive full applicable verification. Scheduled/manual full verification remains available.
An intentionally unselected suite MUST be recorded as not required for that change, never as a
test pass. Failing, cancelled or unexpectedly skipped required work MUST block acceptance.

## Technology and Release Constraints

Use .NET 10, EF Core 10 and stable OpenIddict 7 with ASP.NET Core Identity. All versions must be locked and validated together. React/TypeScript communicates through the Admin BFF. Authentication UI uses feature-organized Razor Pages and shared design tokens. Protocol application handlers and management endpoints use Minimal APIs. PostgreSQL and SQL Server are both required production providers; one is selected per environment.

R1 is single-issuer and greenfield. Duende data import/cutover, implicit/password/hybrid flows, multi-tenancy and FAPI are outside R1. Public general availability requires the approved assurance/certification gates. An unresolved official suite mismatch cannot justify weakening security or falsely reporting conformance. See the approved PRD and any explicitly accepted amendment for exact release scope.

## Development Workflow and Review

Use the installed Spec Kit specify, clarify, plan, checklist, tasks, analyze, implement and converge workflows. Every accepted requirement maps to scenarios/tasks and verification evidence. Resolve critical inconsistencies before executing affected tasks; continue independent authorized tasks. Feature completion requires actual checks, recorded results and the absence of unbuilt accepted criteria.

Architecture and test reviews apply to substantive features, with security/API/performance/compliance/UX/Aspire review selected by impact. Keep reviewers independent and record findings/dispositions. Mandatory tests run against both databases and relevant real host/browser boundaries. Do not mistake draft plans, AI opinions, code coverage or passing selected tests for formal certification or an independent security assessment.

## Governance

The requesting user ratified this baseline by approving documentation version 0.2 on 2026-10-10; docs/approval.md is the authority record. Approved user instructions prevail over this file. Routine work within that scope does not need repeated approval. A material scope or security-architecture change requires a concrete proposed amendment and the user's explicit decision. Production deployment, public certification submissions/payments and legal declarations have their own authorization.

Constitution amendments use semantic versions: major for incompatible governance changes, minor for added obligations and patch for clarifications. Review every implementation plan against these principles; explain any exception with scope, evidence, owner and expiry. Preserve the immutable approved snapshot and record accepted amendments separately.

The user's engineering feedback on 2026-10-12 authorizes principles 19-23. Their adoption does not
claim that legacy code already meets every new rule. Tooling, reviewed exceptions and remaining
retrofit work MUST be explicit in the living task register; new and changed code MUST follow these
principles immediately. Existing violations are corrected in separately reviewed PRs alongside
feature delivery, not silently grandfathered as compliant code.

**Version**: 1.1.0 | **Ratified**: 2026-10-10 | **Last Amended**: 2026-10-12
