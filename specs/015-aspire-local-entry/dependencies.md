# Dependency registry review

2026-10-27T17:34:40.424347+00:00

| Package | Current | Latest stable |
|---|---|---|
| Microsoft.CodeAnalysis.CSharp | 5.0.0 | 5.9.0 |
| Microsoft.Playwright | 1.63.0 | 1.64.1 |
| MessagePack | 2.5.307 | 3.1.12 |
| Deque.AxeCore.Playwright | 4.13.0 | 4.14.0 |
| Microsoft.IdentityModel.JsonWebTokens | 8.23.0 | 8.23.0 |
| OpenTelemetry.Exporter.InMemory | 1.19.1 | 1.20.1 |
| System.Security.Cryptography.Pkcs | 10.0.13 | 10.0.14 |
| Microsoft.AspNetCore.TestHost | 10.0.12 | 10.0.14 |
| OpenIddict.EntityFrameworkCore | 7.7.1 | 7.7.2 |
| OpenIddict.AspNetCore | 7.7.1 | 7.7.2 |
| OpenIddict.Server.AspNetCore | 7.7.1 | 7.7.2 |
| OpenIddict.Client.AspNetCore | 7.7.1 | 7.7.2 |
| OpenIddict.Client.SystemNetHttp | 7.7.1 | 7.7.2 |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | 10.0.12 | 10.0.14 |
| Microsoft.EntityFrameworkCore.Relational | 10.0.12 | 10.0.14 |
| Microsoft.EntityFrameworkCore.SqlServer | 10.0.12 | 10.0.14 |
| Microsoft.EntityFrameworkCore.Design | 10.0.12 | 10.0.14 |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.3 | 10.0.3 |
| Microsoft.Extensions.Hosting | 10.0.12 | 10.0.14 |
| Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore | 10.0.12 | 10.0.14 |
| Aspire.Hosting | 13.6.1 | 13.6.4 |
| Aspire.Hosting.AppHost | 13.6.1 | 13.6.4 |
| Aspire.Hosting.PostgreSQL | 13.6.1 | 13.6.4 |
| Aspire.Hosting.SqlServer | 13.6.1 | 13.6.4 |
| Aspire.Hosting.Testing | 13.6.1 | 13.6.4 |
| Microsoft.Extensions.ServiceDiscovery | 10.10.0 | 10.11.0 |
| OpenTelemetry.Extensions.Hosting | 1.19.1 | 1.20.1 |
| OpenTelemetry.Instrumentation.AspNetCore | 1.19.0 | 1.20.1 |
| OpenTelemetry.Exporter.OpenTelemetryProtocol | 1.19.1 | 1.20.1 |
| Testcontainers.PostgreSql | 4.16.0 | 4.16.0 |
| Testcontainers.MsSql | 4.16.0 | 4.16.0 |
| xunit.v3.mtp-v2 | 4.0.2 | 4.0.2 |
| Microsoft.Testing.Extensions.TrxReport | 2.5.1 | 2.5.2 |
| Asp.Versioning.Http | 10.2.3 | 10.2.3 |
| Asp.Versioning.OpenApi | 10.2.3 | 10.2.3 |
| Microsoft.OpenApi | 2.12.2 | 3.11.0 |
| Microsoft.AspNetCore.OpenApi | 10.0.12 | 10.0.14 |
| Microsoft.Extensions.ApiDescription.Server | 10.0.12 | 10.0.14 |
| Scalar.AspNetCore | 2.17.14 | 2.18.3 |
| Swashbuckle.AspNetCore.SwaggerUI | 10.3.0 | 10.3.1 |
| dotnet-ef | 10.0.12 | 10.0.14 |
| dotnet-coverage | 18.12.0 | 18.12.0 |
| Aspire.AppHost.Sdk | 13.6.1 | 13.6.4 |
| esbuild | 0.28.2 | 0.28.2 |
| openapi-fetch | 0.17.0 | 0.17.0 |
| openapi-typescript | 7.13.0 | 7.13.0 |
| typescript | 5.9.3 | 7.0.2 |
| @redocly/openapi-core | 2.62.1 | 2.71.2 |

Registry URLs, response hashes, timestamps, listing/publish evidence and dependency bounds are retained in [inventory.json](evidence/inventory.json) and [nuget-latest-catalogs.json](evidence/nuget-latest-catalogs.json).

Hard conflicts: Microsoft.OpenApi latest3.11.0 exceeds current latestASP.NET10/Asp.Versioning upper bounds; latestcompatible2.13.0. TypeScriptlatest7.0.2 violates latestgenerator7.13.0 peer^5.x; compatible5.9.3. Do not force or suppress these conflicts.

See [review.json](evidence/review.json) for SDK selectors, frozen-baseline preservation and unexecuted compatibility obligations.

## Selected compatibility decisions

Microsoft.OpenApi2.13.0 is the latest compatible stable2.x; ASP.NET OpenAPI10.0.14 requires `[2.12.2,3.0.0)` and Asp.Versioning10.2.3 requires `<3.0.0`. TypeScript5.9.3 remains because openapi-typescript7.13.0 imports its APIs at runtime and declares peer`^5.x`. All other direct packages/tools use the listed latest stable versions. No force/legacy-peer-deps or dependency-warning suppression.

SDK selection uses numeric minimum10.0.100 (required global.json syntax), `latestMinor`, and `allowPrerelease=false`: stable10.x only, no exactSDKrequirement or11.xrollforward. CI installs10.x; localbuildcurrently10.0.403/runtime10.0.14. DockerbuildSDKuses10.0channel; runtimeimageuses10.0.14with registry-verifieddigest. Package/tool locks remain exact. Frozen provider producer and baseline lock are unchanged.

First upgrade restore reported missing Microsoft.Testing.Platform2.5.2. Independent directpackage/nuspec/index checks subsequently verified availability; one fresh supported no-http-cache restore passed. Earlierfailure is retained as acquisition evidence, not a testpass.

Embedded Scalar/Swagger upgrades have independently reviewed byte provenance and updated notice metadata. Existing twelve viewer findings remain historical/unclosed; newqs1243452 is unresolved. Currentcandidatequeryreturns6findings; unionqueryretains13. No production/demo documentation mapping is enabled.
