# Provider contract

The public runtime contract supplies a stable identifier and EF provider identity; configures an options builder for a validated connection and logical store; exposes immutable migration metadata, model dialect and bounded connection mechanisms; and exposes an explicit local provisioning capability where implemented.

Dialect operations express trusted product-owned SQL identifiers and exact storage constraints. They do not accept end-user SQL. Connection operations validate driver compatibility, apply connection limits, generate read-only history queries, capture/set/restore lock timeout and evict unsafe physical sessions. Shared code owns cancellation, deadlines, transaction/audit boundaries and disposal.

Registration is explicit, rejects duplicate identifiers, and selects case-insensitively to a canonical registered instance. Context options carry that instance. Unknown configuration never selects another engine. Missing required capabilities fail closed with safe messages.

Provider code is trusted in-process code, not a sandbox. Implementations require conformance evidence for the product's semantics; an arbitrary EF driver does not imply compatibility. Aspire hosting is a separate optional contract, with no dependency from runtime providers.

## Review clarifications

Each catalog snapshots explicitly supplied immutable provider instances. A selected registration is an identity boundary: EF service-provider reuse and model caching include the selected instance identity, context type, logical store and design-time flag. Distinct instances sharing an ID/driver cannot reuse differently configured models. Neither identity nor diagnostic fragments contain connection material. A same-driver/different-dialect sentinel test proves separation.

Hosting adapters bind to the same canonical provider selected for every child. Before launching resources, reject missing/duplicate/mismatched adapters and requested-but-absent local provisioning. There is no fallback adapter or successful graph with skipped requested provisioning. Runtime use does not require a hosting adapter.

Reusable conformance accepts an externally supplied provider instance and a fixture that owns disposable connections/containers; the same independently expected registration/model/native-operation scenarios run for each implementation. The external public-contract example uses an arbitrary ID and existing EF driver, implements contracts directly, and has no built-in subclass/delegation or InternalsVisibleTo dependency. It is a boundary test, not a third supported engine.

Compatibility evidence uses a frozen main38eb41e producer on disposable PostgreSQL and SQL Server databases, then the new plugin migrator on those same physical databases. Compare complete history rows, schema fingerprint and representative native/product data. Building both sides with the new implementation does not satisfy this criterion. The producer source, executable inputs and observations must be identifiable in evidence.
