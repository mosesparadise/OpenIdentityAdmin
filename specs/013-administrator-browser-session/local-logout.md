# T017 protected local logout

**Historical implementation candidate; superseded by [accepted internal delivery](#accepted-internal-delivery--25-october-2026) below.**

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

## First hosted run — rejected for an unused import

PR #89 source `de6bfed54b8f37f78c7eaf579c68530faef87956`, tested merge `25189c162807d601ecb3f0bd653092277159ac83`, identical tree `3a19d0bb4a0ca5b1cc4b659a08cc50f55f1356f0`: [run 40218089626](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40218089626), attempt 1, completed with 69 successful jobs and two failed jobs (static analysis and consequent final acceptance).

The sole root failure was IDE0005 for an unused Browser namespace import in `BrowserLogoutLifecycleTests.cs`. The correction removes that import; local whole-solution IDE0005 verification passes. No product behavior, assertion, provider identity, deadline, floor or browser deferral changes. The complete run was retained before the correction was pushed.

Both expanded Product groups passed 96/96 with zero skips: PostgreSQL 425.195 seconds, SQL Server 536.763 seconds, within the unchanged 660-second process limit. Foundations, all other native/browser/contracts/mutation jobs, hosted unit verification (including 1,457 Host cases) and aggregate coverage also succeeded. These are hosted job/log observations, not final corrected-source raw-artifact acceptance. The original source is rejected as whole-run acceptance because its mandatory style/final gates failed. A full corrected-head run and independent raw evidence review remain required.

## Accepted internal delivery — 25 October 2026

[PR #89](https://github.com/mosesparadise/OpenIdentityAdmin/pull/89) was normally squash-merged as `1046d512a2b49936872069f86c8517c6ca788e6f` after all 71 exact-head jobs and independent architecture, security, test, API and raw-evidence reviews passed. The exact head, base, review state and mergeability were checked before delivery; no GitHub restriction was bypassed. The source, tested merge and actual squash have the same tree.

| Boundary | Verified identity |
| --- | --- |
| Reviewed source | `c312c907491ebace0f453694ca13dd009dba926a` |
| Base | `5bb421d9b74de0cf2854f480f97401f49f48b15a` |
| Tested merge | `75ff3fe86af3a2202967e007570897f6d355fca8` |
| Source, tested merge and squash tree | `33e3b23a72d3667057df2fc1317a0bba482f9da4` |
| Actual squash | `1046d512a2b49936872069f86c8517c6ca788e6f` |
| Workflow / attempt | [40219713829](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40219713829) / 1 |
| Coverage / acceptance jobs | `121253260618` / `121253390937` |

Independent inspection of five actual unit/provider ZIPs verifies eight raw TRX reports with 2,060 passing executions: 1,552 unit (Host 1,457, Core 25, Architecture 5, AppHost 65), plus 254 BrowserSessions cases per provider (158 Foundations + 96 Product). Results, definitions, methods, executions, entries and list bindings reconcile exactly, with no failure, error, abort or skipped counter. Every prior 1,681 Administrator identity and complete portable metadata remains, with 68 additions producing 1,749; all prior 1,535 unit identities remain, with 17 Host additions. Actual group discovery forms exact disjoint unions. Native selection JSON was reviewed from local actual discovery; artifact ZIPs retain TRX and coverage rather than selection JSON. Hosted runner enforcement and actual checkout logs bind execution to the final merge.

Independent raw contract review verifies 43 core, seven Chromium and seven WebKit native cases, plus 37 exact Node cases, without skips. Both actual production exports match their committed OpenAPI 3.1.1 documents with no activated operations; six export-control receipts and the dormant session contract remain intact. Original export syscall traces/intermediate control files are not retained; viewer screenshots were not visually re-reviewed.

All 54 mandatory coverage XML inputs were directly unpacked and inspected, with actual ZIP byte sizes/SHA-256 values matching final run/head API metadata. Raw merged-Cobertura line hits and quantified branch counts satisfy all 15 unchanged 90% line / 80% branch floors (Admin.Bff 94.1% / 98.4%, rounded). No floor, exclusion or required input was relaxed. All eight actual mutation ZIPs independently verify exact suite/provider/source bindings, unchanged manifests/verifier, original and reconstructed mutant hashes, 48 kills, 48 passing baseline controls and 48 passing restored controls, zero skips. Mutation ZIPs retain sanitized receipts, not raw mutant TRX; no raw mutant-report inspection is claimed.

The first rejected run and local pre-assertion Aspire startup failure remain above. The corrected run resolves the unused import without a product/assertion change; it does not prove historical infrastructure causation. Source/API reviews include actual route framing, local security headers, disposed-scope response handling, authenticated uncertain-logout recovery and exact B1 audit provenance.

| Retained final artifact | ID | SHA-256 |
| --- | --- | --- |
| administrator-mutations-PostgreSql | `12608937189` | `6f3ae2b5508760bbed22ee83fe5afddfdf92754fcf3d45667e6120144359311d` |
| administrator-mutations-SqlServer | `12608884151` | `dcadcfb1a225b1d5e38a2b86ed29932c84d0c3b2d55dc0f636e884e5cbcc14a6` |
| administrator-protocol-mutations-PostgreSql | `12609331384` | `f3640127021fba223940bec4517ad7d2699a9eeed1ffcdcbd63ebb3a7744c8b8` |
| administrator-protocol-mutations-SqlServer | `12608648736` | `dc6e286c78ca379b36e6cff256e7a37a7e841c537ac62cf0fe0af137e99a8d82` |
| critical-mutations-PostgreSql | `12609382666` | `774c0ccebf6e84600537c06ba8fb977fc4093004cbd569d96620b0b240cfacc9` |
| critical-mutations-SqlServer | `12609407929` | `72fa6e7d7139ef01e5b0a79d281d9ecc12e69e6dee5bc8d03f6469b22a4e1bd5` |
| foundation-administrator-PostgreSql-browsersessions-foundations | `12608927894` | `81fa341178190f77e1b3a4f671d0948f6d5c507b0d0cf11cb17e29df3dea1379` |
| foundation-administrator-PostgreSql-browsersessions-product | `12608413694` | `096aaa54302076cf483066df21938c8b42502b123a199d0c12dffd1806ea55d2` |
| foundation-administrator-SqlServer-browsersessions-foundations | `12609057065` | `dfa804cafc47498d1d4b34c5ce55782bc5880a036383335a8e5d2ce8253d6a7d` |
| foundation-administrator-SqlServer-browsersessions-product | `12609174167` | `5700b1048850cbb0b78000ed51e5b778ddaef670b535e2c49b9ed6e419de13c3` |
| foundation-contracts-chromium | `12608883436` | `18e7f649123f6fa9b2f1e7b27f532ec77f3af84b4943b64d6ecc3f15a5feb18b` |
| foundation-contracts-core | `12609298084` | `f7c7cf9c66d5917b7e2318c1f525260817138d6622472e705e148c4940637f1d` |
| foundation-contracts-webkit | `12609293034` | `1f2463934b57fac26bb84ab0008081f3957f3ef20be34c7da1f138ef11d7dd7a` |
| foundation-coverage | `12608959859` | `b3301585a1e73424cafd7eef11f0412351942223da70ade1c63bf9816f6833e1` |
| foundation-unit | `12609461978` | `47a6f92e39f05b7b779d94793724e4cdb2f80444a1f2dd9afb68c7d9f5f3b6cc` |
| governance-mutations-PostgreSql | `12609168116` | `c15f5a5489b6ae058034738895152fa2023b531982026573f18c4da5dea82a7d` |
| governance-mutations-SqlServer | `12608522743` | `862fac660d62e3ea3c985bcaa3ab7ba3395b0e10025f79c1f9f44ac097b10de6` |

**T017 is complete for internal development.** Remaining T015 process restart, selective restored-boundary consequences and genuine product orphan cleanup/quota tests continue separately; full T015 stays unchecked until that evidence passes. T018/T019 issuer-assisted logout and T012/T016 actual browser/TLS evidence remain open. No production composition, full B3, refresh, current API authority, React or public release is complete.

A-009/T028 browser restoration, PR #69/issue #70, all six historical findings, 48 human rows, 345 not-assessed controls and activation/public-release gates remain open. No expired diagnostic approval is revived.
