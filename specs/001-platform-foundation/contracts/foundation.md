# Foundation Contracts

Configuration: Database:Provider is PostgreSql or SqlServer, without fallback for unknown values. ConnectionStrings:platform and ConnectionStrings:adminsessions identify separate context storage; local profiles may use one database with separate schemas/histories. Identity:Issuer is canonical HTTPS without userinfo/query/fragment. Errors name invalid fields without echoing values.

GET /alive is dependency-free liveness. GET /health is readiness, failing on database/schema unavailability without returning private details. Health-only host shells do not imply implemented protocol or management features.

Migrator exit 0 means both context migrations completed; nonzero means invalid configuration or migration failure. No default production account/credential or runtime automatic migration is created.

Tests use distinct temporary resources, random credentials, bounded startup/teardown and no fallback to external data. Container failures fail tests rather than skip them.
