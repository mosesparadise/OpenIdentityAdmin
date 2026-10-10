# Research and decisions

Research uses the checked-out OpenIddict 7.7.1 source at commit `9146a148b3333570704a0211d037cd318093770a`, existing repository code, and independent security/test/UX reviewers. Findings are design evidence, not executed feature acceptance.

| Decision | Evidence/rationale | Alternatives considered |
| --- | --- | --- |
| Native request caching and token-ID keyed interaction | RestoreParametersFromRequestToken restores original parameters and ignores overlays. ASP.NET Authenticate exposes RequestTokenPrincipal in properties; main principal can be empty. | Handrolled protocol query/return URL storage duplicates validation and permits confusion. |
| Explicit S256 method set | RequireProofKeyForCodeExchange alone also permits plain in the default method set. | Client exemptions or certification mode rejected by A-001. |
| Preserve scoped native code principal/tracking | Native RedeemTokenEntry uses EF concurrency. Early consumption can precede later failure; response loss requires new flow. | Custom redemption SQL, entity detach/reload and blanket retries rejected. |
| Check current stored code scopes | Native token permission validation normally sees request scopes; code exchange often omits them. | Relying only on original registration permission misses later restrictions. |
| CheckPasswordSignInAsync plus explicit pilot eligibility | Identity owns hashing/lockout; password success alone does not prove second factor. | PasswordSignInAsync would issue cookie/enter MFA before atomic application session/audit handling. |
| Separate correlation cookie per interaction | Tabs retain separate browser binding; completed session proves this request's fresh login. | One overwritten browser correlation cookie or removing prompt on resume causes confusion/loops. |
| Fresh cookie properties | Native server Authenticate properties can contain raw protocol tokens. | Reusing them or SaveTokens in a cookie risks browser-held credentials. |
| Shared CSS tokens and feature-root Razor | Same semantic palette supports later React without adding React to STS. | Duplicated palettes drift; SPA login would add unnecessary browser protocol handling. |
| One Aspire provider test project | Shared real process/environment makes protocol/browser failures meaningful without repeated startup. | Separate TestServer persistence mock cannot prove TLS/browser/native provider behavior. |
| Pinned Playwright core plus MTP | 1.63.0 stable available; installer entry point avoids a PowerShell dependency. Trusted browser TLS must be configured explicitly. | Python bundle mismatch, certificate-bypass flags and skipped provider cells rejected. |

Source references: [OpenIddict source](https://github.com/openiddict/openiddict-core/tree/7.7.1), [Playwright browsers](https://playwright.dev/dotnet/docs/browsers), [.NET Linux dev-certificate trust](https://learn.microsoft.com/aspnet/core/security/enforcing-ssl?view=aspnetcore-10.0). The implementation verification records actual tested dependency/browser versions; package availability is not compatibility evidence.

Known environment limit: this cloud's policy previously denied the SQL Server image's redirect host. Do not bypass policy or substitute another provider; required SQL acceptance runs in the normal GitHub CI provider cell. Manual accessibility/external certification are separate evidence owners.
