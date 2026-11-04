# Remaining browser acceptance

**Accepted internal delivery — 4 November 2026:** [Final evidence](verification.md#accepted-internal-delivery--4-november-2026) supersedes the pending or missing-scenario wording below. T007/T014/T019/T020/T021/T022 are complete internally; historical failed runs and evidence limits remain applicable.

Historical source-gap review, 4 November 2026. [The subsequent implementation](browser-acceptance-implementation.md) adds the missing scenario source; runtime acceptance remains pending. This source review distinguishes implemented test coverage from the
remaining feature 019 FR-012, SC-001/SC-005 and T019 obligations. A green current matrix
would not, by itself, complete T019 or T022. Existing Firefox/A-009 and human/Windows
boundaries are unchanged.

The existing retained-engine `ApplicationEditorJourney` performs actual React/BFF
creation, details, callback/name editing, independent-context stale update/reload and
explicit secret replacement on both providers. It checks keyboard submission, linked
validation/focus, explicit dismissal, a synthetic pagehide, current-origin DOM/storage
and selected form/cleared-state presentation. The native Aspire demos reuse this
journey. Managed issuer tests separately perform real HTTPS password/MFA/consent,
code exchange, current policy and cryptographic verification using HttpClient.

Two bounded acceptance groups remain:

1. **The same UI-created registration through a real browser ceremony.** Follow its
   actual authorization request through fresh password/MFA, explicit consent and
   callback, then verify the resulting token with existing native oracles. Include
   denial and a fresh ceremony with an existing issuer session. Current HttpClient
   issuer tests do not constitute this browser journey or connect it to the same
   registration created by the UI test.
2. **Editor recovery, lifecycle and presentation.** Exercise actual back/forward and
   bfcache restoration, visibility changes, navigation during pending writes, late
   responses, original deadlines and session changes. Exercise permission removal,
   expired authority and ambiguous-response recovery through the React editor, with
   one-send checks. Extend keyboard/axe/reflow checks to all defined states, including
   validation errors, stale conflict, working, forbidden, expired, unavailable,
   ambiguous and successful one-time output, plus managed consent screens. Scan the
   actual generated secrets against URL/history, console/page errors and retained
   diagnostic/artifact surfaces as well as DOM/storage.

Synthetic pagehide proves the registered handler, not actual browser history or
bfcache behavior. The current transport browser tests use a bounded upstream seam;
they do not establish those visible editor recovery states. The demo's shared journey
omits the optional axe/reflow callback, so its theme toggles do not claim those checks.
No secret-bearing screenshot, form output or protocol URL is needed in retained
failure diagnostics.

Full hosted checks remain valuable regression evidence. Record exactly which cases
ran and their source version, while leaving these absent scenarios and manual
acceptance open. Reuse the existing issuer and browser fixtures when implementing the
two groups; no second issuer implementation or weaker acceptance gate is proposed.

## Bounded implementation sequence

Independent requirement/UX review recommends extending the existing positive editor
journey and adding four focused two-provider browser theories. Preserve the existing
positive/demo identities; discover and reconcile new records before changing counts.

- Extend the positive journey with a private in-memory handoff of the UI-created,
  edited registration and current one-time credential. Reuse native authorization
  intent, actual password/TOTP sources, a genuine test HTTPS callback receiver and
  existing cryptographic oracles through a narrow non-owning evidence adapter. Keep
  code exchange server-side. Run with the existing issuer session still present to
  require a fresh ceremony. Share the ceremony with both ordinary native demos.
- Add explicit browser consent denial with accessible terminal cancellation and
  independent absence-of-issuance observations.
- Add visible editor recovery checks for real permission removal/expiry and bounded
  upstream failure seams. Cover each defined presentation state, correct focus,
  operation-specific recovery and no automatic duplicate send.
- Add actual history navigation and visibility changes around one-time output.
  Record whether bfcache was used; do not weaken no-store or security headers to make
  a page eligible. Check actual secrets against URL/history, storage and bounded
  console/page-error observations without emitting inspected values.
- Add pending-write navigation/visibility, session-change and original-deadline
  checks using joined response gates. Release the delayed response and require one
  send, honest uncertainty and no secret/success resurrection after ownership loss.

This is a proposed eight-record native inventory addition, producing sixteen retained
engine executions if Chromium/WebKit selection is unchanged; these are not executed
counts. Existing native protocol negatives need not be duplicated in every browser
combination. No production relying-party UI, secret-bearing screenshots, duplicate
demo axe matrix or Firefox reinstatement is required. Human Windows and qualified
manual accessibility acceptance remain separate.

Successful exact-head execution of the current transport and create/details/demo
cases can close scoped T007 and T014 independently. Integrated same-registration
browser acceptance and the remaining state/lifecycle requirements still belong to
T019, and therefore keep final T022 acceptance open.
