# T017 protected local logout — implementation candidate

Base: PR #88 squash `5bb421d9b74de0cf2854f480f97401f49f48b15a`, verified against remote main on 25 October 2026. Work branch: `091-b3-local-logout`. This record is pending exact-head hosted verification and independent final review; it does not claim acceptance or production activation.

## Scope and behavior

FR-008/009/011, B3-STATE-01/02 and B3-END-01: the explicit generic-host BFF now exposes an inert local logout confirmation and native-antiforgery POST. It reuses the accepted Sessions-owned `BrowserLogoutService` transaction to revoke the canonical ticket, cancel pending work, advance generation and atomically audit the real actor or anonymous pending cancellation. The new result retains committed timestamps for cookie publication after disposal. Production Programs, issuer transport and existing migrations are unchanged.

A lost local response can be reconciled only through the original authentic generation and immediately following current logout tombstone. A separate native antiforgery purpose, exact matching audit, absent ticket/pending work, serialized recheck and rollback-only acquisition prevent adoption of later authority or a lifetime extension. See the [concrete HTML contract](contracts/browser-session.md#t017-local-html-surface-and-bounded-reconciliation). A fixed finite 503 describes uncertainty and offers an explicit check. No issuer request or issuer-success claim occurs.

## New verification

| Boundary | Permanent controls |
| --- | --- |
| Actual local logout | `BrowserLogoutTests`: genuine password/MFA/native callback, positive status, inert GET, real B1 revocation, browser and B1 audit provenance, old-cookie denial, new explicit start; lost-response reconciliation with unchanged snapshot and original proof timestamps |
| HTTP protection | `BrowserLogoutFramingTests`, `BrowserLogoutCookieTests`: exact origin, native CSRF, extra/duplicate form fields, malformed/duplicate cookies, actual live foreign B1 handles during ordinary and reconciliation admission |
| Actual provider ordering | `BrowserLogoutRaceTests`, `BrowserLogoutRecoveryRaceTests`: both observed database writer orders for callback/logout and reconciliation/new login; pending cancellation remains anonymous and cannot revoke later authority |
| Failure/uncertainty | `BrowserLogoutFailureTests`, `BrowserLogoutRecoveryFailureTests`: actual browser/B1 audit INSERT denial and restored positive control; genuine committed acknowledgement loss, exact context disposal failure and expiry; finite 503/no cookie, later explicit reconciliation, unchanged recovery state |
| Reconciliation negatives | `BrowserLogoutReconciliationTests`: wrong generation, associated ticket, pending/reserved intent, absent/duplicate/wrong-operation/wrong-correlation audit, expiry and intervening real sign-in |
| T015 subset | `BrowserLogoutLifecycleTests`: actual successful callback response withheld from original browser, later genuine cookie denial after logout/replacement, no token resubmission, retained protection-file reload and independent incapable/capable roots |
| Host/contract | `BrowserLogoutBindingTests`, extended `BrowserLoginCompositionTests`: isolated CSRF purpose, original generation, bounds, reserved callback aliases; JSON browser export continues to exclude HTML forms |

The callback nondelivery fixture uses detached TestServer cookie jars; it is not a physical network disconnect. Retained-file reload imports new protection/native credential objects into independent roots; it is not process restart. No synthetic `EstablishAsync` B1 ticket supplies product acceptance.

## Local evidence and limits

- .NET SDK 10.0.401/runtime 10.0.12. Existing restored worktree and package cache reused.
- Host build passes, zero warnings/errors. Final focused BrowserSessions Host run: 283 passed, zero failed/skipped, including eight actual logout adapter framing cases. The first new framing run exposed a noncanonical fixture URL; correcting that fixture produced the final pass without changing product policy.
- Administrator build passes with zero warnings/errors; source-quality seeded controls and unchanged legacy baseline pass. Native discovery preserves all 1,681 prior identities/metadata and adds 68 provider cases: 1,749 total, 254 BrowserSessions/provider (158 Foundations + 96 Product). All four actual native group discoveries match their disjoint manifests. Python regression suite: 226 passed.
- Full local unit pass before the final eight route-framing additions: 1,544 tests (25 Core, 1,449 Host, 5 Architecture, 65 AppHost), zero skipped. Final expected Host count is 1,457; hosted exact-source acceptance remains required.
- One focused PostgreSQL invocation selected the two `BrowserLogoutTests` cases. Aspire failed startup with its 60-second `HostStart` timeout before assertions; final TRX reports two failures, no passes/skips, approximately 184 seconds. This is rejected evidence and was not retried. It does not demonstrate a logout failure or success. Current managed environment network revision 1646 reports enforced policy and both MCR data hosts allowed; no database experiment or policy bypass was performed.
- Both-provider hosted tests, full exact-head checks, raw evidence review and independent final review remain required. No coverage floor, required identity, process/job deadline or browser deferral may be weakened.

## Review and remaining scope

Independent architecture/security design reviews accepted the bounded reconciliation approach. Draft architecture review found that an exception after local scope disposal could render the new 503 against disposed RequestServices. The boundary now restores original services before either catch renders a result; actual outer-disposal failure tests require the finite response. Test review requested genuine authenticated uncertain-logout recovery and an exact B1 revoke audit oracle; both are included. Independent architecture, security and test source reviews pass the implementation candidate; the API/host reviewer requested logout-specific framing/header controls, now added. Final exact-source hosted evidence dispositions remain pending.

T017 remains unchecked until verification is accepted. Full T015 remains open: actual B3 process restart, restored-boundary consequences and product orphan cleanup/quota integration still need their own decisive evidence. Previously accepted callback/replacement, duplicate callback, audit rollback, unknown establishment commit and foundation cleanup/capacity tests are preserved, not repeated as new work. T018/T019 issuer-assisted logout, T012 browser/TLS evidence, production composition, full B3, A009/T028, PR #69/issue #70, all six findings, 48 human rows, 345 not-assessed controls and public-release gates remain open.
