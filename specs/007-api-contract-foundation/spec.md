# Feature Specification: Management contract foundation

**Feature Branch**: `017-api-contract-foundation`

**Created**: 12 October 2026

**Status**: User confirmed foundation-first sequencing and proposed structure on 12 October 2026. Detailed design and implementation are proceeding under v0.2 approval.

**Input**: Proceed to M01-F after M01-E, preserving approved versioning, documentation, generated-client and security requirements.

## Authority and present capability

Approved v0.2 FR-16 and NFR-14 authorize management contract tooling. FR-11 and NFR-08 preserve independent API protection and server-held browser tokens. This feature does not amend those requirements. The [approved contract](../../docs/approved-v0.2/04-data-api-contracts.md) remains authoritative.

On current `main` (`0c1de1e`), Admin.Api and Admin.Bff register storage and health only. There is no real administrator bootstrap/MFA, BFF login/session, downstream authorization/proxy, management resource or React application. M02-A–D supply those security prerequisites. Test fixtures cannot establish their acceptance.

**Proposed boundary:** implement and verify contract tooling now; keep runtime documentation HTML, assets and JSON routes unmapped in shipped hosts. Exercise representative contracts and documentation renderers in explicitly non-shipped fixtures. Later M02/M04 work must activate and accept the real protected browser documentation path and resource contracts. An empty generated host document is an accurate inventory, not evidence that a management API exists.

## User Scenarios & Testing

### User Story 1 — Know which contract a client uses (Priority: P1)

An integration developer can distinguish a supported management API version from the browser contract and identity protocol routes.

**Why this priority**: Silent version fallback or confusion between browser cookies and downstream bearer authentication creates incompatible and unsafe clients.

**Independent test**: Exercise explicit version selection using representative non-shipped operations and separately inventory the actual hosts.

**Acceptance scenarios**:

1. A supported version identifies exactly its operations, schemas and authentication boundary; each browser operation names its explicit downstream major mapping.
2. Missing, malformed and unsupported versions receive documented rejection and never execute an operation through default/latest fallback.
3. Protocol, callback, session-helper and health routes do not acquire management prefixes or appear as management resources.
4. Current production hosts contain no invented fixture operations and expose no runtime documentation URL.

### User Story 2 — Detect incompatible changes before merge (Priority: P1)

A maintainer can reproduce contract output and reject changes that break supported clients or weaken their documented security boundary.

**Why this priority**: Generation alone can publish syntactically valid but unusable contracts.

**Independent test**: Generate twice, compile a browser consumer and seed both breaking and additive changes against reviewed baselines.

**Acceptance scenarios**:

1. Identical input yields identical normalized documents and generated types; stale checked-in output fails verification.
2. Missing/duplicate operation IDs, removed operations, incompatible schemas, changed required inputs, stricter bounds and changed authentication/preconditions are detected. Additive cases are evaluated directionally rather than accepted solely because a field is optional.
3. A mandatory test-only runtime consumer of the generated BFF contract exercises the synthetic HTTP fixture and proves its URL prefix, same-origin cookies and explicit antiforgery semantics; clients never accept or persist server-held access/refresh tokens.
4. Empty production inventories and representative fixture inventories have distinct provenance. Fixture success cannot mark real resource acceptance complete.

### User Story 3 — Read consistent, non-interactive documentation (Priority: P2)

A developer sees consistent versions and operations in either documentation viewer, with no credential entry, external proxy dependency or request-execution controls.

**Why this priority**: Both viewers must consume the same contract without introducing a second schema authority or a browser-token shortcut.

**Independent test**: Browser automation loads both viewers against non-shipped representative contracts and observes network/storage activity. Separately prove shipped hosts expose no documentation routes.

**Acceptance scenarios**:

1. Both viewers display matching document/operation sets and nullable, composed, date, enum and error examples.
2. Assets are served locally under compatible CSP; no try-it, credential prefill/persistence, unapproved external calls or optional AI/agent features operate.
3. Shared slate/indigo light/dark presentation receives responsive and automated accessibility checks.
4. Later activation requires the real authenticated BFF and allowlisted downstream proxy, including HTML/assets/JSON policy parity, session expiry, permission loss and direct-call denial. This scenario remains explicitly deferred to M02/M04.

