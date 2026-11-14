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
