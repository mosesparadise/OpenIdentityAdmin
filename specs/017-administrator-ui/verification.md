# Visible workspace verification

## Candidate and authority — 29 October 2026

Starting remote main is `4814c3e7a1068e3acb58c620157a55e04cf46d44` (PR114 runtime and PR115 evidence preserved). The user approved visible-first delivery: workspace, protected application list, then creation/editing. Feature017 delivers the first slice. Source is implemented on `119-admin-visible-shell`; hosted acceptance and normal squash remain pending. Refresh stays disabled. Historical findings, A-009 deferrals, human assessment, actual Windows walkthrough, production and public-release gates remain open.

## Planning and source review

Independent architecture/security, test and UX planning reviews accepted the completed packet with explicit opt-in composition, generated-contract/runtime validation, immutable observed deadlines, cancellation/lifecycle fences, deterministic embedded assets and real provider/browser evidence. The initial plan-template gap was resolved before final review. Spec Kit prerequisite/checklist checks pass; all seven requirement-checklist rows are complete. Cross-artifact analysis maps every FR below to implementation, tasks and verification; no new database entity or protocol/API authority is introduced.

Architecture implementation review identified headerless HTTP/2 request-body acceptance in the resource boundary. The handler now rejects the server's body-detection feature in addition to framing headers; a permanent regression covers it. Encoded raw-target aliases also fail before publication. Fixed destinations and default host behavior are preserved.

Test review required exact Node identities, nonempty critical-module coverage, caller abort/deadline controls, real mobile disclosure, disabled JavaScript/storage fallback, authenticated/error accessibility and post-authentication storage observations. Those controls are included. Synthetic pagehide/pageshow events prove lifecycle wiring, not an actual browser BFCache restoration experiment.

UX review inspected actual local system-Chromium anonymous renders at1280/light and320/dark. Hierarchy, legibility and reflow were acceptable. All interactive action/menu/select targets were corrected to44px. Anonymous visual preview uses a substituted anonymous response and does not establish authentication or retained-engine acceptance. Automated retained-engine screenshots and genuine journeys remain the acceptance source. Manual screen-reader and Windows verification remain open.

## Traceability

| Requirement | Implementation/tasks | Permanent verification |
|---|---|---|
| FR-001/006 | T003–T005; AdminUi exact embedded routes and local-demo composition | AdministratorUiTests; existing LocalDemoHostTests strengthened; deterministic six-asset rebuild |
| FR-002 | T007/T008; fixed callback and return navigation | Genuine WorkspaceBrowserTests and both-provider LocalDemoRuntimeTests, preserving all replay/disposal controls |
| FR-003 | T006/T009; generated GET, strict observation and controller | Eleven exact Node identities; real read-failure/database-state/deadline checks; malformed/offline UI tests |
| FR-004 | T005/T006/T009; restrictive headers, finite assets, server-held credentials | GET/HEAD/method/query/TLS/body/raw-target controls; storage allowlist after real login/failure/logout; existing secrecy suite retained |
| FR-005 | T004/T009; shared palette, navigation and appearance | WorkspacePresentation system/light/dark320/768/1280, keyboard/disclosure, axe and anonymous screenshots; session/error axe without secret-bearing capture |
| FR-006 | T003/T011; committed assets, notices, native Aspire documentation | Required Administrator UI CI job, reviewed104-entry lock, advisory gate, installed identity/license validation and runtime MIT notices |
| FR-007 | T008–T011; preserve inventory and gates | Native discovery identity/metadata preservation, both databases, retained Chromium/WebKit and ordinary full acceptance |

## Local execution

