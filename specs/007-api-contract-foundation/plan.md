# Implementation Plan: Management contract foundation

Branch `017-api-contract-foundation`; 12 October 2026; [specification](spec.md).

## Confirmed scope and technical context

The user confirmed foundation-first sequencing and the exact proposed project structure. Implement CONTRACT-001–010 as bounded tooling evidence; actual authentication and documentation access stay M02/M04 dependencies. No further general approval is needed.

Use .NET SDK10.0.401/runtime10.0.12, Asp.Versioning.Http/OpenApi10.2.3, Microsoft.AspNetCore.OpenApi and ApiDescription.Server10.0.12, Scalar.AspNetCore2.17.14, SwaggerUI10.3.0, OpenAPI3.1, existing xUnit v3/MTP and Playwright. Node24.19.0/npm11.9.0 with TypeScript5.9.3, openapi-typescript7.13.0/openapi-fetch0.17.0 is the chosen compatible compiler/generator pair; no peer override. Validate and lock the complete graph before acceptance. Redocly openapi-core2.62.1 validates schemas through fixed in-memory rules and a local-only resolver; supplement compatible schema diff tooling with explicit security/precondition inventory checks and seeded controls. The actual diff implementation and limits are documented with its tests before acceptance.

No persistence/model/schema change. Both provider regressions remain mandatory. New contract tests run in a separate bounded invocation, preserving the issuer660s and ordinary unit180s deadlines. Relevant contract browser cases run in Chromium, Firefox and WebKit; required executions and artifacts must be counted, not silently omitted. Existing nine module coverage floors apply to host composition and rendering too.

## Constitution check

Approved scope and user structure choices: satisfied. Features/Setup/Composition, independent host boundaries, native generation, server-held tokens, both databases, deterministic compatible locks, meaningful tests, independent review and honest capability claims are preserved. No exceptions. Do not edit immutable approved snapshots or use the pilot as admin auth.

## Project structure

Use the confirmed tree in [decision-proposal.md](decision-proposal.md). Host-local ApiContracts owns version policy/native generation/document transformers. Host-local Documentation owns renderer configuration only; shipped Features/Setup never maps docs routes. Fixture hosts call those same components explicitly, without production references to tests. Host code remains independent of the other host implementation. Use existing test reflection pattern or narrowly scoped internal visibility for direct composition verification; no new public product API solely for tests.

`tests/contracts/IdentityPlatform.Api.Contracts.Tests` contains Versioning, Generation, Compatibility, Documentation and Fixtures. `/tests/contracts/` is its solution folder. `src/frontend/IdentityPlatform.Admin.Contracts` is a private package with actual generated inventories distinct from synthetic test contracts/consumers. No React shell, production demo endpoint or shared transport library.

## HTTP and generation design

Set URL-segment reader, AssumeDefaultVersionWhenUnspecified=false and ReportApiVersions=true. Helpers create explicit v1 version sets and groups `/api/v{version:apiVersion}` and `/bff/api/v{version:apiVersion}`. Supported v1 only; no fabricated sunset/deprecation date. Library-owned errors are characterized exactly; absent/unsupported route versions must not dispatch. Exclude all unversioned protocol/session/health routes from management documents.

Native Microsoft generation produces one named document for each host: `v1` and `bff-v1`, OpenAPI3.1. Native Asp.Versioning integration/Explorer resolves version metadata. Explicit surface title/server and browser/downstream authentication metadata remain distinct. Production inventories have no operations today; nonempty fixture inventories separately exercise schemas, headers, security and consumers.

Build-time export invokes actual entry points with synthetic provider/connection configuration that passes normal startup validation and cannot connect to a real service. Do not start the server, migration or bootstrap. Prove export determinism and zero external socket effects (runtime descriptor-allocation probes are counted; connect/bind/listen/send/receive are rejected); no startup bypass. Remove only documented nonsemantic output variability, never sort away missing/changed operations. Archive source/version/provenance with outputs.

## Rendering and client design

Scalar and SwaggerUI render the identical document set at `/docs/scalar` and `/docs/swagger` in test hosts. Pin bundled local assets, disable submission/auth persistence/client/proxy/AI functionality and fonts/CDNs. Apply CSP and shared slate/indigo light/dark tokens; test hostile descriptions and network/storage behavior. No production mapping switch is introduced.

Generate TypeScript from the browser contract only. A mandatory test-only typed runtime consumer performs safe synthetic read/write requests against the fixture with same-origin cookies and explicit antiforgery header; it cannot accept OAuth token input. Compile-time negative consumers reject removed/invalid DTO fields. Actual generated output stays truthful to the empty shipped BFF inventory; no public SDK package is published.

## Verification and delivery

[Acceptance inventory](test-strategy.md) is mandatory. Add reproducible generation/validation/drift/semantic mutation and npm/bundled-asset supply-chain checks to CI. Unsupported schema/security mutations must fail explicitly rather than be silently accepted. New case discovery, TRX and coverage artifacts enter strict evidence verification. Real-host absence cases use ready controls and exact404 across environments, with both real provider Aspire checks; representative schema fixtures are database-independent.

Architecture/API/test reviewers assess the design and source; security reviews renderer/browser token boundary. Fix findings, run exact-head checks, inspect GitHub comments and required review state, update PR20 title/body for final implementation and normally squash using the expected head. Never self-approve or bypass branch protections. Record all executed commands/results and remaining M02/M04/T021/certification dependencies.
