# Research and decisions

- The user's four confirmed decisions fix the host scope, hierarchy, exact web extension names, Setup convention and structural-only behavior. No unresolved architectural choice requires another question.
- Baseline: PR #7 squash `9c52c2d`; its seven checks passed. All six source Programs were inventoried before source edits. The test-only browser installer is not a production entry point.
- Keep default builder configuration precedence. AddConfigurations delegates existing hosting defaults in web hosts; it does not reload JSON or add providers. Do not introduce a bootstrap logger or coverage exclusion from screenshots.
- Use explicit feature calls rather than reflection scanning. Preserve feature hierarchy, assets, manager registrations and middleware sequence. Introduce only populated folders and small graph/lifetime helpers.
- Keep external certificate ownership explicit with an outer disposable context and inner async-disposable application. Reject provider ownership changes and temporary BuildServiceProvider calls because they alter disposal/order and validation.
- Bootstrap returns before any HTTP feature composition; its local catch-all still maps operation failures to the existing fixed message/exit 2. Protection validation remains ahead of it.
- Adapt conventions to host types: Aspire composes resources before Build and Run; Migrator performs one operation instead of starting a generic-host service loop. Empty extension hooks add no value.
- Existing process tests cover invalid configuration and protection-only startup; add valid-credential non-Development pilot denial and the real configured `/connect/jwks` absence. Existing fixtures support lifetime/graph assertions without extra distributed startups. Both-provider/browser CI remains the integration authority.
- This work preserves existing controls; all 345 ASVS register rows remain not_assessed. Feature 004 T026–T030 and public certification gates are unchanged.
