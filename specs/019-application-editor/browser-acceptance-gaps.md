# Remaining browser acceptance

4 November 2026. This source review distinguishes implemented test coverage from the
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
