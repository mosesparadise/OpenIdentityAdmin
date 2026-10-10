# Startup compatibility contract

| Boundary | Required compatibility | Evidence |
| --- | --- | --- |
| Web storage | Provider/connection validation, fixed invalid-database stderr, exit 2, storage readiness with five-second timeout; no schema writes | MigratorConfigurationTests, HealthEndpointTests, both-provider LocalProfilesTests |
| Auth protection | Optional profile remains health-only; complete credentials verified before listener; fixed errors/exit 2; app disposed before certificates, including early failure | IssuerStartupTests, CredentialTests, new composition lifetime cases |
| Pilot | Explicit Development-only flag; unchanged safe denial with valid credentials outside Development; correct configured JWKS route absent while disabled | PilotPolicyTests/IssuerStartupTests |
| Bootstrap | Strip only exact --initialize-pilot before CreateBuilder; verify protection; require enabled pilot; one-minute cancellation; bounded scoped DB operation; success 0/failure 2 with existing fixed text; never map/start HTTP | Existing Aspire bootstrap completion plus targeted process assertions and BootstrapAcceptanceTests |
| Protocol/accounts | Native handlers, registration order, named schemes, middleware order, S256, diagnostics, limits, routes, Razor root and embedded resources preserved | PilotRegistrationTests, PilotFailureTests, all HTTP and six Chromium cases per provider |
| Migrator | Validate local-only flag/passwords before persistence; fixed invalid-configuration error/2; upgrade 0/1 with three-minute cancellation; never Host.Run | MigratorConfigurationTests, DatabaseFailureTests and both-provider migration/profile suites |
| Aspire | Existing resource names, pinned images, runtime/admin connection separation and secret recipients; runtime waits on successful migration, auth additionally on successful bootstrap; local Development only | PilotCompositionTests, ProfileIsolationTests, LocalProfilesTests and issuer fixture startup |
| Verification | Existing coverage inclusion/exclusions and nine module floors; no skips or timeout increases to hide regressions | Seven CI gates and per-module coverage report |

No public management API/version/OpenAPI contract changes. Protocol paths stay unversioned. No new configuration keys or environment variables.
