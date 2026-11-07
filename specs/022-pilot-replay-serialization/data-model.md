# Pilot coordination data

Add `platform.PilotProtocolGate` with one bounded primary-key field `Id`; the fixed
row identifies the single existing pilot profile. Use a conditional no-op update
inside an explicit transaction to acquire exclusive coordination; require exactly
one affected row. No secret, token, subject, user, grant or lease timestamp is stored.
The same transaction/context owns all winning generated entries. Its rollback
releases the gate and removes uncommitted generation without reversing independently
committed early consumption or replay revocation.

Create and seed the exact singleton in new PostgreSQL and SQL Server platform
migrations. Existing migration IDs/history tables and all native/identity data remain
unchanged. Migration insertion must work when no pilot has been bootstrapped and
must not enable the pilot. The normal Migrator applies the schema; HTTP does not
create, recreate or repair it. A missing row fails closed.

Do not attach foreign keys to the manifest, application, authorization or token
tables: the separately retained consumption child must not read or reacquire its
parent's gate. Initial validation intentionally acquires its own gate transaction;
ordinary authority contexts never query the coordination row.

Fresh install, upgrade from the current schema, repeated Migrator execution,
missing-row denial and least-privilege runtime lock acquisition need real-provider
tests. This is a coordinated internal upgrade: drain/stop old pilot issuer processes
before starting the corrected topology. Old binaries do not honor the new gate;
mixed old/new rolling deployment is not claimed safe. Production rollout remains
separately authorized.