### Edge Cases

- Query/header version values cannot override URL-segment selection; no default-major fallback.
- Documents cannot contain machine-specific hosts, timestamps, paths, fixture credentials or private configuration.
- Request/response direction, enum expansion, nullability, composition and missing preconditions cannot disappear in a simple textual diff.
- Missing or empty evidence cannot become a passing compatibility/browser check.
- Build-time export cannot contact databases, bootstrap users, load real issuer keys or start external services.
- A configuration flag alone cannot enable documentation before its real security prerequisites exist.
- New contract cases cannot consume the nearly exhausted issuer partition budget or weaken existing deadlines.

## Requirements

| ID | Requirement | Traceability / acceptance |
| --- | --- | --- |
| CONTRACT-001 | Explicit management major selection; distinct API/BFF ownership and mapping; protocol/helper/health routes excluded. | FR-16; US1 |
| CONTRACT-002 | One authoritative generated document per surface/major; supported format, deterministic offline export and honest operation inventory. | FR-16, NFR-14; US1/2 |
| CONTRACT-003 | Locked compatible generation, validation, diff, viewer and client tools; no peer-dependency override hiding incompatibility. | NFR-14; US2/3 |
| CONTRACT-004 | Unique operation IDs, bounded inputs, typed responses/errors, permission/precondition metadata and synthetic examples in representative tests; later actual operations meet the same rules. | FR-16; US2 |
| CONTRACT-005 | Browser contracts describe cookies/antiforgery; downstream contracts describe bearer access. Generated consumers cannot expose server-held tokens. | FR-11, NFR-08; US1/2 |
| CONTRACT-006 | Both non-interactive viewers consume the same documents with pinned local assets, compatible CSP and shared theme. | FR-16/19, NFR-09/12/14; US3 |
| CONTRACT-007 | Shipped hosts expose no docs HTML/assets/JSON until genuine administrator protection and BFF proxy acceptance exists. | FR-11, NFR-08; US1/3 |
| CONTRACT-008 | CI rejects drift, invalid inventories and seeded breaking changes; generated TypeScript compiles and request behavior is tested. | FR-16, NFR-11/14; US2 |
| CONTRACT-009 | Preserve provider/browser/coverage/static/supply-chain gates; extend evidence discovery for added cases. | NFR-02/11/12/14; all stories |
| CONTRACT-010 | Record fixture/production provenance, independent reviews, deferred acceptance and exact delivery evidence. No premature FR-16/M01-F completion claim. | Governance; all stories |

### Key Entities

Contract inventory: surface, supported major, format, source revision, operation identities and schema/security metadata. Compatibility baseline: reviewed prior inventory and classified changes. Generated artifact: source document identity, locked tool versions and reproducible output. No new persistence entity or migration is required.

## Success Criteria

- **SC-001**: Every explicit version-selection/rejection case has its documented result; actual protocol/health routes retain existing behavior.
- **SC-002**: Two clean generations agree after documented normalization. Stale output, each seeded breaking mutation and missing evidence fail; reviewed additive controls pass.
- **SC-003**: Both viewers display matching operations/versions and schema semantics with zero forbidden external requests, token disclosure or interactive API requests.
- **SC-004**: Every documentation URL class remains unavailable in actual hosts across both provider profiles and deployment environments; no fixture operation ships.
- **SC-005**: Required existing/added checks pass with zero skipped or incomplete required cases, independent findings resolved and traceable squash delivery.

## Dependencies and unresolved decisions

- Confirmed: build the foundation now; authenticated documentation remains a tracked M02/M04 dependency.
- Confirmed: use the feature folders, dedicated contract test project/solution grouping and frontend contract package in decision-proposal.md.
- T021 human accessibility and A-001 external certification remain independent open obligations; this work does not weaken their gates.
- Package availability research is not a validated dependency lock. Implementation requires full graph validation and meaningful tests.
