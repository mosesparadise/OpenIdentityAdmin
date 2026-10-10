# OpenIddict Greenfield Data and Versioned API Contracts Draft

Model native OpenIddict records separately from product configuration and identity sessions. Use manager APIs for lifecycle changes and explicit product contracts for administration. This is a greenfield system; legacy imports and Duende cutover are outside scope.

Status: Proposed design for PRD version 0.2. R1 covers new databases, protected bootstrap, provider-specific schema evolution, versioned management contracts and both API documentation UIs.

## Logical data model

| Entity or aggregate | Owner and relationships | Rules |
| --- | --- | --- |
| OpenIddict Application | Native application manager; referenced by authorizations/tokens | Protocol client ID, type, consent type, redirects, permissions, requirements, settings and supported key material |
| ApplicationPolicy | Product metadata keyed by application ID | Enabled, owner/contact, allowed origins, credential expiry, allowed identity providers, security profile, version |
| OpenIddict Scope | Native scope manager | Name, display data and resources/audiences; names are stable protocol identifiers |
| ScopePolicy and ClaimReleaseRule | Product metadata keyed by scope ID | Identity/API kind, enabled, consent text, required/optional presentation and approved claim destinations |
| ApiAudience | Product catalogue, referenced through scope-resource assignments | Stable resource identifier, display name, owner and enabled state; not a second OAuth client |
| Identity user, role, claim, login, token and passkey | ASP.NET Core Identity | Users have stable opaque string IDs, status and security state; manager APIs control credentials |
| Administrative permission assignment | Product-owned permission catalogue and role binding | Separate human and machine permissions; protected privileged assignments |
| OpenIddict Authorization | Native authorization manager | User/application/scope consent or token-chain association; valid/revoked lifecycle |
| OpenIddict Token | Native token manager | Type, status, subject, application, authorization, creation/expiration and protected payload where needed |
| IdentitySession | AuthServer product session store | User, session ID, authentication time/strength, expiry, revoked time, security version |
| AdminSessionTicket | BFF session store | Opaque cookie handle, protected ticket/token state, user/session binding, concurrency marker and expiry |
| OpenIddict client state tokens | BFF client stack in AdminSessionDbContext | Durable callback state and replay protection, isolated from authorization-server token records |
| Interaction | AuthServer short-lived store | Validated request reference, interaction purpose, browser binding, intended subject where known, completion and expiry |
| AuditEvent and OutboxMessage | Administrative/security feature owner | Durable sanitized event and delivery/retry metadata; no secrets in event payloads |
| ConfigurationRule and ConfigurationIssue | Monitoring feature owner | Fixed rule key/version, parameters, target, severity, status and evaluation timestamps |

Do not duplicate native client permissions or scope resources into a second authoritative table. Product relations may support catalogue navigation, but updates must preserve one authoritative protocol representation. JSON extension properties have documented schemas and limits; they are not a substitute for queryable policy or indexes.

## Database design rules

- Use opaque string identifiers for new Identity/OpenIddict records. External contracts never expose a provider-specific numeric key requirement.

- Choose ordinal case-sensitive semantics for protocol client IDs, scope names and resource identifiers. Use matching provider collations/indexes or portable normalized keys with collision validation. Identity username/email normalization follows Identity policy and is specified separately.

- Use UTC timestamps, bounded text lengths, explicitly tested JSON serialization and a portable product concurrency marker. Database-specific storage representations may differ while contracts remain identical.

- Index client ID, scope name, authorization subject/application/status, token authorization/status/expiry, session user/status and audit time/actor/target. Performance tests use representative cardinalities.

- Scope/audience deletion is blocked while referenced unless an explicit reassignment/revocation workflow is selected. Removing or narrowing a permission must revalidate refresh eligibility and consent.

- Store credential expiry as enforced policy. The existence of a date field does not cause OpenIddict to reject a credential automatically.

- Commit a mutation and its audit intent in one transaction. External email/audit delivery uses an outbox so retries do not repeat the mutation. Identity/OpenIddict managers may save internally, so prove they participate in the same scoped context/transaction before relying on atomicity.

- Keep provider-specific migrations in separate assemblies. Run them through an operator-controlled migrator; no automatic schema change at web startup in production.

- Cleanup removes only records eligible under native lifecycle rules plus retention policy. Never prune active refresh chains, active sessions or keys required to decrypt retained data.

