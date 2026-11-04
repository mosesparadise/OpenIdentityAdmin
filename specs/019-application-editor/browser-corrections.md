# Browser acceptance corrections — 4 November 2026

This is a correction candidate within approved feature019/T019–T022. PR120 remains
draft/unmerged. The fourth and fifth hosted attempts are retained in [hosted verification](hosted-verification.md);
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

Both engines use an **instrumented Playwright1.63 lifecycle driver**, not a claim
of unchanged-driver visibility behavior. Copy the exact original installed driver
to a disposable directory and change exactly one engine-specific startup call:
WebKit `setActiveAndFocused({active:true})` becomes `{}`, while Chromium
`setFocusEmulationEnabled({enabled:true})` becomes `{enabled:false}` in its original
driver session. These distinct semantics disable their respective overrides; they
do not force hidden state. The original package remains byte-identical. The supported
.NET `PLAYWRIGHT_DRIVER_SEARCH_PATH` selects the copy only for the lifecycle child
process; inherited overrides are removed. Other browser lanes use the unmodified
driver. No engine binary, product code, synthetic event or WebKit `active:false`
supplies hidden state. The earlier additional public Chromium CDP session was
ineffective and is removed, with that recommendation explicitly superseded.

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
WebKit-only copy SHA-256:
`dc9fb491407327fc249fa9287fc868d5fe79d64e45db306b00194c3cd28b0458`;
Chromium-only copy SHA-256:
`a1822c4ef78eba42059e6fd7187f5387290ec6c5b456132ba1aaec68a0b9c439`.
Eleven permanent Python controls pass, including unchanged-source copying, hostile
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


## Fifth-attempt fixture follow-up

Run41846849133 at`1efd6a7` finishes108 successful/13 failed/one skipped jobs. Both
native Aspire demos pass3/3. All12 new consent-cancellation/recovery/authority lanes
pass, covering both providers and retained engines. The remaining source corrections
below preserve those scenarios and all required obligations; no previous failure
becomes a pass and aggregate coverage remains unaccepted.

- Appending the consent-button diagnostic selectors preserves the established index
  ordering. Prepending them broke an existing secret-suppression control at index0.
  The unchanged control fails2/2 before correction and passes2/2 afterward in native
  non-browser-launch execution. Before TRX SHA-256:
  `359d4cb640bec74a63510ccea152b13f5468e8f99813c90df48bc2d399e92c1b`;
  after: `fd3fe9966208c74f1201d147aafbb3d3a499aa7e2e43a7f6f485e9780dab11e9`.
- A genuine foreign-origin `no-cors` simple POST has `text/plain`, so JSON endpoint
  media admission rejects it with415 before the feature Origin check. The browser
  test now requires exact415, privately verifies actual browser-generated Origin
  and exact media type, and retains zero upstream dispatch. It does not claim the
  feature Origin guard executed. Existing valid-JSON foreign-Origin controls in
  `ApplicationTransportTests` and `ApplicationHttpUpdateAuthorityTests` retain
  exact400/no-dispatch/unchanged persistence and their legitimate positive controls.
