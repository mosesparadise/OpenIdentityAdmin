# Verification guide

Use the SDK pinned in global.json. Run `dotnet restore --locked-mode`, `python3 scripts/check-static-analysis.py --self-test` and `python3 scripts/check-ai-instructions.py`. The static command includes the Engineering tool's seeded controls and trusted-base comparison. `python3 scripts/test_ci_scope.py` checks proportional CI selection and final acceptance; `python3 scripts/test_code_quality.py` checks baseline monotonicity and strict control outcomes.

The removed legacy findings must be removed from `eng/code-quality/allowances.json`; never add changed/new findings to Legacy. The tool has no automatic baseline-refresh command. Explicit exceptions require source-bound scope, owner, rationale and expiry plus review.

## Configured OpenAPI presentation

Each Admin.Api/Admin.Bff `appsettings.json` contains `ApiContracts:Metadata:Title` (1–120 nonblank, non-control characters) and `Description` (1–2,000). The BFF additionally contains `Documentation:Viewer:PageTitle`, `BrowserDocumentLabel` and `AdminDocumentLabel` (each1–120). Standard .NET environment overrides use double underscores, for example `ApiContracts__Metadata__Title`. Unknown properties in these narrow sections are rejected. Errors identify the section without echoing its values.

These settings change presentation only. Supported API majors/document names, OAS3.1, server/asset URLs, CSP, permission semantics and route activation remain code-owned. Documentation is still unavailable in production; adding a Documentation:Enabled key does not activate it. Configured viewer text is encoded for the relevant HTML/vendor-template context.

After building, `python3 scripts/verify_contracts.py core` checks the isolated native contract suite, repeatable actual-host export, schema compatibility and generated TypeScript. The exporter explicitly selects each host's content root, overriding conflicting inherited ASPNETCORE_CONTENTROOT/DOTNET_CONTENTROOT values. Run the corresponding chromium/firefox/webkit partitions only with their pinned browsers and explicit HTTPS trust provisioned. Local browser unavailability is not a passing check; CI remains mandatory.

See verification.md and the PR for commands actually run and remaining acceptance. Current source adds configuration/hostile-content regressions; full CI acceptance is not implied by this guide.
