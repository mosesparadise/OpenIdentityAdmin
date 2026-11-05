# Hosted lifecycle verification

## First implementation candidate — 5 November 2026

Source `b7886ff6cf87d0cab541ecdc2e8412b8f4b8ea80` was submitted as tested merge
`1d7b2436ba0ae157b36e79f44eca927ddd2a5f77`, tree
`4c9e8b975e246b46e43979a114236c9d6d3b78ef`, against main
`b27669f04ad5992ae829c1250bd13f90e8c72446` in
[run516](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/41946316577).
This candidate is **not accepted**.

The ordinary PostgreSQL/SQL Server foundation jobs failed at full native discovery, before database
execution. `eng/test-matrix.json` correctly expected21 persistence cases/provider, but
`eng/persistence-test-matrix.json` still contained19/provider and omitted the four new
`LifecycleSchemaTests` identities. The shared portable-metadata validator failed closed. Its
administrator-named module is also used for persistence; the error did not prove an administrator
runtime failure. Jobs126979358906 and126979358940 independently reported this same omission.

The correction appends exact actual native discovery metadata for the four lifecycle schema cases.
All38 prior names and complete type/trait metadata remain unchanged. Full discovery and exact
provider selections now verify42 total/21 per provider, matching the matrix. All74 selection controls
pass. No identity checker, count, namespace, provider or skip rule is weakened.

Static analysis job126979357979 independently found four unnecessary using directives in
`ApplicationLifecycleTests`, `ApplicationLifecycleRaceTests`, `ManagedLifecycleRaceTests` and
`ManagedSchemaTests`. Only those directives are removed. The full local
`dotnet format style --no-restore --severity info --diagnostics IDE0005 --verify-no-changes`
then passes. No product behavior or native test assertion changes in these corrections.

An independent hosted reviewer read complete paginated job inventories and exact source/merge
metadata through the approved GitHub connector. Direct shell API access remains forbidden; raw
artifact downloads are unavailable and were not retried. Partial successes or locally passing
feature suites do not replace corrected exact-head hosted acceptance. Later candidate results must
record their own source/tested merge, complete jobs, summaries and evidence-access limits.