- Frontend production build and strict TypeScript/generated-byte check pass. Six deterministic assets are embedded, so running Aspire does not require Node or a frontend build command.
- Eleven Node cases pass with zero skips; reviewed exact JUnit identities are checked. Controller and observation modules report100% lines/branches/functions against enforced95/90/100 floors. These figures do not claim React/rendering or generated-client coverage; transport behavior has separate tests and UI behavior has real-browser controls.
- Reviewed npm graph:104 locked entries, zero reported vulnerabilities. Runtime packages and the distributed Tailwind preflight/theme CSS ship their full MIT notice texts. MPL-2.0 lightningcss packages are unmodified build tools, not shipped browser binaries. Three reviewed musl optional artifacts are excluded only on Linux/x64/glibc; missing applicable required packages still fail.
- CI scope and existing contract supply-chain unit controls pass. Host and Administrator/AppHost builds pass with zero warnings/errors; all1,587 Host cases pass. Full local static-analysis execution could not run without restoring every solution project; hosted static-analysis and formatting subsequently pass. Architecture/security and test source reviews pass; hosted browser evidence remains pending.

Initial local failures are retained in task logs: absent restored Host assets, strict C# formatting/analyzer failures, a zero-selection test command, and the raw PathString percent-decoding mismatch. None is counted as a passing test. The corrected request tests explicitly distinguish literal paths from server raw targets. No full hosted pass or merged delivery is claimed by this candidate record.

Local integration found an embedded `theme.js` suffix collision with the pre-existing BrowserDocument loader: three existing Host cases failed. The UI copy is now named `appearance.js`, preserving the original document resource and policy. A subsequent existing standalone-context test exposed optional navigation resolving services from a context without a provider; null-safe opt-in lookup preserves its prior rendering behavior. Final corrected-source Host execution passes all1,587 cases with zero failures/skips. Native discovery preserves all2,005 administrator identities/type/traits and adds eight workspace cases (four/provider), giving2,013; BrowserProtection is15/provider/engine and the browser total is50/provider/engine. Host adds26 cases, executed1,587. Code-quality rules pass without new allowances.

Published candidate: [PR116](https://github.com/mosesparadise/OpenIdentityAdmin/pull/116), source `714b20751a7975f217d1f54dbefc0e2b606155bd`, [run40861819009](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40861819009). Independent review binds source and tested merge `217e68d3` to tree `fefc3c417721703ac6c192695ed7edcb202d1cfc`. Raw frontend artifact12891133509 SHA-256 `ddf721821f36ffb9f83e59b5573058a7f90c33df33630310eb7f51c73c1a6863` independently verifies all11 exact cases, critical-module coverage,104 dependency entries and zero advisory findings. This does not substitute for the remaining real-provider/browser and aggregate checks.

## Rejected first hosted run and bounded corrections

Run40861819009 is rejected for full acceptance: Contracts core stops at the strict viewer-advisory gate because npm replaces an existing qs advisory ID1243452 with1243687. Independent comparison confirms every other field is identical (GHSA-6pq8-jhm2-2vcw), with no changed runtime asset/configuration. The [dated review](../../eng/contracts/vendor/advisory-review-2026-10-29.json) retains all14 old objects plus the exact replacement (15 objects/14 distinct URLs); the finding remains unresolved and all activation/release blocks remain. No guard is weakened.

A local same-shipped-UI Chromium probe also reproduces a fixture error: installing Playwright fixed time after React mounted leaves the controller holding the prior Date.now function. Installing it before the first workspace navigation preserves the clock owner and the exact expiry assertion succeeds. The provider browser fixture is corrected; production expiry code and its already-passing Node controls are unchanged. This preview probe is not real authentication or retained-engine provider evidence. Corrected full hosted acceptance remains pending.

All four original BrowserProtection cells independently show15 executions:14 pass, one fails, zero skipped. The sole failure is the new original-deadline case at committed line228, waiting for the local expired heading; all11 prior cases and the other three new workspace cases pass in every provider/engine cell. Both actual Aspire local-demo jobs pass their complete journeys. All24 anonymous hosted presentation PNGs are retained for visual review. These successful subsets do not make the rejected full run acceptable.

Final distribution review adds Tailwind’s full MIT text to the shipped notices: although declared as a build dependency, its preflight/theme CSS is included in app.css. The locked graph and six-asset inventory are unchanged, and deterministic verification covers the additional notice.
