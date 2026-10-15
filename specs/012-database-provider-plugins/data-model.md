# Data model

No persisted entity or schema change. Existing PlatformDbContext and AdminSessionDbContext identities remain. Schemas are platform and bff; histories are PlatformMigrations and SessionMigrations. Migration assembly identities remain IdentityPlatform.Migrations.PostgreSql and IdentityPlatform.Migrations.SqlServer.

In-memory contracts describe a registered provider, the two logical stores, immutable ordered migration catalogs, model dialect, connection policy and optional Development provisioning/hosting capabilities. These descriptors grant no application authority. Product entities, invariants, native stores and audit relationships remain owned by Infrastructure.