Native sources: [application descriptor](https://github.com/openiddict/openiddict-core/blob/7.7.1/src/OpenIddict.Abstractions/Descriptors/OpenIddictApplicationDescriptor.cs), [scope descriptor](https://github.com/openiddict/openiddict-core/blob/7.7.1/src/OpenIddict.Abstractions/Descriptors/OpenIddictScopeDescriptor.cs), [EF Core integration](https://documentation.openiddict.com/integrations/entity-framework-core.html).

## Management HTTP conventions

Management APIs use explicit version selection at `/api/v{version:apiVersion}`, initially `/api/v1`; protocol paths are a separate unversioned OIDC/OAuth surface. Minimal API handlers publish OpenAPI operation IDs, request/response schemas and named security policies. Generate React TypeScript contracts from the BFF browser surface/projection, preserving cookie and anti-forgery semantics; keep the downstream Admin bearer contract distinct.

Collections accept bounded search/filter/sort parameters, default page size 25 and maximum 100. Responses include items and pagination metadata with stable sorting; high-volume audit/token searches use a cursor. Date filters require UTC boundaries and a bounded range. Validation uses ProblemDetails with field errors and a correlation ID, excluding sensitive values. Errors distinguish 400 validation, 401 authentication, 403 permission, 404 resource, 409 domain conflict, 412 stale version and 428 required precondition. Protocol errors retain OAuth/OIDC formatting.

GET returns an ETag for editable resources. PUT/PATCH/DELETE require If-Match for existing records, and return 412 when stale. Scope and client identifier changes create a new protocol identity rather than silently renaming an in-use identifier. One-time credential endpoints return `Cache-Control: no-store` and no secret through later GET, export, audit or error responses.

| Operation | Contract outline | Policy |
| --- | --- | --- |
| `POST /applications` | Profile, client ID, display name, redirects, scopes and credential mode; returns ID, version and effective configuration | applications.write |
| `GET /applications/{id}` | Sanitized configuration, policy and credential metadata; never a secret hash | applications.read |
| `PUT /applications/{id}` | Validated editable fields and If-Match | applications.write |
| `POST /applications/{id}/secret-replacements` | Recent MFA, explicit confirmation and expected application/credential version; atomic compare-and-replace, stale request rejected; returns the new secret once | applications.credentials |
| `POST /applications/{id}/disable` | Reason and version; disables issuance and optionally requests grant revocation | applications.disable |
| `POST /scopes` and `PUT /scopes/{id}` | Scope type, resource assignment and consent/claim policy | scopes.write |
| `POST /audiences` | Stable resource name, display name and ownership | audiences.write |
| `POST /users/{id}/disable` | Reason and version; revokes local sessions and authorizations as defined | users.disable |
| `POST /users/{id}/recovery-requests` | Sends a short-lived recovery notification, returns no password or recovery token | users.recovery |
| `PUT /roles/{id}/permissions` | Allowlisted permissions, version and recent MFA for privileged assignment | roles.permissions |
| `POST /authorizations/{id}/revoke` | Reason; repeated requests return the current revoked state | grants.revoke |
| `GET /audit-events` | Bounded filters and sanitized records | audit.read |
| `POST /configuration-checks` | Fixed rule selection/target; returns a job ID | configuration.evaluate |

Bulk grant revocation, monitoring and deletion work use durable jobs: 202 with job ID/status URL, bounded progress/error reporting and idempotent retries. Credential creation/replacement is never automatically retried after an ambiguous response. On lost one-time output, an operator initiates a new replacement explicitly; do not persist plaintext merely to enable replay. Create/other job operations may use scoped idempotency keys with request hashes and expiry.

API contract acceptance includes direct unauthorized calls, excessive page sizes, stale edits, duplicated client IDs under both database collations, unsupported option combinations and secret-bearing responses passing through all proxies/loggers.

## Versioned management API contract

Use URL-segment major versioning through `/api/v{version:apiVersion}/...`, initially `/api/v1/...`. Configure explicit endpoint version sets and URL-segment version selection through Asp.Versioning rather than treating a literal `/v1` prefix as a complete versioning implementation. Missing/unsupported versions have documented rejection behavior and contract tests; do not silently route to the newest version. The BFF explicitly targets a supported Admin API major version.

The browser contract is versioned at `/bff/api/v1/...` where it exposes management resources; its version is owned by the BFF and maps explicitly to the downstream API. Authentication/session helpers such as `/bff/login`, `/bff/logout`, `/bff/user` and the OIDC callback remain stable application routes. OIDC/OAuth endpoints, discovery, JWKS and registered login/logout callbacks keep their protocol-defined routes and are outside management API versioning.

Document additive versus breaking changes: new optional fields/endpoints may be additive; removed fields, tighter validation, changed required fields, enum behavior or changed authorization semantics require compatibility review. A breaking public contract requires a new major version. Proposed support policy: retain the preceding major for at least six months after its successor becomes generally available, publish a deprecation date and sunset notice, and identify affected clients. Initial R1 ships only v1. Report supported/deprecated API versions and use applicable standard deprecation/sunset headers without implying a sunset before a date is approved.

## OpenAPI generation and package policy

One authoritative generated contract per API surface and supported major version is the source for both documentation UIs and contract checks. React's generated client describes the BFF browser contract: `/bff/api/v1`, cookie authentication, anti-forgery requirements and BFF errors. For transparent operations, derive that contract through a deterministic, tested projection of the downstream Admin API contract; explicitly describe BFF-specific or transformed operations. Keep the downstream bearer-token contract separately identifiable, and compile/test the browser client's actual URLs, DTOs and security behavior. Use Microsoft.AspNetCore.OpenApi with Asp.Versioning.Http and Asp.Versioning.OpenApi integration, versioned API Explorer, and build-time document generation through Microsoft.Extensions.ApiDescription.Server. The integration may include an MVC-named API Explorer package transitively; that does not require application controllers.

Keep package versions at the newest stable, mutually compatible set for the selected .NET LTS runtime. Pin the SDK, central .NET package versions, Node/frontend locks and UI assets after approval. Research candidates verified on 10 October 2026 are Asp.Versioning.Http/OpenApi 10.2.3, Microsoft.AspNetCore.OpenApi 10.0.12 and Scalar.AspNetCore 2.17.14. These are not a validated dependency lock. Resolve and test the full graph before adoption; in particular, Asp.Versioning.OpenApi 10.2.3 constrains Microsoft.OpenApi to versions below 3.0.0, so independently choosing every package's newest major is unsafe.

The latest published OpenAPI specification reviewed is 3.2.0. The selected .NET 10 stable generator supports OpenAPI 3.1 and JSON Schema 2020-12; Microsoft's documentation places 3.2 generation in .NET 11. R1 therefore uses OpenAPI 3.1 as the supported baseline. Adopt 3.2 when the approved runtime and complete generation, validation, UI, diff and client-generation pipeline support it. Changing only the document's `openapi` string is prohibited. Management API v1 and OpenAPI format 3.1 are independent version axes.

Sources: [Microsoft generation guidance](https://github.com/dotnet/AspNetCore.Docs/blob/main/aspnetcore/fundamentals/openapi/aspnetcore-openapi.md), [official Minimal API versioning example](https://github.com/dotnet/aspnet-api-versioning/tree/main/examples/AspNetCore/WebApi/MinimalOpenApiExample), [OpenAPI 3.2 specification](https://github.com/OAI/OpenAPI-Specification/blob/main/versions/3.2.0.md), [Asp.Versioning package constraints](https://api.nuget.org/v3-flatcontainer/asp.versioning.openapi/10.2.3/asp.versioning.openapi.nuspec).

## Scalar and Swagger UI

Provide Scalar at `/docs/scalar` and Swagger UI at `/docs/swagger`. Both render the same published document set: the downstream Admin API `/openapi/v1.json` and the browser BFF `/openapi/bff-v1.json`, with clear surface/version labels and correct server URLs. Generate the Admin document in its host and the BFF document/projection in the BFF. Use Scalar.AspNetCore and Swashbuckle.AspNetCore.SwaggerUI for rendering; do not add a second Swagger schema generator. Enabled browser access is through the same-origin BFF: serve its own document and proxy only allowlisted Admin documentation/asset routes, injecting server-held authorization upstream. Direct navigation to bearer-only API documentation is not the browser login path. Verify HTML, asset and document URLs, authorization and CSP through the actual proxy.

The document includes unique stable operation IDs, summaries, permission/scope requirements, input limits, examples, pagination, concurrency headers, typed success responses and RFC 9457 Problem Details. Sensitive examples are synthetic. Both UIs must display nullable and composed schemas, enums, dates and error responses consistently and load all supported versions.

Documentation UI and JSON routes share an access policy and are disabled publicly in production by default. R1 renders documentation with interactive requests disabled; enabling try-it later requires tested authentication and CSRF handling and may not expose the BFF's stored tokens. Self-host/pin assets and fonts, align CSP and theme, and disable unapproved third-party proxies or optional AI/agent features. Swagger UI's current support includes OpenAPI 3.2; the R1 baseline is driven by the selected complete toolchain, not an assumed UI limitation.

Sources: [Microsoft UI integration](https://github.com/dotnet/AspNetCore.Docs/blob/main/aspnetcore/fundamentals/openapi/using-openapi-documents.md), [Scalar ASP.NET Core integration](https://github.com/scalar/scalar/blob/main/documentation/integrations/aspnetcore/integration.md), [Swagger UI compatibility](https://github.com/swagger-api/swagger-ui#compatibility).

## Contract acceptance gates

CI generates and validates every versioned contract, rejects missing/duplicate operations and breaking changes against the supported baseline, and regenerates/compiles/tests the TypeScript client. Test version selection, unsupported/missing versions, deprecation metadata, ETags, Problem Details, documented authorization and response headers. Browser tests load both UIs, compare displayed operation/version sets, verify document access policy and check that credentials or secret values are never prefilled or persisted. Package upgrades rerun the complete compatibility suite before changing the lock.

## Greenfield initialization and schema evolution

Create empty PostgreSQL and SQL Server databases using the product's own versioned EF Core schema migrations, then perform audited one-time administrator and first-party client bootstrap with operator-supplied credentials. Use synthetic development/test seeds with no production defaults. Test idempotency, failed/partial initialization, schema compatibility, future product upgrades, backup and recovery on each provider.

Duende remains a feature reference only. User/client/grant import, secret-hash conversion, legacy issuer coexistence and Duende cutover/rollback tooling are outside the project. Product schema evolution and disaster recovery remain required greenfield lifecycle responsibilities.


Concurrency-sensitive POST commands also carry an explicitly documented expected target version. Secret replacement atomically checks the credential/application version and advances it so concurrent requests cannot both replace the same observed credential. A missing version is invalid and a stale version is a conflict; this is a command precondition, not an undefined If-Match comparison against an action URL. Test two concurrent replacements and lost one-time output on both providers.
