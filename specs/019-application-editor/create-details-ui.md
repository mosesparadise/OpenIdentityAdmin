# Create/details UI checkpoint — 1 November 2026

Feature019 T014 now has a visible create form, sanitized details and one-time secret display on the existing native Aspire Applications route. This is the implementation branch `123-application-editor`, not merged main. Editing and explicit credential replacement remain T015–T017. T014 stays unchecked because its retained-browser/native Aspire acceptance is not complete; this checkpoint does not substitute Node or HTTPS tests for browser execution.

## What is usable

Run the existing `IdentityPlatform.AppHost` **local-demo** profile from the IDE or the [normal .NET command](quickstart.md), initialize/enroll an administrator and sign in. Applications now offers **Create application** and **View details** on addressable registrations. No custom PowerShell, separate frontend server, new configuration or package change is required. The committed deterministic assets are embedded by the BFF build.

Creation takes an exact unique client ID, display name, one to ten HTTPS callbacks and an explicitly chosen future whole-second UTC secret expiry. It uses the already implemented generated BFF contract, current native antiforgery and current server permissions. The supported client remains confidential authorization-code/S256, openid only, with fresh password/MFA and explicit consent. Earlier T012/T013 evidence establishes actual issuer use; this UI change does not reimplement that flow.

A successful creation displays the generated secret once in a labelled selectable field. Copy it deliberately into the integrating application's local secret store. No automatic clipboard call, storage, URL/history entry, logging or OAuth token exposure is introduced. Dismissal, navigation, hiding, original output/credential/session expiry and loss of authority drop the UI references and remove the view. This is not a secure memory-erasure claim. Details retain only separately validated metadata; expired credential metadata can still be inspected. System/unmanaged registrations remain read-only, and the reserved literal antiforgery identity cannot become a details action.

## Failure and authority behavior

The owner sets its busy fence before awaiting a CSRF proof and sends at most one POST. It checks the original session, operation generation and deadlines after every awaited response/cryptographic check and rechecks credential expiry before dispatch. Hidden/unmounted/superseded operations cannot restore output, even if transport ignores cancellation. No response or read grants write permission.

Every unconfirmed post-dispatch result is ambiguous, including 401 after the API may have committed. The UI tells the operator to inspect the list/details before another create; it never claims rollback or retries automatically. Lost secrets cannot be retrieved. Replacement is explicitly described as not yet available rather than offered as a working action. Output expiry is distinguished from actual observed session expiry. Linked errors, safe text rendering, heading focus and a focusable list fallback are implemented; actual browser accessibility remains pending.

The response validator checks exact JSON shape, native secret shape, current observation bounds, resource-bound strong ETag/revision, managed profile and original requested values. Callback membership is exact but order independent because the server sorts callbacks ordinally. A reverse-order regression prevents valid committed output from being rejected and losing its one-time secret.

## Verification and review

[Machine-readable evidence](create-details-ui-evidence.json) binds final source and receipts and separates earlier failures. Existing eleven session and nine application-list Node identities/runners remain byte-identical to the previous checkpoint. Twenty-six new exact editor identities cover input/response validation, generated fixed transport, one-send behavior, all post-dispatch uncertainty, preflight/crypto/POST races, independent deadline minima, lifecycle erasure, safe markup/error linkage and focus target selection. Each of six critical modules enforces 95% lines, 90% branches and 100% functions; no previous floor is reduced. TypeScript, deterministic asset verification and dependency audit pass.

The new provider test enables the real workspace on genuine HTTPS BFF/API/issuer hosts, performs native password/MFA authentication, compares served document/script/style SHA-256 values with independent generated source files, then creates through real CSRF and reads sanitized details with the same version. Its database and audit counts are real. It does not execute JavaScript or a browser engine and does not establish a fresh native AppHost container-demo pass.

Architecture/security, test and UX source reviewers accepted the corrected scope: callback-set binding, strict managed metadata, pre-dispatch expiry, original deadline minima, stale proof/crypto suppression, precise transport/status assertions, shared error-present markup, and focus/expiry guidance. Final scoped execution passes **36 Applications cases per provider, 1,820 Host cases, 46 Node cases and 67 selection controls**, with zero final failures/skips. Full result/definition/entry/execution linkage, all previous Host portable identities/multiplicities and all 2,321 prior administrator metadata records/order are preserved. The two new provider cases bring the native inventory to 2,323. Independent final raw-evidence review passes for this bounded scope; the manifest records its disposition and limits.

## Retained acceptance work

The prior Chromium download returned 403 from `cdn.playwright.dev`; no blocked download is retried here. Firefox remains excluded under the recorded user decision. The following T014/T019 obligations remain **not run**, not waived or counted as passes:

| Scope | Required observation |
| --- | --- |
| PostgreSQL/SQL Server × Chromium/WebKit | Genuine visible create → one-time output → sanitized details on the native Aspire graph |
| Both themes, narrow viewport | Layout, contrast, wrapping, keyboard-only entry, focus, linked errors and automated accessibility |
| Hidden tab, pagehide, history/BFCache, navigation | Actual DOM removes output before capture; later responses cannot restore it |
| Original session/output/credential expiry,401/403, interrupted POST | Accurate visible state, no duplicate request, no secret disclosure, list/details recovery |
| Native Aspire demos and Windows Docker Desktop | Full graph launch/initialization and interactive journey on actual target platforms |

T007 retained-browser work, T019 browser acceptance, T022/T023 full feature verification/review/squash and every historical/human/Windows/A-009/vendor/production/public-release gate stay open. Refresh remains disabled and all six findings remain open. The next bounded implementation is **T015 edit/replacement concurrency and rollback provider tests**, followed by T016 endpoints and T017 visible forms; browser blockers do not restart diagnostic-only work.
