# Feature024 implementation checkpoint

14 November 2026. PR136 remains draft. This records local implementation and focused
verification, not both-provider/browser acceptance or a merged feature.

## Implemented boundaries

Separate Development-only sample client and API now compose into ordinary Aspire.
The authenticated dashboard configures one manually registered client over bounded
stdin into a private per-run file. Native OpenIddict code/S256/basic authentication,
reference state, server-held cookie tickets, current UserInfo, offline JWT resource
validation, finite themed views and local logout are implemented. No sample database,
platform database credential, issuer private key, browser token or custom launch script
is introduced. Existing providers and histories are unchanged.

Native state is bounded and CAS-protected. Callback admission is terminal on failure
or logout; original request/session expiry governs response publication, including
native events that ignore cancellation. API validation requires RS256, exact issuer,
audience, access-token type, expiration/not-before and orders.read. Native and explicit
HTTP paths reject encoded responses before decompression. Current profile projection
accepts only matching subject and the consented finite email pair.

## Local evidence and limits

SDK10.0.404, OpenIddict7.7.2, Aspire13.6.5 and retained Playwright1.63.0.
All builds use `/tmp/sample024-build` because the existing workspace filesystem is
full. Previous artifacts and working branches are preserved. Locked whole-solution
restore and whole-solution build pass; no package upgrade or coverage exclusion.

- `/tmp/sample024-allhost2`: all2,076 native Host cases passed, zero skips; includes
 86 sample cases. `/tmp/sample024-realhost8` separately passes86/86 after stronger
 native denial and publication/cancellation assertions. The isolated
 snapshot315df022 (same tree70fa218 as published8781cbe) passes all three actual
 sample mutants, with baseline/restored3/3 and restored source hashes. Original
 receipt: `/tmp/sample024-mutations/artifacts/mutations/sample-unit/evidence.json`.
- `/tmp/sample024-harness-all2`: all118 Aspire Harness cases pass. The earlier
 `/tmp/sample024-harness-all1` failed one source-location lookup because artifacts
 live outside the repository; temporary source links restore the ordinary lookup
 layout without changing that test. No container/runtime claim follows the Harness run.
- `/tmp/sample024-architecture1`5/5 and `/tmp/sample024-core1`94/94 pass.
- `/tmp/sample024-coverage6.cobertura.xml` observes client91.969% lines/86.319%
 branches and API95.968%/85.294%. These focused native reports are not a substitute
 for the114 original hosted coverage inputs or the17 final module floors.
- Seeded CI-scope8, browser-matrix7, coverage-identity36, AppHost-selection8,
 sample-mutation-selection2 and mutation-evidence64 controls pass. Syntax-aware
 quality checks pass without growing any legacy allowance. Native AppHost discovery
 includes23 cases per provider; four new sample browser cases retain all old groups.

Preserve failed intermediate receipts. `/tmp/sample024-realhost5` mixed a newly
compiled regression with the prior sample assembly while a solution build was in
progress and failed paused-header cancellation. The subsequent coherent build proves
that regression. `/tmp/sample024-realhost6`85/86 failed an expected-status assertion
(native denial is400); `/tmp/sample024-allhost1`2073/2076 also caught a deletion-cookie
assertion and two stale literal CSP hashes after the reviewed CSS change. Corrected
assertions retain exact policy hashes, deletion-only cookie semantics, issuer route
witnesses and zero credential exchange for hostile metadata. None of those failed
runs is counted as acceptance.

## Independent reviews

Main owns all edits. Architecture, test, security, UX and Aspire reviewers independently
inspected scoped source. Resolved findings include lazy native options startup,
canonical health-probe Host, health before setup, dashboard availability publication,
pre-decompression bounds, JWT lifetime/HTTP2 body admission, terminal native ticket
admission, cancellable response startup, secret capture guidance, profile wrapping and
themed recovery. Test review strengthened actual HTTP logout/expiry ordering, unknown
length body rejection, exact native denial oracles and unconditional paused-callback
cleanup. A representative publication-gate mutant joins state-CAS and ticket-removal
mutants; only demonstrated bypass markers may kill them.

New-host configured-session tests prove old cookie rejection. The consumed callback
replay in that test is explicitly replay evidence, not proof of replaying an unconsumed
pending callback with all original cookies across restart. The real Aspire restart
journey separately proves disposable setup/waiting state. UI maximum-length reflow
uses explicitly synthetic256-character text after real profile success and restores it;
it is presentation evidence, not a fabricated identity result.

## Remaining acceptance

Run the full exact-head workflow, including real PostgreSQL/SQL Server × Chromium/
WebKit sample journeys, current-policy UserInfo denial versus offline API acceptance,
three sample mutants, unchanged prior cases and all17 floors. Reconcile original
reports and independent final evidence before checking T004–T014 or normal squash.
Windows/manual, Firefox/A-009, all six historical findings, unknown CI590/CI592 causes,
refresh/vendor/production/public-release gates remain open. No old SQL diagnostic is
renewed and no isolated diagnostic branch is included.

