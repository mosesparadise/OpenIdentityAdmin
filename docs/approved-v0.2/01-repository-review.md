# Skoruba Repository Review and OpenIddict Feature Mapping

The reviewed repository is a useful product reference, but replacing Duende packages in place would leave its configuration entities, service abstractions, API contracts and operational assumptions unresolved. A new OpenIddict implementation should preserve the administration journeys while redesigning the backend around OpenIddict managers and feature slices.

Review baseline: Skoruba 3.2.0, commit `06386cc726f1218d45cef82baad3f1692489300c`, committed 4 October 2026; reviewed 10 October 2026. This is a static architecture and source review, not a penetration test, executed demo or full dependency audit. No upstream source was changed.

## Observed architecture

The source contains three principal web hosts: `Skoruba.Duende.IdentityServer.STS.Identity`, `Skoruba.Duende.IdentityServer.Admin.Api` and `Skoruba.Duende.IdentityServer.Admin`. The administration frontend is a React/TypeScript Vite project. The STS uses ASP.NET Core Identity and MVC-based account/consent interfaces. Administration API controllers delegate through business services to repositories. Shared EF projects provide Identity, Duende configuration, persisted grants, admin configuration, audit/logging and Data Protection storage, with separate PostgreSQL and SQL Server migrations.

The reviewed STS targets .NET 10 and references Duende IdentityServer 8.0.9. The React package declares version 3.2.0, React 19.3.0, Tailwind/shadcn-related components, React Query, form validation and localization dependencies. These observations identify the source baseline; the new system must select its own supported, locked dependency set.

