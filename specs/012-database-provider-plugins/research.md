# Research

The current coupling includes EF registration, model collation/check SQL, readiness catalogs/queries, ceremony session lock limits, account connection limits, local role DDL, design-time factories and Aspire resource creation. Moving only UseNpgsql/UseSqlServer would leave replacement implementations incomplete.

Independent inventory reviews identified 44 migration/designer/snapshot artifacts and 20 migration IDs (8 platform and 2 session per provider). Keep these byte-for-byte. Provider runtime projects must not reference migration assemblies (cycle and runtime privilege boundary) or Aspire. Separate hosting adapters preserve native container orchestration. Shared product code retains UPDATE lock order and exact-one-row checks.

Use an options extension to carry an immutable selected provider into both contexts. This avoids global registries and runtime BuildServiceProvider while preserving options-only construction. Reject missing registrations and incompatible EF/connection configuration. Driver-specific session settings are mechanisms; deadlines and disposal ownership stay in shared product code.

Conformance requires positive native operations and denial/failure evidence. Existing both-provider manager, schema, grants, upgrade, transaction, governance and session suites are foundations. Add an independent external implementation registration example and migration compatibility assertions. No third engine is introduced.
