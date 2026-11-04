# Browser acceptance implementation

4 November 2026. Source implementation checkpoint; required retained-engine execution
and exact-head full acceptance are still pending. This completes missing scenario
source identified in [the gap review](browser-acceptance-gaps.md), not T019/T022
execution acceptance. PR #120 remains draft.

The existing positive editor and native-demo identities now retain the actual
UI create-response application ID, immutable client ID, edited callback and current
one-time replacement secret privately. A genuine loopback HTTPS relying party uses
that same registration through fresh browser password/MFA and explicit consent while
the previous issuer session is present. Code exchange remains server-side. The
relying party checks original state, issuer, HttpOnly browser correlation, PKCE and
public ID-token cryptography; the Administrator scenario additionally reuses native
access-token, durable token/authorization, audit and unchanged-authority oracles and
requires management API rejection. Borrowed evidence adapters create no extra client
or fixture. A separate browser cancellation checks absence of issuance.

Five additive two-provider theories are divided into four required stories:

| Story | Cases per provider/engine | Evidence |
| --- | --- | --- |
| ApplicationEditor | 1 | Real UI writes, form/error/success presentation, finite response failures, gated working states and actual committed replacement with lost response. |
| ApplicationAuthority | 1 | Actual persisted permission removal and original server access expiry; no mutation and applicable visible recovery. |
| ApplicationLifecycle | 2 | Actual history/tab visibility and pending navigation, visibility, durable local logout and original display deadline races. |
| ManagedConsent | 1 | Fresh password/MFA followed by explicit cancellation and independent native absence-of-issuance checks. |

The positive editor remains in BrowserProtection. All previous identities and
metadata must remain exact; new records are admitted only from native discovery.
Each new story keeps the existing 660-second process deadline. Full working-state
presentation has a separate request for each operation/theme/width, with one bounded
scan before explicit cancellation. It does not pause one request across the entire
matrix or enlarge the product's transport timeout.

Finite 400/404/409/412/428/503 browser response seams prove UI rendering/controller
recovery; they do not masquerade as backend denial evidence. Permission removal and
access expiry use actual provider state. Lost replacement output sends through the
real BFF/API, observes a genuine successful response and exact revision/audit advance,
then aborts delivery to the page. Reload cannot recover its secret or repeat the write.
Pending lifecycle races use the already named upstream response probe and remain
separate from genuine persistence tests.

Lifecycle cases use headed Chromium/WebKit under Xvfb in their dedicated CI cells.
Each engine uses the separately hash-pinned disposable driver copy documented in
[browser corrections](browser-corrections.md), disabling only its automation-owned
visibility override before requiring actual window-manager and DOM transitions.
Only the dedicated lifecycle scenarios use synchronous bounded trusted-event
counters under one random test-owned sessionStorage key. The exact validated and
secret-scanned record/listeners are removed before the unchanged empty-storage
assertion. Shared privacy observers remain free of storage instrumentation.
Protocol-delivered counters failed to establish the required Chromium pagehide facts;
the synchronous replacement still requires actual retained-engine verification. Pending navigation
and visibility require no late output and joined browser/upstream completion;
they do not equate browser abort with an immediate remote transport disconnect. There is no synthetic-event fallback or visibility skip. History checks
record native persisted-page restoration counts; zero does not claim bfcache use.
No cache/security header is weakened to make a page eligible. The legacy synthetic
pagehide handler control is retained separately: untrusted events consume the
observation bound but cannot increment any native lifecycle counter.

Actual generated secrets and tokens are retained only in disposable test memory.
Bounded observers inspect committed main-frame URLs, current restored history state,
console arguments/page errors and DOM/storage through the existing independently
seeded encoding detector. Overflow or failed observation is a failure. After scenario-owned cleanup, the actual xUnit output is scanned and attachments
are prohibited. A real Playwright exception containing an actual generated secret
is passed privately through the existing sanitizer and its full safe exception is
scanned. This does not inspect later TRX/coverage output, process stdout or uploaded
ZIP contents. No screenshots, traces, plaintext exports or private exception
diagnostics are added. This scope does
not claim enumeration of all browser-private history or arbitrary process memory.

