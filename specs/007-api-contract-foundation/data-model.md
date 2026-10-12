# Contract metadata model

No persistence entities, migrations or domain model changes.

- **Surface:** Admin API or BFF, stable document name, v1 major, OpenAPI3.1 format, own route prefix and security definition; BFF maps explicitly to downstream v1.
- **Operation inventory:** stable ID, method/path, version, request/response schemas, security/permission, preconditions and response headers. Actual and synthetic inventories have separate source/provenance labels.
- **Generated artifact:** normalized document hash, generator/compiler/validator locks, generated types and consumer test results. No machine address, credential, timestamp or private filesystem path in published contracts.
- **Compatibility result:** old/new identities, classified changes and bounded diagnostic locations. Security weakening never becomes acceptable merely because a generic schema diff calls it additive.
- **Evidence:** source head/tree, actual commands, explicit test identities/counts, provider/browser/runtime versions, artifacts and results. Missing/empty/skipped evidence fails acceptance.
