# Provider implementation guide

The runtime extension point is `IRelationalDatabaseProvider` in `IdentityPlatform.Database.Abstractions`. A project implementing this contract references that project and its chosen relational EF Core driver. It does not need to inherit from either shipped provider or modify shared persistence. The supported built-ins remain PostgreSQL and SQL Server; an arbitrary EF provider still needs a compatible dialect, migrations and passing conformance evidence.

## Reference and register

Add your provider project reference to each consuming host and Migrator. In each host's `Composition/DatabaseProviderRegistration.cs`, register one immutable instance alongside any providers that host supports:

```csharp
using IdentityPlatform.Database;
using Project.Persistence;

internal static class DatabaseProviderRegistration
{
    internal static DatabaseProviders Providers { get; } =
        new(new ProjectDatabaseProvider());
}
```

Set `Database:Provider` to that implementation's `Id`. Existing `PostgreSql` and `SqlServer` values remain compatible. Selection trims surrounding configuration whitespace and ignores case; unknown or duplicate identifiers fail at startup. For a project composing persistence directly:

```csharp
var providers = new DatabaseProviders(new ProjectDatabaseProvider());
var provider = providers.Select(configuredProvider);
services.AddPlatformPersistence(provider, platformConnection, sessionsConnection);
```

Connection strings come from the host's normal secret configuration. Registration is explicit: there is no assembly scanning or fallback to a default engine. The provider instance is part of EF service/model cache identity; do not mutate its settings after registration.

## Implement the contract

`Configure` selects exactly one EF relational driver, its existing migration assembly, the logical store's schema/history table, and a bounded command timeout. `DatabaseStores.Schema(store)` and `DatabaseStores.History(store)` expose the fixed product names. Keep retries disabled for security mutations. Return the driver's assembly name in `EfProviderName`, a non-secret design-time connection template, and the exact ordered migration IDs from `Migrations(store)`.

Implement `IDatabaseModelDialect` for ordinal identity/client lookup and the product's constraints. Implement `IDatabaseConnectionPolicy` for driver compatibility checks, bounded readiness connections, exact ordered history queries, session lock timeout capture/set/restore, transaction-local lock limits where supported, and unsafe pool eviction. Shared infrastructure retains transaction ownership, authorization, cleanup and cancellation. The two context constructors reject a missing registration, a wrong logical store, mixed EF drivers or an incompatible supplied native connection before use.

`LocalProvisioning` is optional. Its implementation creates the two narrowly privileged Development runtime principals using `LocalRolePolicy`; the Migrator rejects requested local provisioning when the capability is absent. It never implies permission to provision a production database.

See the shipped provider projects for complete implementations. The test-only `ExternalDatabaseProvider`, `ExternalModelDialect` and `ExternalConnectionPolicy` implement the public contracts directly, without inheriting from or delegating to a shipped provider. That fixture proves the extension boundary; its empty migration catalogue does not qualify another engine for use.

## Optional Aspire adapter

For local containers, create a separate project implementing `IDatabaseHostingProvider`. Register its instance in the AppHost's `DatabaseHostingProviders` catalogue. The adapter must return `DatabaseResources` carrying the exact runtime provider instance and supplied secret resources. It must support the Development provisioning capability. Runtime hosts never reference Aspire adapter assemblies. An externally managed database needs no Aspire adapter.

## Reusable verification

Provider authors can reuse three layers:

1. `RelationalProviderConformance`: both logical stores, migration configuration, bounded commands and store isolation without a running database.
2. `NativeProviderConformance`: manager-owned identity/client operations, ordinal lookup, both runtime readiness checks, qualified permission denials, and nested connection/lock restoration. Supply a `NativeProviderFixture` with independently defined engine observations and disposable runtime principals.
3. `DatabaseCompatibilityConformance`: fixture-owned physical database identity, complete schema/history/data fingerprints, unchanged EF models and repeated migrator runs. Populate the database using the previous implementation before invoking the helper. Supply a fixture-owned fingerprint callback that independently covers schema definitions, complete migration history and persisted data; it must return a nonempty value and must not derive expected observations from the provider under test. The built-in `DatabaseSnapshot` implements this for PostgreSQL/SQL Server. Another engine can supply its own metadata inspection without requiring `information_schema` or changing shared assertions.

The built-in compatibility cases use a separate process compiled against frozen commit `38eb41e06d40b6addbe57e3af839f912645e6dea`. `python3 scripts/prepare_provider_baseline.py` verifies that checkout, builds the old producer and emits a bounded hash manifest of its entire executable directory. CI supplies `PROVIDER_BASELINE_PRODUCER` and `PROVIDER_BASELINE_MANIFEST`; neither test silently substitutes the current implementation. The ordinary PostgreSQL and SQL Server foundation jobs execute these cases against pinned disposable containers.

## Existing databases

The reorganization keeps all 44 generated migration/designer/snapshot files and both migration assembly names. Do not reset databases, regenerate prior migrations, rename history tables, or copy history rows. Existing PostgreSQL and SQL Server configurations keep selecting the same engine and schema. Production upgrade execution remains a separately authorized operational action.
