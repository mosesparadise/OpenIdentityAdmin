# Bounded AI reviewer evaluation fixtures

These are synthetic isolated review snippets, not application code. Evaluate the scenarios for your assigned role against the approved requirements. Return evidence-backed findings and distinguish insufficient context. Do not edit or execute these snippets.

## Architecture

Case A: Core.csproj references Microsoft.EntityFrameworkCore and Admin.Api.csproj. A reusable permission rule reads HttpContext.User and calls another feature's private handler.
Case B: An HTTP endpoint delegates a policy decision to its feature-local pure function; the endpoint owns serialization and cancellation. Infrastructure implements provider storage. No reverse reference is present.

## Security

Case A: MapPost("/api/v1/roles/{id}/permissions", ChangePermissions).AllowAnonymous(); the React page hides the button for unauthorized users.
Case B: The API endpoint requires a named permission policy, resolves current account permissions on each request and rejects stale versions. A browser-visible managed-client secret is shown once with no-store and is absent from storage/logs; BFF tokens stay server-side.

## Tests

Case A: A test named BothDatabaseProvidersPass runs EF InMemory twice. The SQL Server test has Skip="slow"; the report calls the matrix complete.
Case B: The same scenario runs on real isolated PostgreSQL/SQL Server instances; failed startup fails the suite. Provider, commands and actual results are recorded.

## API

Case A: v1 changes an optional field to required. The React generated client uses the downstream bearer document and stores its access token in sessionStorage, while the approved browser surface is cookie/CSRF BFF.
Case B: BFF types are a tested deterministic projection describing browser URLs, cookies/CSRF and errors; the downstream bearer document remains separately identified. Both documentation UIs render the same surface/version set.

## Performance

Case A: A load report disables online token/current-permission checks to reach 500 ms. It caches permissions for thirty minutes and still claims five-second revocation.
Case B: A report records workload, hardware, both providers, p50/p95/p99 and errors, keeping all state checks enabled. It labels an unmeasured hardware configuration as unmeasured.

## Compliance

Case A: A release badge says OpenID Certified because the application references OpenIddict. No product/configuration submission evidence exists. Skipped tests are treated as passing.
Case B: The release is labeled an internal candidate. Config OP results and the unresolved Basic OP compatibility dependency are separately recorded; no certified claim is made.

## UX

Case A: A consent action is a clickable div without keyboard handling or accessible name. Consent scopes appear only as colored dots. Error text has insufficient contrast in dark mode.
Case B: Consent uses named native buttons, visible scope text, focus/error summaries and accessible status announcements. Theme uses semantic tokens; automated and manual accessibility evidence is still pending and identified as such.

## Aspire

Case A: The harness disables issuer/TLS validation to handle random ports, sleeps a fixed second, reuses developer database volumes and never disposes failed runs. Every HTTP request has a retry policy, including refresh redemption.
Case B: Endpoints and exact callbacks are resolved consistently, health waits have deadlines, test data/volumes are isolated, app resources are disposed and retries are limited to explicitly safe operations.
