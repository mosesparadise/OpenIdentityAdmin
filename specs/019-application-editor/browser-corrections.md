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


## Synchronous lifecycle witness and bounded progress

[The seventh rejected candidate](hosted-verification.md#seventh-candidate--protocol-observation-remained-insufficient)
shows that raw CDP still misses required Chromium pagehide observations; it does not
prove why. WebKit's two lifecycle commands reach the original660s deadline without
a usable case result. No timeout is extended or case skipped.

Only `ApplicationOutputLifecycle` and `ApplicationPendingLifecycle` opt into the new
counter. Their same-origin scenario seeds one unpredictable key once. Trusted
main-document listeners synchronously reread/update a seven-integer record, bounded
by128 characters and256 events. Missing/malformed/negative/fractional/overflow or
contradictory evidence fails. Restored documents cannot overwrite newer counts from
a stale closure. A real untrusted visibility event increments only the separate
untrusted counter. Native history/minimization and secret-erasure assertions remain
independent. Baselines refresh from storage before each actual transition.

Before the ordinary storage assertion, the record is validated and scanned privately
against actual secrets. One synchronous operation requires the exact unchanged
record, detaches current listeners and removes only that exact key. It never clears
storage or exempts a prefix. Cached listeners cannot recreate a missing record.
The unchanged oracle then requires empty sessionStorage; this evidence explicitly
follows removal of validated test instrumentation. Other sign-in/editor/privacy
journeys neither seed a key nor install these listeners, including cross-origin flows.

The optional checkpoint contains four integer enums and no arbitrary text, URLs,
credentials or exception data. Atomic writes retain only the last successful phase;
expected I/O failures disable recording. Cleanup markers do not own resources or
change their disposal order. The private wrapper reader rejects malformed, oversized,
out-of-range, Boolean and private-content substitutes, and diagnostic output failure
cannot prevent window-manager cleanup. All11 prior driver/desktop controls remain;
one additional finite-reader control makes12. Existing child-failure controls also
inject broken stdout. Existing two native diagnostic identities add a real
file/directory collision proving release markers are non-disruptive and disposal
markers preserve the primary exception. No ordinary identity is added or removed.

Independent test/security source review passes after isolating the shared observer
and making progress output non-disruptive. The initial added regression used an
ambiguous throw-only lambda, rejected by xUnit's obsolete async overload guard; an
explicit synchronous Action corrects it. Initial enum formatting was also rejected
and corrected. These failed local checks remain failures, without new allowances.
The final local gate passes: complete solution build with zero warnings/errors,
format/import checks, code-quality and its seeded enforcement controls, all12
driver/desktop/progress controls, and both exact native diagnostic identities with
zero failures/skips. The added identifier-format literal finding was named and
corrected without an allowance. The native TRX SHA256 is
`67816414f0e255a6bb816abbb2df0fbf0e3771e7ec78122f67778a73e3b37550`;
its exact identities and result/definition/entry links are reconciled. Source
gitleaks, instruction and diff checks pass. These local controls do not execute
the retained browsers; corrected exact-head browser acceptance remains pending.


## Context terminal events and safe ordinary-demo locations

[The eighth attempt](hosted-verification.md#eighth-candidate--native-history-observed-pending-completion-still-open)
now observes native Chromium output/history/visibility on both providers. Pending
navigation still lacks the page-associated terminal event after upstream completion.
The test retains the original request captured from its owned page, but subscribes
to context-level native response/finished/failed events with exact object identity.
A separate page-associated terminal flag distinguishes the two observations on
failure. Context completion and upstream completion must still join under the same
three-second cleanup budget. Navigation, pagehide or upstream completion cannot
substitute for a missing native terminal event. This is a source-supported observation
change, not a proven diagnosis of the hosted failure.

Pinned Playwright1.63.0 evidence is the [.NET finish dispatch](https://github.com/microsoft/playwright-dotnet/blob/v1.63.0/src/Playwright/Core/BrowserContext.cs#L259),
[failure dispatch](https://github.com/microsoft/playwright-dotnet/blob/v1.63.0/src/Playwright/Core/BrowserContext.cs#L268)
and [server optional page association](https://github.com/microsoft/playwright/blob/v1.63.0/packages/playwright-core/src/server/dispatchers/browserContextDispatcher.ts#L178).
Independent test and security source reviews pass this narrowly scoped correction;
they do not claim the optional association caused the observed timeout.

The ordinary demo's outer failure boundary adds only a closed error category and
allowlisted source filenames with positive bounded line numbers. It traverses at
most eight exceptions, inspects at most128 frames per exception and retains at most
eight locations. Messages, URLs, selectors, values, paths, Data and unsafe inner
exceptions are never attached. The existing navigation-failure identities require
both a useful native navigation source location and secret-canary suppression,
including an aggregate inner exception. Journey behavior and timing are unchanged.

Local scratch reflection controls exercise actual compiled source frames using
explicit injected null-page failures, nested canaries, a hostile Message/ToString
implementation and the permanent diagnostic-control method. They pass; they are not
native browser execution and add nothing to ordinary test totals. The complete solution build, formatting/imports, quality and seeded analysis controls
pass with zero build warnings/errors. Source gitleaks, instruction and diff checks
pass. A newly added property formatting failure was corrected without an allowance.
Corrected exact-head browser/demo acceptance remains pending.

The two WebKit last-checkpoint records additionally justify five appended milestones
for secret absence, observation, storage and presentation. Existing stage ordinals
remain unchanged; all12 Python controls pass, including the new highest valid stage
and rejection of19. The added markers exceeded the method-size rule; extracting the
unchanged privacy tail into an awaited helper resolves it without a new allowance.
Independent review confirms check and disposal ordering. The final complete solution
build, style/imports, quality and seeded rejection gates pass with zero warnings/errors;
final source gitleaks and diff checks pass. No browser pass is inferred from these
local controls.


## Native terminal correlation and non-window boundary topology

[Run504](hosted-verification.md#ninth-candidate--native-completion-and-presentation-remain-open)
rejects both Chromium context-terminal joins and both WebKit lifecycle commands.
Chromium now observes the same native Network terminal signal before Playwright's
request lookup, while independently retaining the original Playwright request.
Pinned1.63.0 source can discard a terminal event when its request lookup is absent:
[finish](https://github.com/microsoft/playwright/blob/v1.63.0/packages/playwright-core/src/server/chromium/crNetworkManager.ts#L469)
and [failure](https://github.com/microsoft/playwright/blob/v1.63.0/packages/playwright-core/src/server/chromium/crNetworkManager.ts#L491).
This supports the observation point; it does not prove the hosted failure's cause.

One owned observation-only CDP session attaches before submission. It correlates the
exact HTTPS URL, POST, original main frame and loader to one bounded native request ID.
Only that ID's native finish/failure completes the witness. Wrong IDs, missing events,
duplicate requests/terminals, redirects, wrong provenance, malformed IDs and unexpected
session closure cannot pass. Mutable evidence is serialized; subscriptions are frozen
before final sealing, which rejects late invalidation. Only correlation fields are
read; headers, bodies, raw payloads and native error text are not retained. No
interception, cache or network policy changes. The same three-second browser/upstream
join remains, with no sequential fallback. Separate bounded session detach always
runs and preserves both primary and cleanup failures. Pre-trigger assertions reject
any already-observed browser terminal event. Native navigation and visibility are
still independently required; neither can substitute for network completion.

Pending Navigation/Visibility remain headed and require the genuine native window.
Session/OriginalDeadline use a headless context because these boundaries exercise
logout and expiry rather than native window transitions. They still use the same
instrumented pinned driver, not an ordinary unmodified driver. Real logout, original
expiry, single-send, response/header, no-late-output, privacy/storage, theme/width and
accessibility checks remain. Earlier failed headed observations are retained; this
new topology requires its own exact-head execution and does not establish their cause.
No test identity, timeout, retry, production behavior, security assertion or coverage
floor changes.

Independent test and security reviews pass the correlation, final sealing and topology
scope; the final pre-trigger guard strengthens their reviewed source. Complete solution
build, formatting/imports, quality and seeded analysis controls pass with zero build
warnings/errors. All12 driver controls pass. Synthetic ledger calibration covers native
finish/abort and invalid/missing/late evidence, including completion followed by late
invalidation; it passes through the actual compiled control method and is also required
before each Chromium observation. This local calibration is not native browser execution.
The exact two native sanitizer controls pass2/2 with zero skips; TRX SHA256
`02fa392a2ed85905bb7913227dba7ba689def6eea7f5c7d3663beb9403abccd7`.
All2,385 administrator identities,71 browser cases/provider/engine and the full hosted
obligations remain unchanged. Source gitleaks, instruction and diff checks pass. Corrected hosted acceptance is still required.

Final control review removed duplicate terminal signals from the late duplicate-start
and redirect-start calibrations: each now fails solely on its intended bad initiation
after a valid completion, rather than also failing on an unrelated second terminal.
The production ledger and hosted scenario behavior are unchanged.

## Complete native factor candidates and safe mutation failure receipts

[Run506](hosted-verification.md#tenth-candidate--mutation-and-demo-failures-remain-rejected)
localizes the PostgreSQL demo failure to code selection before MFA submission.
The independent test oracle considered only the four adjacent counters, excluding a
possibly unused current value. It now appends the current counter after those four,
preserving their priority and excluding every already accepted numeric value. All
five are already inside the native acceptance window; full exhaustion still fails.
No wait, authentication retry, production factor change or deadline extension is added.
An explicit observation-time overload supports deterministic controls only; ordinary
callers still sample the real clock once. The existing two RFC-vector cases additionally
prove current-only fallback using independently fixed values, unchanged priority and
full-exhaustion rejection. Five ceremonies alone do not prove the hosted failure's
cause: enrollment consumes the current code, and clock movement/collisions change
which values are available. Corrected runtime evidence remains required.

Both hosted managed-mutation lanes also fail without usable retained failure categories.
The existing bounded report projection now goes to an independently guarded console
sink as well as its local file, prefixed `mutation-report-diagnostic `. It contains
only fixed categories/checkpoints, bounded counters, public source/run identities and
marker/stack match booleans. No raw XML, exception, command, filter, marker text or
credential payload is printed. Either sink may fail without replacing the primary
error or interfering with source restoration. Strict native TRX acceptance and all
mutation targets remain unchanged; this diagnostic prefix cannot count as a pass.
All60 Python controls pass, including private-canary suppression, exact finite JSON
agreement, broken-pipe preservation and continued console output after file failure.
Independent test/security reviews pass both corrections and preserve the stated
causality limits. All native case identities and hosted acceptance counts are unchanged.


## Navigation ownership and asynchronous native window state

The tenth run still rejects Chromium pending Navigation after observing the exact
raw native request start. Another terminal-event observation layer is not justified.
Product lifecycle handling synchronously invalidates the editor generation and
aborts its signal; late results independently check that generation. The requirement
is that an old operation cannot republish into the returned UI, rather than an
assumption that Chromium must deliver a network-terminal event within three seconds.

The navigation-only Chromium witness now uses the existing owned session to capture
the original main/default context's frame, numeric ID and unique ID before submission.
It verifies the running browser's exact `--disable-back-forward-cache` switch, enables
Runtime once, requires the original context still live immediately before navigation,
and observes either exact individual destruction or explicit clearing of the captured
context epoch. Individual and epoch observations remain distinct finite categories.
Actual native history must complete, a different live default-context unique ID must
be observed, and the returned main frame must retain its identity with a different,
nonempty loader ID. All of this occurs before response release within the original
five-second interaction budget. Missing/pre-arm events, session loss, original-ID
reappearance, and destruction of the replacement owner prevent acceptance. No new
loader or pagehide signal alone can satisfy the witness.

Pinned Chromium153 uses V8 commit `0b60d2b01800d7ba2c6eeb5e51ecd95f6dab44c7`.
The [protocol defines both native events](https://github.com/v8/v8/blob/0b60d2b01800d7ba2c6eeb5e51ecd95f6dab44c7/include/js_protocol.pdl#L1804);
[context-group reset](https://github.com/v8/v8/blob/0b60d2b01800d7ba2c6eeb5e51ecd95f6dab44c7/src/inspector/v8-inspector-impl.cc#L352)
and [runtime reset](https://github.com/v8/v8/blob/0b60d2b01800d7ba2c6eeb5e51ecd95f6dab44c7/src/inspector/v8-runtime-agent-impl.cc#L1179)
produce clearing. [Runtime restoration can also emit it](https://github.com/v8/v8/blob/0b60d2b01800d7ba2c6eeb5e51ecd95f6dab44c7/src/inspector/v8-runtime-agent-impl.cc#L1107),
which is why same-session continuity, original-ID rejection, real history and the
new-loader observation are mandatory. The claim is an observed context-epoch change
under this verified configuration, not generic physical realm destruction, preserved
BFCache acceptance, browser receipt, network completion or cancellation. WebKit and
same-document boundaries retain their actual native terminal-event requirement.

The existing outer BFF endpoint additionally arms one exact query-free POST attempt.
A private attempt record separately observes pipeline-finally and the server's real
Response.OnCompleted callback, without retaining HttpContext, headers or bodies.
Callbacks only signal; neither is manually invoked in a browser journey. Both signals
and upstream middleware completion join under the original three-second cleanup cap.
Final sealing rejects duplicate admission/callbacks or incomplete evidence, including
late observations during native-session cleanup. Original/cleanup failures remain
aggregated. One browser request, one native initiation, one BFF admission and one API
send are independently required. The probe separately records completed response
writes and observed request cancellation; a canceled-only path is not evidence that a
successful late response was delivered. Existing response/header, returned-DOM,
no-late-output, native history, privacy/storage, theme and accessibility assertions remain.

Native minimization/restoration now issues each original window action once and polls
actual WM_STATE until it matches under one shared monotonic three-second budget.
Action and observation cannot restart that budget; each process still has bounded
cleanup. This observes asynchronous state publication without synthesizing visibility,
repeating native actions or extending timing. The prior failed preflight remains
rejected until the corrected source completes real native transitions.

Permanent synthetic calibrations cover both context-event forms, mismatched/missing
identity, pre-arm clearing, restored/lost replacement owners, late sealing, and exact
BFF admission with separate required completion signals. Initiation-only network sealing
is permitted only alongside the navigation context witness; default sealing still
requires an actual terminal. Missing, duplicate and invalid initiation fail either
mode. Synthetic callbacks are not native execution or additional ordinary test cases.

Final architecture/security/test reviews pass after adding returned-loader corroboration,
invalidating destruction of the replacement context, and ensuring launch-policy failure
assertions cannot print browser arguments. Complete solution build, style/imports,
quality and seeded rejection gates pass; the final privacy-only assertion change is
separately rebuilt with zero warnings/errors. All three compiled synthetic calibration
groups pass. The final exact two sanitizer and two factor-oracle identities pass4/4
with zero skips; strict native TRX SHA256 `61abb1cecd0470c4a44d887275ba185e1f1fb581af504bdb9f73fb0311775901`.
Source gitleaks, instruction and diff checks pass. An initial method-size finding was
resolved by an awaited final-view helper, and formatting/named-expectation findings
were corrected without any new allowance. These local checks do not execute retained
browsers or accept the rejected hosted candidate.

## Chromium launch-observation prerequisite

Run507 rejects both PostgreSQL and SQL Server Chromium pending Navigation before dispatch, at
`ApplicationNativeNetwork.ObserveDocumentAsync`'s `Browser.getBrowserCommandLine`
call. The fixed diagnostic is unclassified; no raw exception or command-line contents
are exposed. The original-context observer has not yet been enabled at that point,
so this result does not exercise the new context-epoch or BFF-completion witness.

Pinned Chromium153.0.8010.12's [BrowserHandler::GetBrowserCommandLine](https://github.com/chromium/chromium/blob/153.0.8010.12/content/browser/devtools/protocol/browser_handler.cc#L670) requires
`--enable-automation`; the Playwright1.63.0 default switch list includes
`--disable-back-forward-cache` but omits that prerequisite. The reviewed correction
opts into `--enable-automation` only for pending Navigation on Chromium through an
explicit `BrowserSession.CreateHeadedAsync` parameter. All other calls default to no
additional arguments; non-Chromium opt-in is rejected. The same page CDP session,
actual command-line policy assertion, Runtime/Network continuity, native window/DOM
observations and original deadlines remain required. Changing CDP target alone would
not satisfy the missing launch prerequisite and is not part of this correction.

Automation changes Chromium's configuration and can affect browser chrome. Results
therefore apply to this explicitly configured lifecycle fixture, not an ordinary
unmodified browser launch. No product code, TLS policy, request count, native lifecycle
assertion, timeout or security boundary is relaxed. Source inspection establishes the
command's missing prerequisite; it does not recover the suppressed hosted error text.

Scoped architecture/security/test reviews pass. The full solution build passes with
zero warnings/errors; whitespace/imports, quality, instruction and diff checks pass.
All three compiled synthetic calibration groups pass; they remain calibration only,
not native browser execution. Local pinned browser executables remain unavailable.
No fallback engine, blocked download retry, mirror test or inferred runtime pass is
used. The new exact-head hosted execution must verify this configured browser.

| Local correction receipt | SHA256 |
| --- | --- |
| Solution build | `2991778bdac3bacb0cb54d5cd3af4eefb5f1f1342ec7fadde3c82e473fb042a2` |
| Quality gate | `7fbcef84db42cc7f144e1483b1d2fd346f6675f31540cc19c1a5dad94498bfc6` |
| Compiled synthetic calibration | `39b197284812a9d22e9707d145bee80f91c2a9aaf46eb8e236b15cd7b23af9c4` |

## Immediate creation-response observation

Run508 rejects both ordinary native Aspire demos at the same creation-body read:
`ApplicationEditorJourney.cs:124`, after status201, the Created heading and expected
secret length already passed. Each demo reports2/3 passes, one failure and zero skips.
The safe category is Browser. No raw exception, response body or command output is
available, and no specific browser cache, detachment or navigation cause is inferred.

Pinned Playwright1.63.0 reads response content lazily: its `Response.internalBody`
creates the cached body promise only when called, waits for completion, then invokes
Chromium's `Network.getResponseBody` callback. That callback cannot refetch this POST.
The generic navigation warning is appended to protocol errors and is not proof that
navigation occurred. The inspected creation path prevents native form navigation and
updates React state; there is no explicit test navigation between receipt and the old
body read. Successful UI output establishes application-side consumption, not later
availability of the inspector's copy.

The candidate captures and parses the same exact201 response immediately, before
heading/focus/input waits. All original status, DOM, focus, secret-length,
body-secret/display equality and application-ID bindings remain. There is one body
read, no retry/refetch/interception, no background task and no timeout change. Parsed
secret-bearing content remains in a scoped private JsonDocument. This reduces the
body-observation delay but is not a proven explanation or acceptance of the failed run.

A new shared helper is explicitly source-linked into the native-demo project. On a
Playwright body-read failure it best-effort emits only a closed category and six
booleans: page closed, browser connected, exact expected-location match, service-worker
response, redirect presence and request-failure presence. It never emits response
content, URLs, headers, request failure text or exception text. Category matching
inspects at most4096 message characters; unknown and generic navigation-hint messages
remain Unknown. Classification is diagnostic, not proof of causation. Diagnostic
observation or sink failure cannot replace the original failure.

The two existing provider-independent BrowserEvidenceTests identities retain their
original checks and add exact category/boolean expectations, secret-canary and
exception-data suppression, overlong/unknown/navigation-hint cases, successful output
and throwing observer/sink controls. No ordinary test identity, required count or
browser/provider obligation is removed or renamed. Architecture/security source
reviews pass after adding the explicit AppHost source link; runtime acceptance remains
required. No raw private browser content is exposed to improve diagnostics.

The final complete solution build passes with zero warnings/errors, and whitespace,
imports, quality, instruction, diff and source-secret checks pass. Focused native
sanitizer execution passes2/2 with zero skips; repository strict counter/name and
result/definition/entry validators pass. Initial compile/formatting and unnamed-test
literal findings were corrected without allowances. These checks do not establish
native browser body-read acceptance.

| Local correction receipt | SHA256 |
| --- | --- |
| Final solution build | `459b4f67a0098806292420d461033481aca8d55611b5fe26355e5a278d62831c` |
| Final quality gate | `7fbcef84db42cc7f144e1483b1d2fd346f6675f31540cc19c1a5dad94498bfc6` |
| Exact focused sanitizer TRX | `55abae3b67697023fe337cfbf33898e713004537bd1095433a4023352dbcbd35` |
