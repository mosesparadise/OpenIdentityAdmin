# M01-F research — 12 October 2026

Status: source/package availability research only. No dependency graph has been restored, built or accepted for this feature. The user confirmed both structural/sequencing recommendations in [decision-proposal.md](decision-proposal.md); tool validation proceeds under the implementation tasks.

## Existing-host boundary

Both `src/backend/IdentityPlatform.Admin.Api/Features/Setup.cs` and the BFF equivalent call `AddStorage` and `MapPlatformHealthEndpoints`. Neither registers an administrator authentication/authorization scheme, session or proxy. `src/frontend/README.md` reserves a future React application. Therefore there is no real protected documentation browser path to reuse.

User-confirmed decision: keep docs unmapped and test representative tooling in non-shipped fixtures. Alternative rejected: temporary anonymous Development docs, use of the non-admin pilot as admin authentication, or fake production operations. These would create unapproved exposure or misleading acceptance.

Build-time Microsoft generation executes entry-point startup. Existing storage configuration must remain fail-closed. Export validation must use explicit synthetic configuration without DB access/seed/migration/key loading and must prove no network side effects. Do not add a general production startup-validation bypass.

## Stable package candidates observed

Queried official NuGet flat-container indexes/nuspecs and npm registry metadata on 12 October. These are candidates, not an approved compatible lock.

| Component | Stable candidate | Constraint / verification still required |
| --- | --- | --- |
| Asp.Versioning.Http / OpenApi | 10.2.3 | Explicit Minimal API URL-segment version sets and Explorer integration; no application controllers. |
| Microsoft.AspNetCore.OpenApi | 10.0.12 | .NET 10; Microsoft.OpenApi >=2.12.0 and <3.0.0. |
| Microsoft.Extensions.ApiDescription.Server | 10.0.12 | Build export startup behavior, no external effects and deterministic output. |
| Scalar.AspNetCore | 2.17.14 | Bundled/self-hosted assets, supported schemas, disabled interaction/AI, CSP, theme and browser tests. |
| Swashbuckle.AspNetCore.SwaggerUI | 10.3.0 | UI only; no Swagger generator; same contracts, disabled submit/auth and asset/CSP tests. |
| openapi-typescript | 7.13.0 | Peer dependency TypeScript `^5.x`; cannot independently adopt current TypeScript 7.0.2. |
| openapi-fetch | 0.17.0 | Browser-only same-origin cookie/antiforgery behavior and no token API; candidate for the mandatory test-only runtime consumer; no usable production SDK is claimed. |
| @redocly/openapi-core | 2.62.1 | Node >=22.12 or >=20.19 <21; npm >=10; validate actual 3.1 output and pin entire dependency graph. |

Asp.Versioning.OpenApi 10.2.3 depends on Asp.Versioning.Mvc.ApiExplorer >=10.2.1, Microsoft.AspNetCore.OpenApi >=10.0.10 and Microsoft.OpenApi >=2.7.5,<3.0.0. The intersection with Microsoft.AspNetCore.OpenApi10.0.12 is >=2.12.0,<3.0.0. An MVC-named transitive Explorer package does not authorize controllers or a second generator.

Do not force TypeScript peer compatibility with legacy-peer-deps/overrides. The latest stable 5.x observed is TypeScript 5.9.3. Alternative @hey-api/openapi-ts 0.99.0 declares TypeScript `>=5.5.3 || >=6.0.0 || 6.0.1-rc` and Node >=22.18.0; this range admits the current compiler but does not itself establish schema/client correctness. Compare these candidates and choose a lock only after a representative compile/runtime/schema proof. The plan selects Node24.19.0/npm11.9.0 and the compatible TypeScript5.9.3 pair; diff tooling remains subject to the documented validation gate.

Sources (indexes supply availability; exact nuspecs supply constraints):

- [Asp.Versioning index](https://api.nuget.org/v3-flatcontainer/asp.versioning.openapi/index.json), [10.2.3 constraints](https://api.nuget.org/v3-flatcontainer/asp.versioning.openapi/10.2.3/asp.versioning.openapi.nuspec).
- [Microsoft 10.0.12 constraints](https://api.nuget.org/v3-flatcontainer/microsoft.aspnetcore.openapi/10.0.12/microsoft.aspnetcore.openapi.nuspec).
- [Scalar index](https://api.nuget.org/v3-flatcontainer/scalar.aspnetcore/index.json), [SwaggerUI index](https://api.nuget.org/v3-flatcontainer/swashbuckle.aspnetcore.swaggerui/index.json).
- [openapi-typescript metadata](https://registry.npmjs.org/openapi-typescript/7.13.0), [openapi-fetch metadata](https://registry.npmjs.org/openapi-fetch/0.17.0), [Redocly metadata](https://registry.npmjs.org/@redocly/openapi-core/2.62.1).
- [Official Minimal OpenAPI example](https://github.com/dotnet/aspnet-api-versioning/tree/main/examples/AspNetCore/WebApi/MinimalOpenApiExample).

## Format and compatibility semantics

Retain approved OpenAPI 3.1/JSON Schema 2020-12 on .NET 10. API major v1 is independent from document format. Do not relabel output as 3.2. Use one native generator per API surface/major; viewers render the same output. BFF-owned metadata remains separate from downstream bearer metadata.

A diff engine alone does not prove permission, CSRF, concurrency or client behavior. Supplement validated schema comparisons with semantic inventory rules and seeded regressions. Compare request and response changes directionally, including required/nullable/readOnly/writeOnly, enums, composed schemas, input bounds, security alternatives, responses, headers and preconditions. Review additive output enum changes rather than assuming they are harmless to exhaustive clients.

## Evidence isolation and runtime budgets

Separate actual host inventories/denials, test-only tooling fixture results and later real authenticated acceptance. No generated-empty-contract pass substitutes for a meaningful nonempty compatibility matrix. Do not publish a placeholder SDK or add management demo resources merely to populate output.

Retain existing both-provider and browser evidence. PR #19's final SQL issuer report took 653.141 seconds against the unchanged 660-second bound. New contract/browser work needs a separate bounded invocation; it must not increase that partition's workload/deadline. Track the existing narrow headroom separately from M01-F success.
