# Focused GitHub Actions certificate diagnostic proposal

Prepared 15 October 2026 (Europe/London) after the user accepted review of the next focused diagnostic. This is diagnostic preparation, not a browser execution, CI acceptance, incident closure or release authorization. PR #50 stays draft and unmerged. A-004 remains expired; A-005/A-006 remain concluded. All six historical findings, 48 human assessment rows and activation/public-release gates remain open. Do not renew the witness-size exception or mark feature 009 T020 delivered.

## Question and existing evidence

PR #50 source is `6fcb96216eca0974041d3e8346adc4ce8458789d`; main is `5f4fce7fe0c7761e6eab7308f2cd38908937af51`. Full run [38483679324](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38483679324), attempt 1, remains failed. PostgreSQL/WebKit job [115592186294](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38483679324/job/115592186294) executed 30 Issuer browser cases; only the exact certificate-evidence case failed. It observed cancellation in both public request and navigation channels without certificate-specific evidence. Generic cancellation does not satisfy the unchanged certificate oracle.

The previous single local diagnostic passed with both trusted HTTP 200 positives and native certificate rejection. It did not reproduce or explain CI. Its raw TRX SHA-256 is `3a24f092020513f9b33421dba5e372071b7b64d5905b7dde6868cd53e473c5e2`; sanitized observer SHA-256 is `1633e899d32429a8fd89f9f410905c443ec4baea35e44d2da7b42dc0c68a34d1`. Source, shared driver and temporary runtime cleanup were independently verified.

| Boundary | Failed hosted run | Prior local diagnostic | Proposed single hosted case |
| --- | --- | --- | --- |
| OS | Ubuntu 24.04.5; image 20261004.327.1 | Ubuntu 24.04.4 in an official Playwright filesystem | Current `ubuntu-24.04`; record actual image |
| .NET | SDK 10.0.402; app runtime 10.0.13 | SDK 10.0.401 | Original `global.json`/setup recipe; record actual versions |
| Collector | dotnet-coverage 18.12.0 on .NET 8.0.31 | No coverage wrapper | Same pinned tool and unchanged `eng/coverage.xml` |
| Cohort | 30 cases; certificate case was 20th | One case, fresh fixture | One case, fresh fixture |
| Browser | Playwright 1.63.0; WebKit 2359 | Same pinned versions | Same package and revision; retain actual file hashes |

The omitted 29 cases include 19 that preceded the historical failure and shared its issuer fixture. Within-assembly parallelism is disabled. This proposal does not reproduce that prior fixture history, the historical hosted machine, or all surrounding job load. A pass cannot exclude cohort-dependent behavior. No additional cohort, retry, second provider or fault injection is added automatically.

## Concrete workflow and source boundary

Use a separate branch named `diagnostic/webkit-ci-provenance`, based on the frozen PR source. Replace only that branch's `.github/workflows/verify.yml` with the reviewed dispatch-only proposal, and place the packet under `diagnostics/webkit-ci/`. Do not open a merge PR or merge/cherry-pick this diagnostic workflow. Main and PR #50's workflow and required checks remain unchanged.

The existing default-branch `verify.yml` already registers `workflow_dispatch`. GitHub can dispatch that registered filename at the diagnostic branch. The proposal contains exactly one diagnostic job, no push/PR/schedule triggers, read-only repository permissions and a distinct concurrency/check/artifact name. It does not emit `Verification acceptance` or `foundation-*` artifacts.

Before checkout, the job requires the exact repository, branch, manual event, first run attempt, GitHub-hosted runner and independently supplied full diagnostic commit SHA. It checks out that diagnostic commit and the fixed source commit separately. The latter is built from locked dependencies and must retain all 1,105 tracked file hashes. The two checkout identities are intentionally different.

The run-attempt guard prevents rerunning an existing run. It does **not** prevent a second fresh dispatch. Record the one intended run ID; if acknowledgement is uncertain, inspect matching runs before taking any further action. Never blindly resubmit a dispatch.

## Execution, evidence and stopping rules

The exact selector is `Provider=PostgreSql&Category=Browser&FullyQualifiedName=IdentityPlatform.Issuer.Tests.Features.Browser.LoginBrowserTests.Given_ExplicitBrowserTrust_When_UnrelatedCertificateIsPresented_Then_CertificateValidationRejectsIt`. Native discovery must retain the complete 644-case Issuer catalogue and select exactly this one identity. Both trusted navigations remain part of the same unchanged case.

The packet copies the pinned driver privately, checks its original SHA-256 `549070af3acabb3efcc4f55bfe6210f9f7c2fcf633cf7eaa59bfe60719969171`, applies the same ten reviewed additive hooks and verifies reversal. The observer and strict native/observer validator are byte-identical to the reviewed local versions. No shared package, source assertion, certificate predicate, trust recipe, 20-second navigation timeout or two-second evidence wait is changed. The private browser-cache location is new; browser payloads are pinned and hashed.