## CI599 preflight correction

[CI599](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/43615154596)
at8781cbe exposed stale independent provider totals in lifecycle/issuer selection
controls, stopping the affected provider/sample jobs before runtime execution. Standard
provider count is356, including23 AppHost cases; totals are1759 PostgreSQL/1866 SQL
Server. Corrected constants retain exact/disjoint checks. All28 workflow Python control
scripts pass locally. Native sample mutation CI already passes all three decisions.
Static analysis also identified import ordering/unused directives; the formatter fixes
only those directives. Preserve this failed run; no absent runtime artifact is a pass.

Original archive download through the GitHub connector succeeded, but the returned
file-service host `sdmntprnortheu.oaiusercontent.com` received proxy CONNECT403 on this
executor. Current enforced environment policy lacks that host. Decoded GitHub logs remain
readable; raw archive reconciliation is still pending, not replaced by a log-only claim.

## CI600 browser integration correction

[CI600](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/43615830444)
atd7451a3 passes static analysis and the three sample mutations. All four new sample
browser cells fail: both Chromium cells during authentication and both WebKit cells
during the broad startup phase. Original decoded job logs are retained under
`/tmp/sample024-ci600-evidence/logs/`; original archives remain inaccessible as above.

Independent source review identifies two concrete integration defects. The sample
WebKit lane omitted OS trust for the exact public development certificate, unlike the
established WebKit lane. Actionable sample forms used no-referrer, which yields a null
browser Origin while server mutations correctly require the exact sample Origin.
The correction provisions the same public leaf in the ephemeral runner's OS trust
bundle and uses strict-origin on actionable forms, retaining no-referrer for inert
responses/native redirects. Exact Origin, native antiforgery, TLS validation, assertions
and deadlines are unchanged. Native form/redirect tests assert both header policies.

The prior exception wrapper discarded causal locations. Startup now distinguishes
browser creation, waiting-page navigation and page assertions; consent distinguishes
Allow from Cancel. The existing safe diagnostic formatter retains only closed error
categories and allowlisted source filenames/line numbers. No URL, browser body,
credential, protocol parameter or raw exception is retained. Independent test review
passes this bounded correction. CI600's precise failure causes remain unproven until
new runtime evidence; transient database startup warnings are not causal evidence.

Local correction verification: whole-solution build passes with zero warnings/errors;
86 sample Host cases and118 Aspire Harness cases pass. Syntax-aware quality and
retained browser-matrix controls pass without changing allowances or required counts.
Full provider/browser acceptance and original archive reconciliation remain pending.

## CI601 runtime result and Host scheduling

[CI601](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/43617510791)
at8701511 passes all four real sample journeys: PostgreSQL Chromium/WebKit and SQL
Server Chromium/WebKit, with one complete native case and zero skips per cell. These
cover the actual UI registration/setup, fresh password/MFA, Cancel/Allow, native
callback, UserInfo/API, both themes and layouts, local logout, browser isolation,
policy change and fresh disposable run. Static analysis and all three sample mutations
also pass. Original decoded logs are retained in `/tmp/sample024-ci601-evidence/logs/`;
archive reconciliation remains blocked, so this is not full acceptance.

The unit job fails: the first configured GET in the native sample lifecycle case
aborts at the unchanged10-second request deadline, and the Host process subsequently
exceeds its unchanged180-second suite deadline without a complete report. Its cause
is not established. Native ephemeral RSA initialization and concurrent host tests are
a possible contributor, not a proven diagnosis. Whole Host reproduction under coverage
passes2076/2076 in57.431s; an additional run restricted to two CPUs and processor
count2 passes2076/2076 in105.229s, within the same180-second outer bound. Neither
local pass explains the hosted failure.

Independent test review recommends a bounded scheduling change: the five classes
using the real native sample host share a nonparallel xUnit collection, so other test
collections cannot compete during cold initialization. No native options prewarming,
request/suite deadline extension, skipped case, scenario-name/count change or production
change is made. Deliberate concurrent redemption/logout/publication operations inside
each case remain concurrent. Preserve CI601 as failed evidence; new exact-head
verification is required and no stable-suite/root-cause claim follows from isolation.

After isolation, the same two-CPU covered Host run passes2076/2076, zero skips,
in113.554s within the unchanged180-second bound. Build/style/quality checks pass.
Native discovery preserves the complete2076 scenario/type/trait multiset. Sample
TRX IDs change with collection identity; scenario names and counts remain intact.
The complete report reconciles current discovery by name multiplicity and verifies
unique native IDs, result IDs, definition links and execution bindings. Legacy
duplicate display names prevent using the provider-only unique-name helper.
This is local scheduling evidence, not an explanation of CI601 or hosted acceptance.
