# A-010 — B3 native form document policy

**Proposed, not approved — 26 October 2026.** No production code or approved contract changes are included in this proposal. The requesting user has authorized roadmap implementation and ordinary failure correction; this document requests a specific exception to the explicit `no-referrer` security requirement. The implementing maintainer owns implementation and evidence after approval.

## Problem and evidence

The first real HTTPS B3 journeys in [PR #97](https://github.com/mosesparadise/OpenIdentityAdmin/pull/97) fail before issuer password entry in all four PostgreSQL/SQL Server × Chromium/WebKit cells. All 4,377 prior executions pass unchanged; all twelve new executions fail. [The rejected-run record](../../specs/013-administrator-browser-session/browser-form-policy-evidence.md) binds the source, raw artifacts and limits.

Independent local Chromium native-form reproduction demonstrates two browser behaviors:

1. `Referrer-Policy: no-referrer` makes native form POSTs send `Origin: null`. The required exact-origin guard correctly rejects that value. `strict-origin` retains the exact origin and sends only the origin in `Referer`, without the page path or query.
2. `Content-Security-Policy: form-action 'self'` blocks the demonstrated native POST's cross-origin redirect. Adding the exact trusted destination origin permits it. An explicit `no-referrer` policy on the 302 response suppresses the redirected request's `Referer`.

These local controls use synthetic HTTP loopback origins, not the hosted HTTPS/provider journey. Hosted coverage establishes rejection in the first form guard but cannot distinguish its individual operands. The controls establish a browser-policy incompatibility; they do not prove there are no additional product failures. CSP is a separately reproduced prospective obstacle after the current first guard, not the cause of the recorded first timeout.

## Proposed bounded change

Amend feature 013's [browser contract](../../specs/013-administrator-browser-session/contracts/browser-session.md), including its sensitive-page statements, with this exception:

> Actionable B3 native form documents use `Referrer-Policy: strict-origin`. All protocol redirects, non-form results and inert errors retain `Referrer-Policy: no-referrer`. Form POSTs still require the exact configured `Origin`, native antiforgery and every existing binding and framing check. No origin supplied by the request determines trusted response policy or a redirect destination.

Apply the exception at rendering, not across entire routes or shared middleware:

| Actual form document | Referrer policy | CSP change |
| --- | --- | --- |
| BFF explicit sign-in form (`BrowserLoginBoundary`) | `strict-origin` | None required by this proposal. |
| BFF local/issuer choice logout form (`BrowserLogoutPage.Form`) | `strict-origin` | Preserve current restrictions. |
| Issuer B3 password form (`Browser/LoginModel`) | `strict-origin` | Preserve current restrictions. |
| Issuer B3 MFA form (`Browser/MfaModel`) | `strict-origin` | Retain `'self'` and add only the configured, validated BFF origin to `form-action`. |
| Issuer end-session confirmation (`IssuerLogoutPage.ConfirmAsync`) | `strict-origin` | Retain `'self'` and add only the configured, validated BFF origin to `form-action`. |

An actionable validation-error rerender remains a form document. An inert denial is not a form document. Defaults in shared account and BFF boundary middleware remain `no-referrer`; fail-safe cleanup restores that default. Do not change unrelated administrator/pilot/account surfaces.

Derive the CSP destination from the validated fixed B3 registration/profile. Do not trust request Host, forwarded headers, query/form values, arbitrary post-logout URI values or a wildcard. An origin source permits that origin in CSP; it is not a path-level authorization boundary. Exact server-side client, callback, resume and logout-return URI validation remains authoritative. Other CSP directives remain intact.

## Security tradeoff and preserved requirements

The change deliberately permits an origin-only `Referer` from these form documents to HTTPS destinations. This exposes the form page's scheme/host/port, while suppressing path, query and fragment, even on same-origin requests. `strict-origin` suppresses the referrer on an HTTPS-to-HTTP downgrade. Existing resource and navigation restrictions still apply; the amendment does not grant arbitrary navigation or resource loading.

Keep rejecting missing, null, multiple and foreign origins. Do not introduce a Referer fallback, synthesize browser headers, bypass TLS or replace the native form ceremony with scripted authentication. Preserve antiforgery, password/MFA, intent, generation, cookie, authority, transaction and audit checks. Do not forward credential-bearing form bodies cross-origin through 307/308 redirects. Continue explicit `no-referrer` on protocol 3xx responses and sensitive returns.

## Required verification and delivery after approval

1. Add permanent meaningful response-policy controls for actual form documents, actionable validation rerenders, redirects, non-form results and inert failures. Preserve negative null/missing/foreign/multiple-origin and CSRF tests.
2. In real native browser journeys, observe request headers in memory and assert exact configured `Origin`, origin-only form `Referer`, no selector/credential disclosure and absent `Referer` on intended protocol redirect hops. Retain only finite classifications or booleans; do not log raw headers, URLs, HTML, cookies, factors or tokens.
3. Verify fixed-origin CSP rendering and attacker-controlled destination rejection. Demonstrate the actual permitted issuer MFA and end-session redirects without browser header overrides, certificate bypasses or hidden navigation repairs.
4. Execute PostgreSQL/SQL Server × Chromium/WebKit with normal TLS and native BFF backchannel HTTP. Require the three PR97 journeys to pass and inspect real stores, cookies, exact source revocation and audits. Preserve the initial failed run. Investigate any subsequent failure on its evidence; do not classify it as deferred tooling by default.
5. Complete exact-final-head required CI, raw inventory/coverage/mutation reconciliation and architecture/security/test review before normal squash. Do not waive a missing coverage input or floor. The proposal itself is not executable acceptance.

The exception is restricted to feature 013's listed form documents. It adds no production activation, external release, browser-support or certification authority. A-009's named Firefox/Playwright deferrals remain separate. All six historical findings, 48 human rows, 345 unassessed controls and production/public-release gates remain open. T020 and complete B3 acceptance remain incomplete until their own evidence is satisfied.

## Approval record

Pending. Suggested explicit decision: **“I approve A-010's scoped B3 form referrer/CSP policy changes and their required verification.”** No approval is inferred from publication, passing documentation checks or the existing general roadmap instruction.
