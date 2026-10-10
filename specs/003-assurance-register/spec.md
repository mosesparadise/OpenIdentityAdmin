# Feature Specification: Assurance Register Baseline

**Feature Branch**: `003-assurance-register`
**Created**: 2026-10-10
**Status**: Implemented; verification evidence recorded; PR review in progress
**Input**: Continue the next roadmap item after PR #2: establish M01-C's official security control inventory, ownership, delivery links and truthful evidence tracking before the first login feature.

## User Scenarios & Testing

### User Story 1 — Find every security obligation (Priority: P1)

As the product owner, I can inspect the complete pinned ASVS catalogue and see an initial owner, relevant delivery work and assessment status for every requirement, so obligations cannot disappear between features.

**Why this priority**: The approved assurance plan requires complete Level 1–3 inventory before feature delivery.
**Independent Test**: Compare the register with the official release and verify exactly one entry per requirement, unchanged source wording and a valid delivery/threat link.

**Acceptance Scenarios**:

1. Given the official ASVS 5.0.0 release, when the register is inspected, all 345 controls retain their source identifier and have an unambiguous version-qualified identifier.
2. Given any initial row, when an owner reviews it, its applicability rationale, role owner, task, threat/requirement, enforcing component, evidence fields, residual risk and reassessment trigger are visible; no control starts as passed.
3. Given deployment-dependent or unused functionality, when applicability is uncertain, the row remains pending review with a reason; silence or absent implementation does not become an exclusion.

### User Story 2 — Prevent misleading evidence (Priority: P1)

As a maintainer, I receive an actionable failure when a register edit loses a control, changes official content, breaks traceability or reports unsupported completion.

**Why this priority**: A complete-looking spreadsheet can otherwise hide missing or fabricated assurance.
**Independent Test**: Run the normal validation and seeded invalid cases locally and in CI.

**Acceptance Scenarios**:

1. Given unchanged valid data, validation succeeds and explicitly reports that this is register integrity, not product compliance.
2. Given missing/duplicate/unknown controls, source corruption, invalid statuses or broken local references, validation fails.
3. Given a claimed pass without implementation, dated evidence and assessor identity, or an exclusion without approval/rationale, validation fails. An approved exception is distinct from a pass and requires owner, expiry and residual risk.

### User Story 3 — Understand release gaps (Priority: P2)

As the product owner, I can distinguish an internal demonstrator from the full assurance target and find the remaining human decisions and external gates.

**Why this priority**: Password-only pilot acceptance must not imply L3 or protocol certification.
**Independent Test**: Review the status summary, known release tensions and roadmap links against approved v0.2/A-001.

**Acceptance Scenarios**:

1. Given the initial register, the documentation reports pending evidence, named responsibility roles needing human assignment, and preserved independent assessment/certification gates.
2. Given L3 requirements for MFA, PAR, sender constraints, asymmetric client authentication or hardware-backed cryptography, each remains visible as pending implementation/design disposition; no requirement is silently waived.

### Edge Cases

Changed source bytes; duplicated/misspelled identifiers; malformed records; unknown threat/task/requirement links; future or malformed dates; missing evidence files; path traversal; expired exceptions; conditional features; source wording errors; unknown deployment facts; synthetic positive test fixtures mistaken for actual control passes.

## Requirements

### Functional Requirements

- **REG-001**: Preserve all 345 official ASVS 5.0.0 requirements unchanged with immutable source provenance and attribution.
- **REG-002**: Give every requirement the complete approved register fields and a conservative initial applicability, owner, delivery and threat/requirement mapping. Unassessed controls remain pending.
- **REG-003**: Provide a repeatable integrity gate that rejects missing/duplicate/foreign identifiers, corrupt source, invalid records and broken traceability.
- **REG-004**: Reject unsupported pass/exclusion/exception transitions; show expired exceptions as failures. Structural validation never establishes the truth of evidence or assessor independence.
- **REG-005**: Publish a readable maintenance guide, threat inventory and pending release/design dispositions; retain all approved external gates and distinguish role assignment from named ownership.
- **REG-006**: Cover gate failure modes with permanent regression tests, wire validation into CI and record actual independent reviews and verification.

Product traceability: FR-21, FR-22, FR-25; NFR-01, NFR-11, NFR-13, NFR-14; roadmap M01-C. This increment delivers the register baseline. Applicability refinement, implementation evidence, normative OAuth/NIST mappings and independent assessment continue with their owning features; the whole M01-C and assurance programme remain incomplete.

### Key Entities

Official catalogue; control assessment row; initial threat; responsibility role; delivery task; evidence reference; exception decision.

## Success Criteria

- **SC-001**: Every one of the 345 official controls appears exactly once with all required fields; no initial row claims pass or exclusion.
- **SC-002**: Each specified corruption/unsupported transition has a regression that fails validation; the valid baseline succeeds.
- **SC-003**: Every control links to an existing roadmap task, requirement and threat; users can identify its pending work without inferring compliance from test results.
- **SC-004**: Source attribution, known release tensions and ongoing assessment obligations receive independent architecture, test and compliance review with findings resolved or explicitly outstanding.

## Assumptions

- v0.2 and A-001 authorize this routine assurance work; no product policy changes or external submissions are authorized here.
- Security, deployment, privacy and independent assessor roles have no named appointees yet. The register records that gap rather than inventing people.
- This is repository tooling/documentation; it changes no runtime, database schema, browser flow or API. Existing product/provider regression CI remains in place.
- Final applicability decisions and actual evidence are feature/deployment/assessor work. Conditional controls stay visible until a justified reviewed disposition exists.