The native invocation retains its 660-second bound. The collector has a separate 720-second outer bound; owned cleanup waits are individually bounded at ten seconds, not ten seconds in aggregate. The overall job retains 20 minutes, so slow setup or cancellation can leave incomplete evidence. Missing, malformed, overflowing or incomplete evidence is a failed diagnostic; it cannot justify extending timeouts or repeating the case automatically.

The native MTP exit, covered-child exit and collector exit are recorded separately. All must be zero, the native report must pass, diagnostic evidence must be complete and restoration/cleanup must succeed for the diagnostic job to be green. A native failure stays red even when its diagnostic observations are complete. `acceptance` remains false in every disposition. An unexpected successful untrusted response stops the experiment and supplies no acceptance.

Only eleven explicit sanitized JSON paths are uploaded, under a distinct diagnostic artifact with three-day retention. The original failure is projected through the exact finite C# expiry format and fixed helper-frame flags; unknown messages remain unknown. Raw TRX, stdout/stderr, coverage output, protocol payloads, screenshots, raw environment dumps, unapproved environment values, URLs and certificate/key stores stay private in runner-temporary storage. The raw TRX hash binds the validated bytes but cannot reconstruct that report after runner teardown; the retained projection is intentionally limited and is not a claim of full raw-report archival.

Cleanup verifies source and shared-cache hashes, restores the private driver, removes its observer and reconciles owned process groups. Docker must be empty before the sole test; only Testcontainers-labelled resources on that dedicated runner are eligible for cleanup. Unlabelled resources, identity mismatches or missing process journals remain incomplete. No daemon prune or cleanup of unrelated resources is allowed. A missing final cleanup receipt after forced cancellation is not success.

| Requirement | Diagnostic obligation |
| --- | --- |
| NFR-08/11/14 | Preserve strict TLS checks, native identity, bounded execution, privacy and actual evidence |
| ENG-01/09 | Keep ordinary CI and final acceptance unchanged; independently review diagnostic behavior and controls |
| Feature 009 T020 | Gather evidence about its current CI blocker without marking the refactor delivered |

## Review and launch status

Preparation runs only pure observer/evidence/outcome/process/restoration/failure-projection controls; it does not execute a browser or database. Independent AI architecture/test and security source reviews pass with no remaining findings in this diagnostic scope. All 86 pure controls pass: 44 observer, 19 evidence, 6 outcome, 6 process, 5 cleanup and 6 failure-projection controls. Python syntax and dispatch/source/artifact workflow boundaries were also checked. Review corrections cover native failure propagation, guaranteed post-spawn cleanup, independent journal/restoration attempts, immutable TRX hashing and finite original-failure projection. Passing pure controls establish those controls only; the CI port is not yet runtime-validated.

The initial published proposal `70296beb53b44a2b4d5a3d56afba70ec3352d07d` produced a failed GitHub workflow-definition check [38705200976](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38705200976), with **zero jobs**. It used the `runner` expression context in job-level environment declarations, where GitHub does not allow it. The correction assigns those temporary paths in the guarded first step through `GITHUB_ENV`. Actionlint 1.7.7 confirms the corrected workflow passes Actions expression/schema validation (`-shellcheck='' -pyflakes=''`; shell/Python linters disabled, Python syntax checked separately). Its archive SHA-256 was verified as `023070a287cd8cccd71515fedc843f1985bf96c436b7effaecce67290e7e0757`. This failed definition check remains recorded; no diagnostic test ran and it is not acceptance evidence.

The current connector exposes workflow reruns but no workflow-dispatch operation. Its generic fetch rejects workflow metadata endpoints. A read-only `gh api .../actions/workflows` request returned `Forbidden`; that does not establish whether an authenticated write would be authorized. No dispatch, remote CI retry or browser execution was attempted. Do not substitute a full-workflow rerun for the unavailable focused dispatch.

After verifying the published diagnostic SHA, the intended manual launch targets the existing `verify.yml` filename with branch `diagnostic/webkit-ci-provenance` and input `expected_diagnostic_sha` equal to that full SHA. In a separately working GitHub CLI session this is `gh workflow run verify.yml --repo mosesparadise/OpenIdentityAdmin --ref diagnostic/webkit-ci-provenance -f expected_diagnostic_sha=FULL_REVIEWED_DIAGNOSTIC_SHA`; the equivalent GitHub Actions UI selects that branch and supplies the same input. This is a launch recipe, not a record that it has run.

After the one hosted observation, reconcile native/child/collector/cleanup outcomes and sanitized artifacts against the run, source and diagnostic SHA. A fresh cancellation remains evidence for further diagnosis, not a causal fix. A pass remains nonreproduction. Any production correction, full acceptance rerun or merge remains a separate, evidence-based step under current authority.
