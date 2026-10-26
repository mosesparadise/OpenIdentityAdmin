# T020 logout browser matrix

## Candidate scope — 26 October 2026

The user selected roadmap tasks **T020–T022**, including dependencies and tests. This increment completes the remaining T020 source matrix using the accepted PR #97 HTTPS harness. Acceptance is pending: no newly drafted browser case is claimed to have passed. T021 dedicated security mutations and T022 operations handoff follow. Full B3, production and public-release gates remain open.

Baseline is remote main `9c208a11a24b7a6aa1f529879ec8f5fb6be48d04`, verified before editing. [Prior accepted evidence](https-browser-logout.md#accepted-internal-delivery--26-october-2026) and both rejected historical runs remain unchanged. The retained PostgreSQL/SQL Server × Chromium/WebKit cells use ordinary trusted TLS, genuine fresh password/MFA, native PAR/token/callback and real independent Platform/Sessions stores. A-009 continues to defer Firefox and only its two named WebKit certificate cases; it does not waive this matrix.

## Traceability and independent observations

| New scenario | Source class | Requirement/control | Decisive observations |
| --- | --- | --- | --- |
| Foreign browser, wrong/duplicate state and replay | `SessionLogoutReturnBrowserTests` | FR-007/009/010; B3-END-03 | Unchanged full Sessions snapshot on denial; original one-use return remains usable; issuer stays live; return means unverified status. |
| Original correlation expiry equality | `SessionLogoutReturnBrowserTests` | FR-009/010; B3-END-03 | Real retained cookie denied at persisted original application-clock expiry; no extension or consumption. This is not browser wall-clock cookie-expiry evidence. |
| Absent/substituted issuer cookie, client, hint, return and confirmation cookie | `SessionLogoutBindingBrowserTests` | FR-007/010/011; B3-END-02/03 | Two genuine sources; all invalid requests preserve stores; original protected confirmation revokes only its source, with ticket/audit/generation checks and other browser still authenticated. |
| Local after-save rollback | `SessionLogoutFailureBrowserTests` | FR-008/009/011; B3-END-01 | Real save then rollback, zero commits, unchanged store, original authentication usable; explicit unfaulted subsequent form commits. |
| Local committed acknowledgement loss | `SessionLogoutFailureBrowserTests` | FR-008/009/011; B3-END-01 | Actual commit/cleanup, 503 without invented cookie deletion; explicit reconciliation adds no mutation or authority. |
| Issuer after-save rollback | `SessionLogoutRemoteBrowserTests` | FR-009/010/011; B3-END-01/02 | Local revocation persists independently; issuer stays live until explicit restored confirmation commits once. |
| Lost ensuing return navigation | `SessionLogoutRemoteBrowserTests` | FR-009/010; B3-END-03 | Actual issuer POST 302 and exact aborted return request; issuer revoked, local remains revoked and remote-pending; later original return consumes once, replay denied. |
| Distinct-site hostile forms | `SessionLogoutCrossSiteBrowserTests` | FR-007/008/010; B3-WEB-01/02, B3-END-01/02 | Issuer `localhost`, BFF `127.0.0.1`, both trusted by the development certificate; genuine copied forms submitted from real peer documents, exact foreign Origin/Referer, zero authentication cookies, 400 and unchanged effects; original same-origin forms and top-level return succeed. |
| Light/dark presentation (two cases) | `SessionLogoutPresentationBrowserTests` | FR-014; B3-WEB-03/04 | Initial rendered system palette, live system changes, explicit opposite per-origin themes, reload persistence, three viewport widths, real Tab/Shift+Tab/Enter/Space, visible focus and axe checks across actual outcomes. |

The three accepted journeys remain, with the abandoned-confirmation case now exercising an explicit fixed BFF cancellation link. Ten new cases per provider yield a proposed 13-case BrowserSessions story per provider/engine. Actual native discovery confirms 1,877 cases: all 1,857 prior portable identities and traits are unchanged, with 20 additions. All six actual provider/story filters match Accounts 8, Security 5 and BrowserSessions 13. The reviewed browser total is now 26/provider; server inventories remain unchanged. Hosted reports must establish execution before acceptance.

## Product presentation prerequisite

The BFF login/logout/error and issuer logout documents now share canonical slate/indigo tokens, responsive layout and the existing theme-only script through internal linked `BrowserDocument` source. Each assembly embeds each canonical asset once. Resource resolution requires exactly one matching fixed asset; ambiguity fails closed. Endpoint owners retain authentication, form-action and referrer policies. The login form continues to allow only self and the configured issuer origin for its native redirect. No protocol or production composition is enabled.

Exact inline SHA-256 CSP sources authorize immutable normalized asset bytes, without `unsafe-inline`. Existing script-safety tests now require exactly the independently pinned canonical theme script and reject event-handler/javascript navigation. Titles and request-derived values remain encoded. Theme storage contains only the allowlisted preference. Native forms remain usable without JavaScript; this source property does not establish a newly executed no-script browser test.

Issuer cancellation navigates only to the configured BFF `/bff/login` location, without returned state, issuer mutation or a success claim. Local revocation has already committed and remains authoritative.

## Evidence boundaries and handling

The test-only one-shot cross-site sender is served before product middleware, with a fixed peer `form-action`, `strict-origin` and no-store policy. This avoids inheriting a CSP that would prevent the hostile request from ever reaching the product. It changes no target guard, authentication or browser header. Observers retain only scalar counts, exact expected peer classification and zero-cookie assertions; they do not retain raw URLs, forms, factors, tokens or cookies.

Screenshots are restricted to eight reviewed fixed-content documents: login, logout form, local result, unknown local outcome, issuer confirmation, incomplete issuer result, unverified return and fixed error. Full-page captures include the theme header at 320 and 1280 pixels; account credential/MFA pages, browser chrome and hidden form values are excluded. Semantic/palette/reflow/axe assertions also cover 768 pixels. Screenshots require actual visual review after hosted execution. First-paint, manual screen-reader acceptance and full T012/T016 secrecy/stale-tab coverage are not claimed.

No timeouts, coverage floors, exclusions, legacy allowances or existing mutation oracles are relaxed. Native browser execution and complete required workflow/evidence review remain mandatory. Existing 660-second invocation and 20-minute job bounds remain; measured results decide whether any separate execution-layout adjustment is needed.

## Review and local verification status

Independent architecture, security, test and UX review identified and corrected inherited sender CSP, event-order-sensitive lost-return handling, incomplete two-source audit assertions, missing explicit/opposite theme coverage, missing header/form screenshots and missing explicit cancellation navigation. Architecture, security, test and UX source re-reviews pass; runtime evidence reviews remain pending. Root owns all edits and integration.

Initial local builds rejected formatting and duplicate embedding of already shared assets. Formatting was corrected and existing resource names preserved while removing duplicate embeds. The syntax-aware quality check passes without added allowances. The final solution build passes with zero warnings/errors; all 1,519 Host cases pass (zero failures/skips), including the 15 focused response-policy/framing cases. All 226 Python controls pass after correcting the aggregate browser count alongside the story inventory. Six actual native browser filters pass. Final formatting/analyzer verification passes. Hosted acceptance remains pending; no failed build or earlier inventory mismatch is treated as acceptance.

All six historical findings, 48 human rows, 345 not-assessed controls, A-009 restoration, PR #69/issue #70 and production/public-release gates remain open. This increment does not deliver refresh, current Admin API authority or React acceptance.
