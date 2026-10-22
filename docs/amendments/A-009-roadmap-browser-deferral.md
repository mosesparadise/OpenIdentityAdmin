# A-009 — Continue roadmap implementation with browser-tooling deferrals

Approved by the requesting user on 22 October 2026: **“Can we proceed with the actual implementation of the roadmap? Any Playwright issues or Firefox blockers should be documented and bypassed.”** This is new authority, not a renewal of A-004/A-005/A-006.

## Scope and ownership

Continue internal roadmap implementation, beginning with feature 013 administrator browser sessions (B3). This supersedes A-007's B2-only expiry and the feature 011 T028 / feature 013 T002 requirement to resolve Firefox before executable B3 work. The requesting user owns the deferral; the implementing maintainer owns the backlog, visible CI selection and restoration before production/browser-support/public-release acceptance. Review the deferrals at each coherent feature delivery. They do not silently expire into a development halt or become completed evidence.

Known Playwright/Firefox defects are documented deferred work and do not block internal implementation or its review. Preserve the original tests, failed evidence and assertion semantics. Do not substitute a skipped test for a pass, add retries/sleeps, ignore TLS errors, or weaken password/MFA, native protocol, CSRF, current-authority or transaction checks. Continue meaningful .NET, native-protocol, both-provider, Chromium/WebKit and security checks that are independent of the documented issues. A newly observed product defect is not automatically a Playwright defect.

This instruction permits normal implementation and required verification. Internal delivery may use an explicitly documented, narrowly scoped deferral; external release and browser-support claims still require the outstanding evidence. Non-browser failures retain their normal investigation and delivery requirements. No branch-protection bypass, deployment or production activation is authorized.

## Deferred issue register

| Issue | Evidence and current disposition | Internal development treatment | Restoration obligation |
| --- | --- | --- | --- |
| Firefox history observation completes before a later commit | [Feature request #70](https://github.com/mosesparadise/OpenIdentityAdmin/issues/70), retained PR #56 diagnostics, feature 011 T028. No supported operation/document-completion contract has been established. | Firefox execution and historical closure are deferred; T028 no longer blocks B3 code. Keep the seven Firefox coverage inputs visibly deferred. | Restore Firefox and review the actual history semantics before browser-support/release acceptance. |
| WebKit certificate-negative case reports cancellation without a certificate-specific marker | [Run 39649668195](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/39649668195), SQL WebKit job 119394530270, artifact 12370636126: 29/30 pass, one strict failure. Earlier PostgreSQL recurrence remains retained. Cause unestablished. | This known browser-evidence failure does not halt roadmap code. Retain the strict test and other WebKit coverage. Any delivery exclusion must identify this exact case/provider/engine and report it as deferred. | Obtain complete native/public evidence and a reviewed correction or supported limitation; passing reruns alone do not close it. |
| Upstream report submission | Integration returned HTTP 403; #70 holds the complete report for manual upstream submission. | An upstream response is not a prerequisite for internal implementation. | Link the upstream issue when submitted; do not invent a maintainer response. |

The local WebKit diagnostic preparation may be retained for later use; roadmap implementation takes priority over further diagnostic-only work. PR #69's existing restoration work remains preserved as draft; this amendment neither merges it nor discards its evidence.

Implementation selection is explicit: `scripts/browser_matrix.py` retains Chromium/WebKit and visibly defers Firefox under A-009. After validating the complete native issuer inventory, `scripts/browser_deferrals.py` removes exactly the certificate-negative case above from each WebKit provider invocation. The other 29 issuer cases remain mandatory, along with all administrator WebKit cases. Chromium still executes the certificate case. Native discovery must exactly match the reduced selection; identity drift, missing/duplicate deferred cases and unknown scopes fail. Each invocation retains a `browser-deferrals.json` receipt and CI summary stating `deferred_not_run`. No test body, skip attribute, retry, timeout, coverage input or module floor changes. All 50 active B2 coverage inputs remain required; the seven Firefox inputs remain deferred.

## B3 sequencing

Start T003's native identity/state feasibility and T007's independently testable claim/canonical-ticket policy now. Those pure boundaries may be implemented before the remaining transaction, issuer-continuation/PAR and end-session feasibility work. Dependent product routes still require the corresponding proven native ownership and durable-generation contracts. This is an explicit refinement of the planning task dependencies, not completion of T003 or activation of a partial B3 story.

All six historical findings, 48 human rows and 345 not-assessed assurance controls stay open. Provider plugin interfaces, existing databases/migration histories, mandatory real-provider verification, security mutation semantics and coverage floors remain intact. The approved v0.2 snapshot is unchanged.