Source anchors: [solution](https://github.com/skoruba/Duende.IdentityServer.Admin/blob/06386cc726f1218d45cef82baad3f1692489300c/Skoruba.Duende.IdentityServer.Admin.sln), [STS project](https://github.com/skoruba/Duende.IdentityServer.Admin/blob/06386cc726f1218d45cef82baad3f1692489300c/src/Skoruba.Duende.IdentityServer.STS.Identity/Skoruba.Duende.IdentityServer.STS.Identity.csproj), [frontend package](https://github.com/skoruba/Duende.IdentityServer.Admin/blob/06386cc726f1218d45cef82baad3f1692489300c/src/Skoruba.Duende.IdentityServer.Admin.UI.Client/package.json), [shared DbContexts](https://github.com/skoruba/Duende.IdentityServer.Admin/tree/06386cc726f1218d45cef82baad3f1692489300c/src/Skoruba.Duende.IdentityServer.Admin.EntityFramework.Shared/DbContexts).

## Findings that change the replication approach

1. The administration backend is coupled to Duende models. For example, ClientService works with Duende grant types, client settings and shared-secret hashing. Copying these DTOs would imply unsupported OpenIddict semantics. Replace them with product contracts and explicit mappings.

2. The current admin host is already a server-mediated browser architecture. Its proxy calls `GetUserAccessTokenAsync` and forwards the user's access token. The README statement about client credentials does not describe that code path. Preserve user attribution and authorization in the new design.

3. ASP.NET Core Identity remains an appropriate user system. OpenIddict does not replace passwords, MFA, recovery, account linking or the login UI. Those are separate features to implement and test.

4. Monitoring rules, client integration snippets, passkeys and local session management belong in the inventory. An older picture of Skoruba as only client/resource CRUD would understate this project.

5. Duende operational features are not interchangeable database records. Consent, authorizations, tokens, login sessions and signing keys require distinct greenfield lifecycle designs.

Evidence: [ClientService](https://github.com/skoruba/Duende.IdentityServer.Admin/blob/06386cc726f1218d45cef82baad3f1692489300c/src/Skoruba.Duende.IdentityServer.Admin.BusinessLogic/Services/ClientService.cs), [admin proxy](https://github.com/skoruba/Duende.IdentityServer.Admin/blob/06386cc726f1218d45cef82baad3f1692489300c/src/Skoruba.Duende.IdentityServer.Admin.UI/Services/IdentityServices.cs), [admin authentication](https://github.com/skoruba/Duende.IdentityServer.Admin/blob/06386cc726f1218d45cef82baad3f1692489300c/src/Skoruba.Duende.IdentityServer.Admin/Services/StartupService.cs), [Identity context including passkeys](https://github.com/skoruba/Duende.IdentityServer.Admin/blob/06386cc726f1218d45cef82baad3f1692489300c/src/Skoruba.Duende.IdentityServer.Admin.EntityFramework.Shared/DbContexts/AdminIdentityDbContext.cs).

## Feature disposition

R1 means required for the first production release. Adapt means equivalent user value through a different data model. Later means an explicit parity gap requiring subsequent work.

| Observed source capability | OpenIddict platform treatment | Disposition |
| --- | --- | --- |
| Client search, edit, clone and wizard | OpenIddict applications plus product policy metadata | R1 adapt; FR-01–03 |
| Web, SPA, native, machine and device profiles | Allowlisted application templates with grants and endpoint permissions | R1; FR-02,09 |
| Multiple Duende secrets, types and expiration fields | One manager-hashed active shared secret or supported asymmetric client keys; own expiry policy | R1 reduced shared-secret model; overlap later |
| API scopes | OpenIddict scopes with allowed resources/audiences | R1 adapt; FR-04 |
| API resources and resource claims | Product audience catalogue, scope-resource assignments and claim policy | R1 adapt; no native ApiResource table assumption |
| API resource secrets | Separate introspection client registration, bound to allowed audiences | R1 adapt; never copy secrets into scope rows |
| Identity resources | Identity-scope catalogue and explicit scope-to-claim release rules | R1 adapt; FR-04 |
| User, role, user claims and role claims | ASP.NET Core Identity managers and permission policies | R1; FR-05–07 |
| Password login, email and recovery | Identity UI and configured email delivery | R1; FR-07 |
| Passkeys and other MFA journeys | ASP.NET Core Identity plus complete enrollment, challenge and recovery UI | R1; FR-07 |
| Consent and grants | OpenIddict authorization records with consent logic | R1 adapt; FR-08,10 |
| Persisted-grant administration | Separate authorization, token metadata and local-session views | R1 adapt; FR-10 |
| Device flow and user verification UI | OpenIddict device support plus feature handler and confirmation UI | R1; FR-09 |
| Server-side sessions | Product-owned AuthServer and BFF session stores | R1 local control; arbitrary relying-party logout propagation later |
| Identity-provider CRUD | Operator-configured OIDC federation first | R1 login; dynamic CRUD/social catalogue R1.1 |
| Key list/detail administration | Public key/certificate metadata and expiry visibility | R1; private-key lifecycle via operator runbook |
| Duende automatic key management | Durable operator-managed signing/encryption credentials | Operational replacement; distributed automated lifecycle later |
| Audit log and operational log views | Append-only sanitized audit history and bounded operational diagnostics | R1; FR-12 |
| Dashboard, rules and issues | Fixed rule catalogue over OpenIddict/product configuration | R1 adapt; FR-13 |
| Per-client generated integration code | OpenIddict-compatible examples from management DTOs | R1; FR-16 |
| API Swagger and generated TypeScript client | Explicit API versioning, one OpenAPI contract, both Scalar and Swagger UI, and generated frontend contracts | R1; FR-16 |
| Localization and branding | English first, shared slate/indigo light/dark React and Razor theme, accessible responsive states | R1 foundation; additional translations later |
| PostgreSQL and SQL Server | Provider-specific migrations and mandatory test matrix | R1 equal support; FR-17 |
| Docker, health checks, deployment guidance and certificate integration | Container/runbook baseline and secret-provider abstraction | R1; cloud-specific integration recipes later |
| dotnet new template and publishing tooling | Reproducible repository setup first | Reusable distributable template R1.1 |
| PAR and high-security wizard options | PAR included; other security-profile fields separately evaluated | R1 PAR; DPoP/mTLS/FAPI work later |
| Implicit, password and legacy flow configuration | Unsupported by greenfield policy; no ineffective settings exposed | Deliberately excluded from new platform |

Source inventory: [API controllers](https://github.com/skoruba/Duende.IdentityServer.Admin/tree/06386cc726f1218d45cef82baad3f1692489300c/src/Skoruba.Duende.IdentityServer.Admin.UI.Api/Controllers), [STS UI controllers](https://github.com/skoruba/Duende.IdentityServer.Admin/tree/06386cc726f1218d45cef82baad3f1692489300c/src/Skoruba.Duende.IdentityServer.STS.Identity/Controllers), [configuration rules](https://github.com/skoruba/Duende.IdentityServer.Admin/tree/06386cc726f1218d45cef82baad3f1692489300c/src/Skoruba.Duende.IdentityServer.Admin.BusinessLogic/ConfigurationRules), [integration snippets](https://github.com/skoruba/Duende.IdentityServer.Admin/tree/06386cc726f1218d45cef82baad3f1692489300c/src/Skoruba.Duende.IdentityServer.Admin.UI.Client/src/lib/snippets), [tests](https://github.com/skoruba/Duende.IdentityServer.Admin/tree/06386cc726f1218d45cef82baad3f1692489300c/tests).

## Entity mapping

| Duende concept | Proposed target | Important qualification |
| --- | --- | --- |
| Client | OpenIddict Application and ApplicationPolicy | Enabled, CORS, credential expiry, owner and UI metadata require product policy. |
| AllowedGrantTypes and response settings | Application permissions plus enabled server flows | A permission alone does not enable a flow globally. |
| RequirePkce and RequirePushedAuthorization | OpenIddict requirements and server policy | Verify public/confidential profiles and noncompliant request rejection. |
| AllowedScopes | Scope permissions plus consent and product restrictions | Standard special scopes still require product policy and refresh eligibility. |
| ApiScope | OpenIddict Scope | Scope resources must be used when producing the principal. |
| ApiResource | Product ApiAudience | No one-to-one native equivalent; audience assignments affect issued tokens. |
| IdentityResource | Identity scope plus claim-release policy | Defining a scope does not automatically emit claims. |
| PersistedGrant | Authorization and/or Token, depending on purpose | Different lifecycle semantics; design native state transitions. |
| User session | Product login session and cookie ticket | Distinct from OpenIddict authorization and token entries. |
| Identity users and roles | ASP.NET Core Identity entities | New opaque identities and manager-owned account credentials; no legacy import. |
| Signing and Data Protection material | Separately managed key rings and credentials | Do not treat cookie encryption keys as token signing keys. |

## Reuse and licensing

Use the repository as a behavioral reference. Reusing UI components or text requires preserving applicable Apache-2.0 notices and checking dependency/asset licenses. OpenIddict's Apache-2.0 license does not settle the licenses of every package, font, icon or copied source file. The new runtime dependency graph should contain no Duende IdentityServer component; any separate Duende-named helper must be consciously evaluated rather than carried over implicitly. Prefer platform or OpenIddict client facilities for the BFF.

Sources: [Skoruba license](https://github.com/skoruba/Duende.IdentityServer.Admin/blob/06386cc726f1218d45cef82baad3f1692489300c/LICENSE), [OpenIddict license](https://github.com/openiddict/openiddict-core/blob/7.7.1/LICENSE.md).

## Version evidence

OpenIddict documentation explicitly supports Minimal APIs and .NET 10. The implementation baseline is stable 7.7.1, tag commit `9146a148b3333570704a0211d037cd318093770a`. The repository development head inspected during research was an 8.0 preview, so it must not be confused with the selected release. Exact patch versions and database versions will be locked after approval and compatibility verification.

[OpenIddict ASP.NET Core integration](https://documentation.openiddict.com/integrations/aspnet-core.html) and [7.7.1 source](https://github.com/openiddict/openiddict-core/tree/7.7.1) establish the framework and API baseline. Product-specific security and persistence rules in the companion documents are proposed design decisions, not built-in OpenIddict guarantees.

## New requirements beyond the reference

Version 0.2 adds Aspire composition/distributed testing, comprehensive regression and coverage governance, shared React/Razor design tokens, both API documentation UIs, explicit API version lifecycle, project AI skills/reviewers and an OIDF/ASVS assurance plan. These are proposed greenfield requirements, not assertions that the inspected Skoruba release implements them. Duende migration is outside scope.
