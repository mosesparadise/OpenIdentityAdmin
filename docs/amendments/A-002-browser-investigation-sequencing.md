# Browser investigation sequencing amendment A-002

Status: Approved by the requesting user on 11 October 2026. A-001 is a separate amendment.

## Approved decision

Permit the reviewed development changes in PR #10 to be squash-merged after all checks pass on its exact final head, and permit T028 to proceed in its own reviewed and squash-merged PR. Keep T027 incomplete while the historical WebKit contrast finding remains unresolved. This changes the user's task sequence; it does not declare that finding fixed or waive a failing check.

## Evidence and uncertainty

- The cross-site antiforgery-cookie defect was reproduced before its correction. Explicit SameSite=Lax matches the approved contract, passes actual pending-tab continuity, and preserves native rejection of a cross-site POST carrying a copied valid form token. Security review accepted the bounded correction.
- Source `b268c9c` passed all 14 checks and the then-required 743 tests in [run 38100113749](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38100113749).
- Documentation head `82f2cd2` subsequently failed one PostgreSQL/WebKit dark/1280 error-page color-contrast scan in [run 38101226608](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38101226608). That report retained the rule name but not the failing selector or colors. Its cause is undetermined; it is not established as a product defect or a scanner false positive.
- Subsequent stable-page, fresh-response and independent-context experiments have not reproduced it. Safe diagnostics now preserve allowlisted selectors, colors, ratios and computed state on a failure. No original scan or rule is excluded, and failures remain fatal.
- A real bad-contrast/corrected-page control exposed diagnostic and fixture defects, which received fixes and independent review. These are separate from the historical application finding. Current scope is 24 browser cases per provider/engine and 749 required .NET cases for the full matrix, with zero skips. Exact-head results are recorded on [PR #10](https://github.com/mosesparadise/OpenIdentityAdmin/pull/10); passing probes alone do not close T027.

Reviewed head `ef0464c2ab40c7855bacaba2decf463f7eda7ebe` passed all 14 checks and all 749 required .NET cases with zero skips in [run 38120855109](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38120855109): 229 unit/architecture, 188 HTTP/provider cases on each database and 24 browser cases in each of six cells. All nine module coverage floors passed. This verifies the diagnostic/control corrections and current regression run; it does not resolve the historical finding.

## Approved boundaries

1. Merge PR #10 only after exact-final-head static, assurance, dependency/license/secret, unit, both HTTP providers, all six browser cells and combined coverage gates pass. Retain every existing deadline and all nine 90% line/80% branch floors. Resolve substantive review findings and obey GitHub restrictions; no bypass or self-approval claim.
2. Leave T027 and its unresolved acceptance checkbox open. Record the exception in the approval record, roadmap, task list and PR. Do not count the historical intermittent finding as clean feature acceptance.
3. Implement only the already-authorized T028 next: seeded critical-decision mutations and complete regression/evidence review, through its own PR. Keep the original accessibility scan, new diagnostics and negative/positive controls blocking. No failed scan is retried into a pass, disabled, marked expected failure or silently quarantined.
4. If the contrast finding recurs, use the captured node/color/state evidence to investigate before accepting the affected PR. No automatic workflow rerun substitutes for analysis.
5. The implementation lead owns the investigation, with independent test and UX review. At T028's acceptance review, explicitly record the open T027 finding and verify all current gates before its authorized squash merge. This sequencing exception expires immediately after that merge or at a recurrence, whichever occurs first. No subsequent task or milestone progression is authorized by this exception; further progression requires the finding's disposition or a separate explicit extension.
6. Full T027/M01-D acceptance, production/public release, accessibility/conformance declarations and the approved external assurance gates remain blocked by their outstanding requirements. This amendment changes no ASVS status or certification claim and authorizes no external communication or deployment.

## Closure evidence

Close the historical finding only with an evidence-based disposition: reproduce and correct a defect with a permanent regression, or substantiate a scanner limitation and independently review an equivalent blocking check and any proposed exception. A-002 does not authorize replacement or exclusion of the original rule; an equivalent-check exception requires a separate reviewed decision. Nonreproduction and a green rerun alone are insufficient. Record the actual evidence and its limits.

## Approval record

On 11 October 2026, the requesting user stated:

> I approve A-002: merge PR #10 and complete T028 under the documented conditions, keeping T027 unresolved and release-blocking.

This explicitly authorizes the bounded sequence above. All conditions and expiry remain mandatory.