- Chromium's additional CDP session cannot release Playwright's original-session
  visible capture. Pinned [EmulationHandler](https://github.com/chromium/chromium/blob/153.0.8010.12/content/browser/devtools/protocol/emulation_handler.cc#L1004)
  returns early when a new handler already has disabled focus emulation; the owning
  session's true setting increments visible capture. Pinned
  [WebContents](https://github.com/chromium/chromium/blob/153.0.8010.12/content/browser/web_contents/web_contents_impl.cc#L5159)
  keeps DOM visibility visible while that capture exists, explaining the observed
  iconic-window/visible-document mismatch. The engine-specific original-session
  correction above removes that capture. No launch flags or visibility assertions
  are changed. Whole-file source hashes respectively:
  `1447e3b18e2b87ea58a6c60fda002200693b627bee1aa1107f2951cf31c51e4b` and
  `0ab4088c0d47084561833d6a87eb436b5df28f0d31866bfcb09f5abf2ad30b92`.
- Pinned WebKit [MiniBrowser title handling](https://github.com/microsoft/playwright/blob/v1.63.0/browser_patches/webkit/patches/bootstrap.diff#L19472)
  prefixes document titles with `🎭 Playwright: `. Removing only the start anchor
  lets the unpredictable page-owned marker match that title. Exactly one visible
  numeric window, original-title restoration and every native-state check remain.

Independent test/security integration reviews pass the eight-file fixture delta,
qualified by pending actual corrected browser execution. Full solution build,
format/import and quality checks pass without warnings, errors or new allowances.
The11 native-driver controls pass; no database experiment, browser download,
production correction or merge is added by this follow-up.


## Native lifecycle event and completion follow-up

The [sixth hosted attempt](hosted-verification.md#sixth-candidate--native-lifecycle-observation)
passes116 jobs, including both demos, all standard/protection cells and all12 new
consent/recovery/authority cells. Four lifecycle cells and final acceptance fail;
aggregate coverage is skipped. WebKit output/history/visibility passes on both
providers. Chromium misses a required event-counter join; the exact missing event
was not retained. Pinned Playwright coreBundle.js `_onBindingCalled` resolves its
execution context after an await and returns if that document disappeared. The
correction receives the raw Runtime binding event directly on an owned CDP session,
without that lookup. It adds no storage writes and manufactures no browser events.

The shared payload parser accepts only a three-field tuple, four finite event kinds,
Boolean trusted/persisted flags and at most64 characters. Independent malformed,
wrong-type, extra-field, unknown-kind and overflow controls run before capture.
Main-document listeners exclude iframe events. Untrusted events cannot increment
native acceptance counters; missing transitions still fail after the original5s
observation join with finite previous/current counters. Actual bfcache restoration
remains reported only when observed.

Pending lifecycle no longer assumes remote RequestAborted within3s of navigation
or browser Fetch cancellation. It retains observed dispatch, the original5s semantic
boundary, actual history/visibility, native browser finish/failure, one send and no
late output. The released paused middleware is independently joined, including its
cancellation/error finally, within one existing3s cleanup cap. Cleanup can complete
after test cancellation within that same cap. Genuine transport cancellation tests
remain unchanged. This is UI race/completion evidence, not immediate server-side
cancellation evidence.

Primary and cleanup failures are preserved together; disposing the network observer
only detaches handlers and cannot introduce a fresh timeout that hides the primary.
Sanitization traverses aggregate source stacks while discarding arbitrary messages,
exception state and inner exceptions. Existing two-provider diagnostic identities
add seeded aggregate secrecy controls; no ordinary case is added or removed.

Initial local builds rejected formatting, required braces and nullable diagnostic
arguments; the local quality gate also rejected unnamed literals. These are retained
failed checks, corrected without allowances. Final local verification and the new
exact-head hosted run remain pending at this documentation checkpoint.

Final local checks pass: full solution build0warnings/errors; formatting/imports;
quality rules and seeded static rejection controls; six exact native executions
(two diagnostic cases plus two transport cases per real provider), zero failures/skips.
The unchanged strict TRX validator reconciles exact expected names, complete counters,
result/definition/entry execution bindings and no run errors. These provider checks
retain real TLS forwarding, genuine cancellation propagation and foreign/stale JSON
proof denial. They are not retained-engine lifecycle execution. Final independent
source/test/security reviews pass; hosted acceptance remains required.

| Local receipt | SHA-256 |
| --- | --- |
| `t019-native-events-diagnostics/diagnostics.trx` | `fbd6c081bca474bb3fbebe6b09d561ccd6bd3264b9f584a6663f787e2276956f` |
| `t019-native-events-PostgreSql/transport.trx` | `2a8fac7c24fce603ee20a2af8ac36a47319d3c21bbf546d5fd37cf42641954b3` |
| `t019-native-events-SqlServer/transport.trx` | `91587cb20401b3d48ec721da7858d106eac7a1f719679f9c2584b500db38de56` |