Architecture, security, UX and selection reviews are source reviews; they do not
establish runtime success. Human Windows/accessibility acceptance and every historical,
A-009, vendor, production/public-release boundary remain open. Firefox remains deferred,
refresh disabled and the blocked local browser download is not retried.

## Independent source review

Architecture review accepted the shared public/native token oracles and borrowed
ownership. A vacuous `All(Redeemed)` check was corrected to require exactly two
matching consumed inputs first; denial additionally binds the sole valid native
request token to its interaction. The callback requires exact ordinal query names.
Security review accepted scoped native visibility and diagnostic ownership after
awaited cleanup. The diagnostic scan runs only after successful action and cleanup;
failures remain failed evidence and do not imply a successful secrecy scan.
UX review matched the shipped selectors, finite messages and genuine permission/
expiry boundaries. Authority setup now performs one actual create without repeating
the recovery case's presentation matrix; recovery and authority have independent CI
cells. Working checks require zero browser mutation attempts as well as unchanged
persisted state. Test-selection review accepted all nine closed story sets and the
balanced-drift, omission/substitution and unchanged provider identity controls.

## Local verification before hosted execution

Native discovery contains 2,385 cases: all 2,375 prior records retain identical
portable type/traits and provider-list order, with ten appended records. Actual
filtered discovery checks all nine browser stories on both providers and their
exact 71-case disjoint unions. The four new story counts are 1/1/2/1. Seventy-seven
Python selection, metadata, coverage and quality controls pass. Required coverage
inputs are now 98; all 15 module floors remain unchanged.

Ten existing native token/claim/created-client/updated-client cases pass on each real
provider, with complete TRX counts and exact execution identities reconciled. These
focused runs used the compiled shared-oracle extraction before the later browser-only
diagnostic wrapper and unused-import cleanup. They are scoped regression evidence,
not final-head browser or full-CI acceptance. Initial compilation/format/API-overload
failures were corrected and remain in local logs; none count as passes.

Retained local receipts: PostgreSQL TRX SHA-256
`ed098fe701c6dca5de0141f7a807d5a2c64d73a789aabc2fa695413adc466f1a`, SQL Server TRX
`ccfcbd3184210f555f79668b6d1d4c39be22bed4e51a92910a8d9d83f2ed70ee`, complete native
discovery `619a6feee3289676bd4ca3c2d2bb1ef9b2648b3d357f6067751a7b4c7a5687b3`, and
filtered-union log `3f904f53fbfaa94925f11e0431e5a235611068340fa477d6da7f46ce8b75c7bc`.
These are local receipts, not published hosted artifact checksums.

The complete solution build, formatting/import checks, quality controls and seeded
unsafe/misformatted/naming rejection checks pass. A later narrowly reviewed observer
correction excludes untrusted legacy handler-control events from native counters;
its affected Administrator build is checked separately. No analyzer allowance or
security timeout changed.


The fourth hosted attempt and subsequent product/fixture corrections are recorded
in [browser corrections](browser-corrections.md). Its instrumented native-visibility
lane supersedes the earlier BringToFront preflight; actual acceptance remains open.


### Pending lifecycle topology follow-up

Navigation and Visibility retain headed contexts and real native-window transitions.
Pending Session and OriginalDeadline now use headless contexts with the same
instrumented pinned driver; they retain genuine logout/original expiry and every
request, privacy, no-late-output and presentation assertion. Chromium adds an exact
raw native terminal witness before Playwright request lookup. See the
[reviewed correction and limits](browser-corrections.md#native-terminal-correlation-and-non-window-boundary-topology).
Prior failed headed observations remain rejected; this source needs fresh hosted acceptance.
