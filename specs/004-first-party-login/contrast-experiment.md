# T027 — Focused WebKit contrast experiment

Status: implemented; pinned CI execution pending. Authorized by the user's “Please proceed” instruction. This is a diagnostic extension of LOGIN-007/009 and T027, not a new acceptance exception. PR #11 remains unmerged; A-002 remains expired. The existing pilot scans, 100-context WebKit cases, rules, deadlines and coverage gates stay intact.

## Question and scope

The captured error-page failure reported white behind the wordmark, badge and footer; the later computed body color also disagreed with the authored dark theme. Pinned Playwright screenshot preparation deliberately invalidates body styling, so the later readable PNG does not prove the state during the preceding scan. Source inspection has not established a product defect or a scanner limitation.

Use a small browser fixture under the existing `Issuer.Tests/Features/Browser` hierarchy. Serve the actual embedded `tokens.css`, `login.css`, `theme.js` and `login.js` through a routed synthetic HTTPS origin. Synthetic HTML mirrors the relevant login/error layout, labels and navigation, but does not contain an issuer, database, tokens, real antiforgery values or authentication decisions. It is a rendering experiment, not replacement login/TLS/protocol evidence.

Each sample launches a fresh browser/session at 1280×900 with reduced motion, navigates to login, selects dark through the shipped theme control, performs the assigned initial intervention, reloads, submits a real synthetic form POST to the error response, clears the username and focuses the error summary. The final error-page check always uses the same unmodified full `RunAxe` path. Do not insert computed-style reads or screenshots before that final check. On failure, preserve the original violation and existing safe before/after-capture diagnostics, with only a fixed arm name and bounded sample number added.

## Comparisons

| Arm | Initial intervention | Initial screenshot | Interpretation against reference |
| --- | --- | --- | --- |
| Reference | Full .NET wrapper scan | Yes | Closest rendering-only approximation of the observed journey |
| NoInitialScan | None | Yes | Isolates the complete initial scanner operation |
| PartialScan | Native `axe.runPartial` with the pinned script/configuration, without a finishing page | Yes | Separates target scanning from the wrapper's finishing-page lifecycle |
| PageLifecycle | Create and close a separate 1×1 browser page/context, without axe | Yes | Tests page/context activation alone |
| NoInitialScreenshot | Full .NET wrapper scan | No | Isolates the known screenshot invalidation boundary |

Only the initial diagnostic intervention differs. No alternative scanner result substitutes for the final required scan. Rotate arm order by sample number to distribute order/resource effects. The native partial arm must complete successfully and return structurally valid partial results; an exception is not a clean sample. The page-lifecycle arm must use a separate context like the pinned wrapper. Every page/context/browser is disposed on success or failure.

## Bounds and verification

- Add one ordinary browser theory case per provider with one sample of every arm, increasing each browser cell from 26 to 27 and the full ordinary matrix from 775 to 781. This validates the fixture on Chromium, Firefox and WebKit; it makes no additional database claim.
- Run a separate focused WebKit job with a fixed 40 samples per arm (200 fresh sessions), selecting only the new PostgreSQL-labeled synthetic case. Keep a 660-second invocation bound and a 20-minute job limit. The label selects an existing test partition; this job does not use PostgreSQL. Do not merge its reports into ordinary coverage or double-count its case as ordinary acceptance.
- Validate the experiment's iteration setting explicitly, reject unknown/out-of-range values, verify exact test selection/counters, preserve process teardown and fail on the first finding. No retry, quarantine, disabled rule or screenshot baseline acceptance.
- Use pinned dependencies and actual embedded assets. Keep raw DOM, arbitrary CSS, URLs, credentials and browser exception text out of retained output. Screenshots, if captured on failure, contain only the synthetic empty form. Retain the focused report separately for seven days.
- Before CI, build/format and verify actual MTP discovery and the matrix manifest. A local system-Chromium mechanism probe may check fixture behavior but cannot stand in for pinned WebKit results.

## Decision and stopping point

Inspect the first reproduced failure to choose a smaller paired experiment; a difference between arms is a candidate mechanism, not sufficient causal proof. A product correction requires a demonstrated failing control and a passing permanent regression. A scanner limitation requires substantiated evidence and independently reviewed handling; rule replacement/exclusion still needs a separate approved decision under A-002.

After the bounded cohort, record all completed arms, actual counts/timings, failures and unexecuted work. If every arm passes, report the result as inconclusive and identify which hypotheses remain untested. Do not automatically expand repetitions or claim closure from green results. No upstream contact, release, certification claim or subsequent task progression is authorized by this experiment.

## Harness verification before CI

`dotnet build --no-restore --nologo` passed with zero warnings/errors; formatting passed. Actual MTP discovery verifies 181 HTTP and 27 browser cases for each provider and exactly one designated case for the focused runner. Two C# redaction/bounds cases pass with zero skips. Five Python runner controls reject wrong/empty test selection, incomplete/duplicate cohorts, inconsistent TRX outcomes/counters/errors, timeout/pipe-holding/stubborn descendants and cancellation leaks. Every process wait, including captured discovery and cleanup, is bounded; SIGTERM unwinds cleanup.

Independent test, security and UX reviews led to corrections in process containment, report integrity and exact pinned wrapper injection. The comparison uses the installed public `BundledAxeScriptProvider` and the native target-page sequence from Deque commit `46f4556d52f811116148a396ee8fbc5276ec3d7b`; the native partial result is JSON-serialized, structurally checked and never reported as a full accessibility pass.

A local system-Chromium 151 mechanism probe completed all five arms in approximately 1.25–1.88 seconds each. Native partial scanning produced populated results (89 rules). This only checks fixture/API mechanics; it is neither the pinned .NET/browser matrix nor WebKit timing evidence. CI will measure the actual 40×5 cohort against the existing 660-second invocation bound. The focused artifact remains separate from ordinary coverage and acceptance counts.
