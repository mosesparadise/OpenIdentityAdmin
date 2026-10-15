# Feature Specification: Project-level relational database providers

**Feature Branch**: `058-database-provider-plugins`
**Created**: 15 October 2026
**Status**: Approved scope; implementation pending
**Authority**: [A-008](../../docs/amendments/A-008-database-provider-plugins.md), including the user's three explicit clarification answers.

## User Scenarios & Testing

### User Story 1 — Register a replacement (P1)

A project author references an implementation project, explicitly registers its provider, and selects its identifier in configuration. No changes to shared product persistence or a central provider switch are required.

Independent test: a test-owned implementation outside the shipped provider assemblies configures both contexts through the public contracts. Missing, duplicate, unknown or mismatched registrations fail safely before storage use.

### User Story 2 — Retain existing databases (P1)

Existing PostgreSQL and SQL Server installations use the reorganized implementations against their existing stores. No reset, data transfer, schema change or migration-history rewrite occurs.

Independent test: compare all 44 migration artifacts byte-for-byte, assert no pending model changes, and apply the reorganized migrator to pre-existing stores while preserving history and representative native/product data.

### User Story 3 — Qualify another implementation (P2)

A provider author reuses conformance scenarios for configuration, model identity, schema readiness, least privilege, transaction/lock behavior and native manager operations. Reference alone does not establish support for an arbitrary database engine.

## Requirements

- **FR-001** Explicit immutable registration and canonical provider selection support arbitrary project-owned relational EF Core implementations without a closed enumeration or shared vendor switch.
- **FR-002** Move existing EF configuration, connection validation, dialect expressions, schema catalog, connection/lock mechanisms and local role mechanisms into the appropriate provider implementation. Keep domain decisions, transaction ownership, audit atomicity and lock order in product code.
- **FR-003** Preserve context and migration assembly identities, all 20 migration IDs, all 44 generated artifacts, schemas, history tables and existing data. Generate no new migration.
- **FR-004** Preserve native Identity/OpenIddict managers, disabled entity caching, no implicit retries, safe startup failures, exact timeout restoration and physical-session discard on failed restoration.
- **FR-005** Preserve bounded unpooled readiness, exact ordered history comparison, Development-only privileged provisioning, password-log suppression and least-privilege runtime credentials.
- **FR-006** Keep runtime implementations independent of Aspire and executable migrations. Explicitly register providers in every consuming host. Preserve Aspire resource identities, image pins, credential recipients and migrator completion dependencies.
- **FR-007** Supply reusable conformance tests and an external test-owned registration example; run existing mandatory tests on both supported databases and add coverage floors for handwritten modules.
- **FR-008** Document implementation, reference, registration, migrations and optional local hosting, then resume feature 011 T005 after accepted delivery. Preserve all external gates and A-007.

### Edge cases

Unknown/blank/duplicate identifiers; mismatched EF provider/connection; missing capabilities; cancellation during lock acquisition; nested connection ownership; failed session restoration; extra/missing/reordered history rows; malformed connection strings; accidental runtime privilege or migration assembly dependency.

## Success Criteria

- **SC-001** An external test-owned implementation configures both stores without editing shared persistence code.
- **SC-002** Both existing providers retain unchanged generated artifacts and compatible runtime/design-time models; existing databases survive repeated upgrade unchanged.
- **SC-003** Existing security, native integration, mutation and required CI gates pass on the exact reviewed head; no exclusion or weaker oracle hides provider code.
- **SC-004** Independent architecture/test, security and Aspire reviewers resolve concrete findings; delivery evidence distinguishes execution from planned work.

## Clarifications

All scope questions are answered in A-008. No NoSQL abstraction, third database, generic repository, production migration operation or release acceptance is included.
