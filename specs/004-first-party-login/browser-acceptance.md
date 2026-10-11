# T027 — Browser and theme acceptance

The user authorized T026–T028 sequentially, each with a reviewed, corrected and squash-merged PR. T026 merged in PR #9 as `7d1a9bc`; this increment completes only T027 under approved LOGIN-007/009, SC-004 and the existing browser cadence. Requirements checklist remains 7/7. T028–T030 and manual/external acceptance stay open.

## Execution design before implementation

Keep the existing projects and production composition. Extend `Issuer.Tests/Features/Browser` with small explicit browser/session and screenshot helpers. Use the genuine HTTPS relying party, protected login form, native OpenIddict callback and server-side exchange/validation. Browser state contains only cookies and the theme preference. Never expose tokens, passwords or connection details in captures or assertion output.

| Obligation | Planned evidence |
| --- | --- |
| Actual TLS trust | Each selected engine accepts issuer and relying-party HTTPS, then rejects a valid-time, matching-loopback-host unrelated server certificate with a certificate-specific error. No HTTPS bypass. Firefox gets a disposable NSS profile with an explicit trust-anchor import of the exact synthetic certificate; Chromium uses the explicit NSS import; WebKit uses the Linux trust store. Validate these mechanisms in CI. |
| Existing login and layout | Retain keyboard login/CSP/cookie/storage assertions and all light/dark × 320/1280 reflow/axe cases. Strengthen autocomplete/labels and keyboard checks without asserting secret values. |
| Generic error and recovery | Real incorrect-password submission, generic summary focus, cleared password, unchanged safe layout, successful correction. Credential-empty error captures in both themes and widths; no values retained in images. |
| Expired/unavailable interaction | Expire only the synthetic persisted interaction through a fresh database scope, then prove terminal safe UI and a new relying-party journey succeeds. No production mutation route. |
| Theme behavior | System color preference, saved explicit preference before first render, media change, invalid preference and unavailable storage; reduced-motion and 320 CSS-pixel reflow (equivalent 400% reflow width, not a claim of native browser zoom testing). |
| Freshness | Existing browser session followed by real prompt=login and max_age=0 requests; each requires a form and completes one callback/exchange without redirect loops, retaining original authenticated-time validation. |
| Visual review | Canonical Ubuntu 24.04 / pinned Chromium empty-login and error images, both widths/themes. Missing or changed baselines fail; inspect every captured image before explicitly committing it. Firefox/WebKit execute the same semantic/axe/layout journeys and retain safe captures, but do not compare engine-specific font rasterization against Chromium pixels. |

## CI partition and cadence

The slowest T026 SQL issuer invocation was 654.846 seconds under its unchanged 660-second deadline. Move its six existing browser cases into dedicated browser-only jobs; retain all 181 HTTP issuer cases per provider. Unit/Persistence/AppHost selections remain unchanged. Expand browser cases in their isolated invocation, retaining 660 seconds per provider/browser and exact expected case counts. Keep the same project and real Aspire orchestration. Explicit mutually exclusive trait filters plus partition/cadence regression checks guard dropped, duplicated or silently skipped cases.

Chromium × PostgreSQL/SQL Server is required on every PR and main push. Firefox and WebKit × both providers run nightly, on dependency/browser/workflow changes, and on manual dispatch. This PR changes browser/workflow code and must demonstrate all six browser cells before merge. A setup job derives the explicit matrix from the event and changed paths; no API permission/token is needed to read a pull-request diff. Combined coverage depends on every selected test job, includes all child-process reports and preserves all nine 90% line / 80% branch floors. Provision downloads and Linux certificate trust before tests; never download during discovery. Artifact names include provider and browser to avoid overwrites. Retain only synthetic TRX, coverage and credential-empty screenshots for seven days.

## Acceptance checklist

- [x] BR-01 Review this bounded design for test completeness, architecture, security and UX/Aspire execution cost.
- [ ] BR-02 Implement browser selection/trust controls and strict test/CI partition with regression checks.
- [ ] BR-03 Implement meaningful error, theme, terminal recovery and freshness journeys on both providers.
- [ ] BR-04 Inspect and approve every new canonical baseline; retain accessibility/manual limits.
- [ ] BR-05 Pass static, assurance, supply-chain, unit, HTTP providers, six selected browser cells and combined coverage with zero skips; correct independent and GitHub findings.
- [ ] BR-06 Record exact source evidence, publish a Conventional Commit PR and squash its verified final head before T028.

No automated result constitutes complete WCAG conformance or password-manager/screen-reader acceptance. Qualified manual review, external certification and T030 concurrent interaction capacity remain explicit obligations. Local browser downloads are blocked by the managed network policy; CI execution and retained artifact inspection provide browser evidence without bypassing that policy.

Design dispositions: independent architecture, test, security and UX/Aspire reviewers accepted this bounded design. Incorporated explicit engine trust, test-identity partition validation, fail-closed cadence, known-user lockout isolation, same-tab issuer storage checks, token canaries kept only in the test RP, pre-continuation session/time oracles, and sanitized browser failures. Added two representative terminal layouts and JavaScript-disabled manual continuation. These are source-design dispositions; execution remains pending.

First binary feedback: Firefox does not honor NSS peer trust as a trust anchor. Its private profile now imports the exact synthetic certificate with `C,,`, while Chromium retains `P,,`; unrelated certificate rejection remains mandatory. No global Firefox policy or product trust changes. Chromium/WebKit exposed a redirected-request interception mistake in the freshness oracle; observe the newly initiated resumed authorization instead, require one unconsumed credential snapshot and nonempty session/time.
