# Foundation Research

Decision: SDK 10.0.401/runtime 10.0.12 and Spec Kit 1.1.3. Its annotated tag object bea765ce48b383c811872bfa2761a1e9600b7f32 peels to source commit 0282d03eb6fc3091b3deaa3a855d4030dc40cd6c; record both, rather than calling the tag object a source commit. Initialization uses bundled Codex skill templates.

Decision: native Identity/OpenIddict EF model in PlatformDbContext and separate BFF context/schema. Use native manager writes and provider-specific migrations for both contexts. SQL Server protocol identifier columns use binary collation; verify PostgreSQL semantics with real queries. Reject provider-neutral migration assumptions and mocked database equivalents.

Decision: Aspire 13.6.1 uses explicit provider selection, Migrator completion before dependent services, health readiness and isolated resources. Testcontainers 4.16.0 verifies native persistence; distributed tests verify process boundaries. No generic security-mutation retries.

Decision: safe configuration errors identify fields without echoing credentials. Only explicit known providers and canonical HTTPS issuers are accepted. Runtime hosts never migrate automatically.

Decision: mandatory PKCE remains. Official suite release-v5.3.2 source confirms Basic OP usually omits PKCE with no global setting. Evidence and proposed sequencing are in docs/amendments/A-001-certification-sequencing.md. No suite was executed and no external communication sent. Infrastructure work does not depend on issuer acceptance.

## xUnit v3/MTP and grouped layout update

NuGet stable indexes and package manifests inspected on 2026-10-10: `xunit.v3.mtp-v2` 4.0.2 is the xUnit v3 product package explicitly targeting MTP v2; its dependency graph pins the framework/assertions 4.0.2 and requires MTP/MSBuild/TRX abstractions 2.5.1. Add `Microsoft.Testing.Extensions.TrxReport` 2.5.1 for the actual reporter. Select native MTP with `global.json` `test.runner`; executable projects use `UseMicrosoftTestingPlatformRunner=true`. Remove xUnit v2, VSTest adapter, Microsoft.NET.Test.Sdk and Coverlet collector. External pinned dotnet-coverage continues collecting child hosts.

Use row-level `Provider` traits on the existing theories, `--pre-enumerate-theories on`, and `--filter-trait Provider=PostgreSql|SqlServer`. The unit AppHost subset uses `Category=Harness`. This selects stable metadata rather than argument display-name formatting. Native MTP uses `dotnet test --project`, `--report-trx`, `--report-trx-filename`, and `--results-directory`; no VSTest compatibility path or `--logger` is retained. Verify actual selected case sets and report completeness before acceptance.

Sources: [xUnit MTP guide](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform), [xUnit 4.0.2 source](https://github.com/xunit/xunit/tree/0c7aa4991fa340689155d52043ce88f52f2f7421), [native .NET MTP command](https://learn.microsoft.com/dotnet/core/tools/dotnet-test), and NuGet flat-container manifests for the exact packages above.

Responsibility groups improve navigation without changing assembly identities or feature boundaries. Solution folders mirror physical paths; the future React client is explicitly reserved, with Razor Pages remaining in AuthServer. Earlier immutable approval/evidence snapshots retain historical paths.
