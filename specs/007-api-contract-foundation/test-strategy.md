# Contract foundation acceptance design

Status: proposed acceptance inventory, not executed evidence. Concrete commands/discovered test identities will be added to the implementation plan and verification record after the structure decision.

| Case group | Boundary and oracle | Requirements |
| --- | --- | --- |
| API-VER | Supported v1 succeeds; missing/malformed/unsupported URL major has the exact documented pinned-library rejection; query/header cannot override segment; helpers/health/protocol are excluded. | CONTRACT-001 |
| API-GEN | Real host build export produces OpenAPI3.1, independent API/BFF identity and actual operation inventories; repeat generation equals normalized output; no network/DB/issuer key effects. | CONTRACT-002/003 |
| API-META | Representative Minimal APIs use production registration; unique stable operation IDs, references, nullability/composition, errors, UTC dates, enum and constraints survive export. | CONTRACT-002/004 |
| API-DIFF | Removed operation/field, new required input, tightened bounds, incompatible response/enum/nullability changes, duplicate IDs and invalid references fail. Additive direction-aware controls pass. | CONTRACT-004/008 |
| API-SEC | Removed/weakened security, `security: []`, permission loss, missing expected-version/If-Match or antiforgery metadata, accidental browser bearer/OAuth requirements fail semantic checks. | CONTRACT-005/008 |
| API-TS | Regenerate twice and compare; stale committed output fails; representative consumers compile; a mandatory test-only runtime consumer exercises URL/serialization/cookie/antiforgery behavior against the synthetic HTTP fixture; no OAuth token API/storage. | CONTRACT-005/008 |
| API-UI | Both viewers load identical contracts in a trusted-HTTPS fixture; assert rendered operation/version/schema parity, pinned local assets, CSP, no interactive calls/credential persistence/external telemetry or AI; hostile description renders harmlessly. | CONTRACT-006 |
| API-THEME | Light/dark and responsive browser checks plus automated accessibility against shared tokens; missing/blank/error viewer is never a pass. This is not T021 human acceptance. | CONTRACT-006/009 |
| API-CLOSED | Known-ready real Admin API/BFF on both provider profiles return exactly 404 for docs HTML/assets/JSON and representative management routes in Development, Staging and Production; assert health succeeds first so broken startup/connection failure cannot pass. A docs setting alone cannot activate them. | CONTRACT-007 |
| API-EVIDENCE | New project cases/artifacts enter strict discovery/execution/coverage inventory; missing report, zero cases, skips and incomplete execution fail; preserve all existing gates/deadlines. | CONTRACT-009/010 |

## Provenance and claim limits

Actual-host export/denial evidence, non-shipped representative endpoint/viewer evidence and future authenticated product evidence are separate categories. The synthetic BFF fixture may exercise the browser request shape and test access-policy mechanics, but cannot prove real sessions, current permissions, CSRF enforcement, proxy authorization or token protection. No fake login shortcut counts as a React→BFF→API→STS journey.

Retain representative nonempty fixture contracts to test otherwise unobservable generation semantics without inventing production resources. Never use an empty spec to satisfy operation/schema/client-compatibility cases. Empty actual-host inventories are checked only for accurate absence and generation correctness.

## Existing gates

Preserve real PostgreSQL and SQL Server foundation/issuer/lifecycle evidence, all current critical mutation cases, nine production coverage floors, strict MTP/TRX completeness, static analysis, license/advisory/secret checks and browser selection. Extend dependency/license/advisory inventory to the new npm lock and bundled viewer assets; existing NuGet-only checks cannot substitute for this. New tooling tests need separate bounded execution and explicit artifact discovery. Reuse existing both-provider Aspire profiles for actual-host route absence; database-independent schema fixtures need not be duplicated merely to inflate provider counts.

## Deferred real access acceptance

M02-A–D/M04 must prove genuine administrator login/MFA, valid/invalid/expired/revoked sessions, permission loss, both-provider direct bearer checks, actual same-origin allowlisted proxy routing, blocked arbitrary upstream paths, HTML/assets/JSON policy parity and no upstream OAuth token disclosure in browser/network/storage/logs. Real React consumers must use actual BFF contracts. These remain mandatory after this foundation.
