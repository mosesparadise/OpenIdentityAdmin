# T022 — independent account composition

This packet implements the composition prerequisite from the reviewed MFA design. It maps ADMIN-003/010/012 and FR-07/11/21/22 to explicit activation, scheme/route isolation and shared protection; NFR-08/11 map to secret-safe failure and verification. It does not implement MFA-01–13 acceptance or administrator sessions.

## Implementation boundary

Keep existing projects, thin Program, Composition extensions and explicit Features/Setup. Accounts owns shared Identity/form/antiforgery options, rate/concurrency/deadline protection, database connection/command bounds, safe failures and diagnostic suppression. Pilot registration retains its public compatibility entry point and logger category. Combined activation registers the shared pipeline once.

`Identity:AdministratorAccounts:Enabled` defaults to false. Only literal true/false (case insensitive) are accepted; unknown/nested fields and scalar sections fail startup with a fixed diagnostic and exit code 2. Activation requires the existing validated durable issuer/data-protection configuration. This setting is a composition seam, not an operator instruction to enable unfinished account journeys.

Pilot remains independently controlled by `Pilot:Enabled`. Administrator-only composition does not activate pilot login/complete or protocol handlers. Native Razor route models use exact page/route allowlists; unknown pages and aliases are removed. Administrator entries describe future pages only: none is shipped by this packet. Administration APIs, BFF access and documentation remain unavailable.

The administrator scheme is `IdentityPlatform.Administrator`, with its own secure HttpOnly host cookie, no sliding renewal or forwarding. Its current validator rejects every ticket, and its issuance hook refuses sign-in. This deliberate gate must be replaced by T025 authoritative session checks, not removed to make login work. In administrator-only composition, the explicit anonymous default prevents ASP.NET Core's single-scheme auto-default from implicitly selecting administrator authentication. Combined composition preserves the pilot default and named administrator isolation.

Shared transport safeguards preserve 600 requests/minute and 16 concurrent account/protocol requests, a ten-second cooperative deadline, 16 KiB request bounds, safe fixed failures, non-caching headers/CSP, and sensitive-framework diagnostic suppression. Five-second native connection/command bounds are configured for both providers. These are not the authoritative distributed MFA budgets or transaction lock policy; T024 owns those.

## Verification plan and evidence boundaries

New tests live under the existing Host.Tests/Features/Accounts:

- CompositionTests: all four real composition combinations; actual compiled pilot-route isolation; strict opt-in; native authentication defaults; synthetic future-page/alias filtering.
- AuthenticationTests: validly protected administrator and cross-scheme tickets cannot authenticate; issuance fails; challenge/forbid have no login redirect or session.
- ProtectionTests/ConcurrencyTests: administrator-only body/deadline/admission enforcement, one budget across real pilot and administrator probe traffic, released permits and all admitted responses.
- StartupTests: execute AuthServer Program from test output and prove malformed/unknown/unprotected activation exits safely before listening.
- IsolationTests: safe database-resolution failure; native provider timeout options; actual host logging filter registration and options recreation.

TestServer probes establish pipeline behavior, not usable administrator pages. Provider option checks do not establish real lock cancellation. Existing pilot operations, native-provider suites, actual logging reload, Kestrel oversized/chunked forms and browser suites remain regression requirements. T023–T029 supply actual protected storage, distributed budgets, sessions, journeys and their live browser/provider evidence. No test substitutes fake authentication for future administrator acceptance.

## Review dispositions

Independent architecture/security source review accepted explicit schemes, exact Razor allowlisting, shared registration, unchanged startup disposal and pilot compatibility. Its database-resolution observation was fixed by including resolution/bounds in the shared safe failure boundary; a permanent regression verifies this path.

Independent test review identified a weak combined rate test; it now alternates 300 real pilot requests and 300 administrator probes before shared denial. It also required every admitted concurrency response to be 204, and accurate naming of options recreation rather than reload; both are corrected. Exact-head CI remains the acceptance authority.
