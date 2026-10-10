# Foundation Data Model

PlatformDbContext owns ASP.NET Core Identity user/role/claim/login/token/passkey and native OpenIddict application/scope/authorization/token tables. PlatformUser uses opaque string IDs. Future features add explicit product policy; native metadata alone never implies enforcement.

AdminSessionDbContext owns a separate bff schema and native client-stack state storage. Browser tickets/refresh coordination belong to the BFF feature. Each context has a distinct migration history; each provider assembly has both context migrations/snapshots.

Issuer protocol client IDs and scope names in PlatformDbContext use exact case-sensitive comparison, including SQL Server binary collation. Native manager descriptors, uniqueness and concurrency are verified on both providers.

The migrator applies platform then session migrations and returns nonzero on failure. Re-running resumes from EF history. Runtime services never call Migrate or EnsureCreated.

Provider values accept PostgreSql and SqlServer without case sensitivity in configuration spelling; protocol identifiers are never normalized. Empty connection strings and unknown providers are invalid. An issuer must be an absolute HTTPS URI without userinfo, query or fragment.
