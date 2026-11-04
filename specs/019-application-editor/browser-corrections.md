# Browser acceptance corrections — 4 November 2026

This is a correction candidate within approved feature019/T019–T022. PR120 remains
draft/unmerged. The fourth hosted attempt is retained in [hosted verification](hosted-verification.md);
none of its failing cases or skipped aggregate coverage counts as acceptance.

## Product form behavior

The managed consent form previously inherited same-origin-only `form-action`, although
successful consent resumes authorization and redirects to the registered client.
`ManagedFormPolicy` now permits one exact callback origin only on current
`PendingConsent`, after the real native/account operation revalidates the protected
request, current registration and exact callback under locks and completes its
transaction. Raw HTTP metadata never selects that origin. The original nonce and
all other directives remain exact. Password/MFA stay same-origin; inert output and
redirects use `no-referrer`. Invalid callback forms, missing nonce and foreign-client
results fail closed. This implements feature019's approved browser-consent dependency;
it does not expand or revive the earlier feature013-only A-010 amendment.

Sixteen additive Host cases cover every phase, unsafe callbacks, hostile HTTP
metadata, original nonce, client binding and real outer-middleware failure cleanup.
All **1,846 Host cases pass**, with no skips. Native helpers now require the exact
consent origin, terminal no-referrer and redirect no-referrer. Twenty-two focused
cases pass on each real provider: managed HTTP, atomicity, authority drift, races,
password upgrade, token/claim and created/updated registration journeys. Complete
TRX counts and actual execution identities were reconciled. Browser-only fixture
changes made afterward are not covered by those earlier native DLL executions.

The actual WebKit axe failure occurs on consent, where Cancel lacked a styling
class. Its new secondary style uses the existing surface/text/border tokens with
explicit native appearance reset. The original decision field, native submit and
keyboard order are retained. Both consent buttons have fixed diagnostic IDs; the
sanitizer still refuses arbitrary selectors or page content. Existing light/dark,
width and axe checks remain required; no contrast exception is introduced.

Local TRX SHA-256 receipts (not hosted artifact digests):

- Host: `53a243b027ca620e30c789bc6807c3ca6a4986a1e6485ac3565ebe218ee77bd4`.
- PostgreSQL22: `72091503931a37dcab2e20939f2df6b8a5d4ac1664a539fafaa10376d51283aa`.
- SQL Server22: `810b1b0dc2616b9c95a3324e44f5088ca1fad948a84548719861ca9221065844`.

## Request-test corrections

The cross-purpose JSON test now leaves the native logout document after collecting
its form-purpose token and submits from the real Applications document. Logout CSP
correctly blocks arbitrary fetch; it is unchanged. Token-purpose, origin, generation,
no-dispatch and no-retry assertions remain exact.

Playwright `Route.FetchAsync` uses Node's HTTPS transport, independently of browser
NSS/OS trust. The browser job now supplies `NODE_EXTRA_CA_CERTS` with the already
exported exact synthetic development certificate before driver startup. Normal
hostname/chain checks remain enabled, with zero retries and the existing timeout.
The lost-output seam retains finite stage/status/certificate-category diagnostics;
it never emits raw exception, body, URL or header values. The earlier `Failed`
boolean alone cannot establish that TLS caused its hosted failure. Native demos
also distinguish fixed list, test-client startup, editor, subject-binding and
managed-ceremony phases without private diagnostics.

## Actual native visibility

`BringToFrontAsync` activates a window but does not guarantee another is hidden.
Additionally, the pinned WebKit driver forces active/visible flags. Lifecycle jobs
therefore own a private Xvfb display and Openbox window manager. They identify exactly
one actual browser window using a temporary non-secret title, restore the original
title, minimize and restore that same window through X11, and require its real
`WM_STATE` as well as native document visibility and trusted event counts. Existing
secret-erasure, one-send, original deadline and database assertions remain unchanged.
The exact three-second observation and five-second pending-mutation bounds remain.
Visibility predicates use50ms timer polling within that same bound because native
minimization can suspend animation frames; the predicate still reads actual DOM
state, and trusted events remain independently required.

Chromium uses its public CDP API to disable the automation focus override. WebKit
uses an **instrumented Playwright1.63 lifecycle driver**, not a claim of unchanged
driver behavior: copy the installed driver into a disposable directory and replace
one reviewed startup `setActiveAndFocused({active:true})` call with `{}`. The original
package remains byte-identical. The supported .NET `PLAYWRIGHT_DRIVER_SEARCH_PATH`
selects the copy only for the WebKit lifecycle child process; inherited overrides
are removed. Other browser lanes use the unmodified driver. No engine binary,
product code, synthetic event or forced `active:false` supplies hidden state.

Pinned [native source](https://github.com/microsoft/playwright/blob/v1.63.0/browser_patches/webkit/patches/bootstrap.diff)
declares `active` optional (lines539–547), forwards `std::optional<bool>` unchanged
(lines13360–13365), stores it and recalculates activity (lines14182–14190), and only
overrides visibility when the optional has a value (lines14203–14225). Omission
therefore clears the override by the schema/C++ semantics; that inference still
requires actual hosted runtime proof. Retrieved source SHA-256:
`fea735011093a46032cc9b3da9e67ecab28821a6a8e20da071d3c962d4d5e1ff`.

The wrapper rejects version, original hash, replacement count and resulting hash
drift. Original bundle SHA-256:
`549070af3acabb3efcc4f55bfe6210f9f7c2fcf633cf7eaa59bfe60719969171`;
copy SHA-256:
`dc9fb491407327fc249fa9287fc868d5fe79d64e45db306b00194c3cd28b0458`.
Ten permanent Python controls pass, including unchanged-source copying, hostile
inputs and exact lane/coverage paths, engine environment, failure propagation and
window-manager cleanup. Browser processes are joined before temporary copy cleanup.
Native utilities bound exit/output/cleanup and never publish captured diagnostics.

Independent architecture/security/test reviewers passed the scoped design and
integrated source, qualified by pending browser execution. Their corrections bound
process cleanup, removed inherited driver overrides, added orchestration controls
and retained the first finite response-seam failure stage. UX review identified
the actual Cancel control and approved use of the existing tokens and native action.
Required hosted execution, all98 coverage inputs/15 unchanged floors and complete
acceptance remain pending. Firefox, precise A-009 deferrals, actual Windows,
historical/human/vendor/production/public-release gates and disabled refresh are
unchanged. The local blocked browser download and raw artifact-transfer restrictions
are not retried or bypassed.


Final local source checks pass: complete solution build with zero warnings/errors,
formatting/import verification and unchanged quality controls, followed by a clean
Administrator build of the later reviewed timer-observation adjustment. The final
source snapshot passes Gitleaks8.30.1. Initial missing-import, unnamed-literal and
CompositeFormat analyzer failures were corrected and remain separate local logs;
no allowance or gate was relaxed. A scratch coverage command without its required
arguments and a quality invocation without the SDK environment were rejected; the
correctly configured controls and final checks pass. Expected hosted obligations are
122 jobs,116 artifacts,101 ordinary reports/5,427 cases, plus55 UI and44 Node cases,
98 coverage inputs,15 floors and90 mutation controls across14 lanes. These are
expectations, not execution receipts.
