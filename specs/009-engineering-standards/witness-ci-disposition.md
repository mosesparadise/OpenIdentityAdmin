# Witness refactor: delivery blocker and investigation limit

15 October 2026. [PR #50](https://github.com/mosesparadise/OpenIdentityAdmin/pull/50) implements the witness-test size-exception retirement at `6fcb96216eca0974041d3e8346adc4ce8458789d`. It remains draft/unmerged. Do not repeat the refactor or renew the allowance.

## Required acceptance remains failed

[Run 38483679324](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38483679324), attempt 1, tested merge `2ca984fc335882cdc0205ab6211b666a92108bda` over main `5f4fce7`; its tree equals PR head tree `3fc0fc9304decb48f0e4d01a13fa9018ec465a44`. It concluded with 67 successful jobs, two failed jobs and one skipped job. PostgreSQL/WebKit executed 30 Issuer cases, 29 passing and the certificate-rejection case failing with missing certificate-specific evidence and `navigation=cancelled`. Final acceptance failed; aggregate coverage was skipped. All six mutation jobs passed, recording 42 kills and complete baseline/restored controls. These facts are preserved on the PR; no full acceptance claim follows.

## Subsequent observations

| Observation | Result and limit |
| --- | --- |
| Hosted single case, [38715730653](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38715730653) | 1/1 passed; fresh fixture omitted original preceding cases. [Durable evidence](https://github.com/mosesparadise/OpenIdentityAdmin/blob/d8114ad2af24bd20f0aee3fffb8b82947a3b9d9a/diagnostics/webkit-ci/hosted-result.md). |
| Hosted full PostgreSQL/WebKit cohort, [38719806243](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38719806243) | 30/30 passed; certificate ran 25th rather than 20th, keyboard login after rather than immediately before it. Newer runner image. [Durable evidence](https://github.com/mosesparadise/OpenIdentityAdmin/blob/b600702280b05b6bf82e44a0bbd16f238ff13c2b/diagnostics/webkit-cohort/result.md). |

Both diagnostic launch workflows were retired. No causal source correction is established. These passes cannot replace the failed required run. All six historical findings and human/release gates remain open.

## Bounded next decision

Further native diagnostics are paused while M02-B preparation proceeds. Before any remaining order comparison, prepare a reviewed packet with:

1. A falsifiable question: can the original default test order be reproduced when the exact historical case-sensitive assembly path, configuration path/absence and unique-ID inputs are preserved?
2. The pinned source, native discovery, original 30 identities, protected certificate-session observer, unchanged TLS/oracle/deadlines and restoration requirements.
3. One invocation only. Record actual sequence and native outcome. If order differs, it is not an order-equivalent comparison. If order matches and passes, causation remains unresolved. If it fails, analyze the retained evidence before proposing a specific correction.
4. A stop after that result. No automatic second run, timeout relaxation, forced custom order or full-CI retry. Execution requires current scope/authorization; this document does not launch it.

The delivery target remains accepted source and normal squash, not accumulating green diagnostic receipts. Do not weaken assertions or describe a nonreproduced failure as fixed.

## Effect on other roadmap work

Main still has the allowance expiring 2026-10-15; its validator rejects expiry on or before UTC today. That is a shared executable-delivery dependency even for unrelated code. M02-B planning can progress and independent implementation can be prepared, but an executable merge needs the actual cleanup plus required passing checks. No source is copied from PR #50 by this record, and no gate is waived. [Feature 010](../010-bff-session-foundation/spec.md) is the next bounded roadmap packet.

## Current-base acceptance continuation — 15 October 2026

The user instructed proceeding with resolving PR #52 acceptance dependencies and
conditional squash. PR #50 is updated by merging main `ab281729` into its existing
head `6fcb962`; history and the implemented witness refactor are preserved. Only
the roadmap/handoff introductions conflicted. Their resolution retains main's
later feature-010 packet and this failed-CI disposition, with PR #50's earlier
continuation explicitly historical. No runtime, test, selector, workflow or
allowance content changes relative to the existing PR #50 refactor.

Normal exact-candidate checks are required for this integration. This is not a
relaunch of a diagnostic packet or a claim that the historical certificate
cancellation was corrected. The original failed run remains part of the
evidence; future green checks cannot establish its cause. If the required case
fails again, retain the new evidence and investigate before any further retry.
No forced order, deadline extension, assertion relaxation or exception renewal
is included. Keep draft/unmerged until all applicable checks, artifacts and
independent final review are reconciled.

Local reconciliation checks pass: the mandatory code-quality validator and its
seeded controls against verified base `ab281729`, seven scope-selection controls,
AI instruction consistency and `git diff FETCH_HEAD --check`. All 1,826 legacy
allowances remain byte-for-byte equivalent and the one exception is removed by
the original refactor. Source/test/policy paths are unchanged from PR #50's old
head; this merge requires fresh hosted acceptance but no duplicate local native
diagnostic or mutation run.


## Current-base failures and bounded image preparation correction

[Run 38746001120](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38746001120), attempt 1, tested head `2581cfac2f31b9b11a98796b88058f5290bf4199` through merge `e17e15d1cb704ed6be168bbd90f89ceb63dc476a`. Both have tree `39184462c784256321c5ab82e245734391b37ed6`. Final result: **65 successful jobs, four failed jobs, one skipped coverage job**. Quality checks pass with the exception actually removed; final verification does not pass.

Independent review reconciled all six actual mutation ZIPs: **42 kills, 42 passing baseline controls, 42 passing restored controls**, zero skips. Exact head/merge, source, manifest, mutant hashes and native identities agree. These are verifier receipts; raw mutant TRX is not in the artifacts.

| Suite | Provider | Kills | Artifact | ZIP SHA-256 |
| --- | --- | ---: | --- | --- |
| issuer | PostgreSql | 7 | 11979947197 | `bde2809bc8a9f6b45b6f549372ba14b396627bd666cbe1e52d238942ce49215f` |
| administrator | PostgreSql | 8 | 11978309181 | `3c2a64b950f2d8c5829be1313fd507ca890f440878e4ff56e8164fa9584ee457` |
| governance | PostgreSql | 6 | 11979407272 | `88b15a3631447e25141327cbeca0ebc614c3441f2080c7a7fff6ff22ecff75ad` |
| issuer | SqlServer | 7 | 11979143234 | `b608687617d7ee19436cf8356ac09f9e5b7cd667e61b4b3686ec210852489a86` |
| administrator | SqlServer | 8 | 11980565977 | `1cb80091d9bccc2f8e76124a933747b144e58bb55b360765a57acbd959f5c3b5` |
| governance | SqlServer | 6 | 11979572811 | `e98c7cdd39b9d8f9e1c6290ec98d2d9dc0fec88a313d5ce3ebd91223f8ff8a50` |

Three native cases failed before their security assertions:

- PostgreSQL recovery: 19 executed, 18 passed, one failed, zero skipped. The first host remained in startup while the pinned PostgreSQL image's layer `c8145e79cc2b` was incomplete. At approximately 180 seconds startup cancellation propagated; cleanup contributed to the 210.079-second failed-case duration.
- PostgreSQL issuer boundaries: 162 executed, 161 passed, one failed, zero skipped. The same layer was still incomplete before startup cancellation at approximately 180 seconds. The failed case took 210.227 seconds.
- PostgreSQL/WebKit administrator Accounts: eight executed, seven passed, one failed, zero skipped. Image acquisition finished after approximately 122 seconds; the database became healthy and migration completed. Auth/worker creation subsequently hit a distinct 20-second DCP API timeout waiting for HTTP/2 response headers. This case reached authentication readiness, unlike the two image-download failures.

| Failed job artifact | ID | ZIP SHA-256 |
| --- | --- | --- |
| Recovery | 11978789142 | `0571a454dd6bfb5778dc51bfa9687882d21d432fa5e22c3dc2f0378be2bee47c` |
| Issuer boundaries | 11979078314 | `5b2f7a4efaea4eb8d7d2f92cd813250fbae6759c7c7c88c9a80ec99abd66b2a8` |
| WebKit Accounts | 11979834247 | `4787b5f7adaee03d10d2d2cd4da807af086412d0b0194bf44fa573005ba43664` |

Retained native TRX and job logs support these sequences. They do not explain the registry delay or DCP timeout; artifacts contain no separate daemon/registry/runner-resource telemetry. No certificate-oracle failure or production defect is established by these three receipts.

The correction adds `scripts/prepare_native_image.py` before native provider, browser (including the earlier Firefox history comparison) and mutation invocations. It reads the exact current AppHost tag/digest chains, rejects ambiguous/missing pins and repository overrides, performs one pull bounded to 300 seconds, then verifies the exact local repository digest with a 15-second inspection bound. Failure stops the job before native tests. There is no retry, tag fallback, TLS weakening, container creation or daemon cleanup. The pull limit bounds the Docker CLI; it does not prove instantaneous daemon download quiescence after a client timeout. A failed preparation never starts native tests. Unit, contract and synthetic contrast jobs do not acquire database images.

All existing job, invocation, fixture startup, case and cleanup deadlines remain unchanged. Fresh native container/database creation, migrations, readiness, isolation, startup-cancellation and teardown assertions remain under their existing bounds. The new preparation uses part of the unchanged job budget. It establishes a warm image prerequisite, not cold-download startup performance.

Six seeded pure controls cover exact pins, drift/ambiguity/overrides, provider rejection, pull failure/timeout, inspection failure/mismatch, mandatory workflow ordering and unchanged job bounds. The actual helper has not been run locally because Docker storage is insufficient. Required hosted native acceptance remains outstanding. This preparation change addresses the demonstrated acquisition gap; it is not a proven fix for the separate DCP timeout, PR #52's SQL timeout or historical certificate cancellation. No diagnostic is relaunched and no historical/human/release gate closes.

Local verification passes: six image-preparation controls, seven scope controls, actionlint 1.7.7, AI instruction consistency, diff whitespace and the mandatory code-quality gate with seeded rule controls against main `ab281729`. Independent architecture/test and security implementation reviews pass. No local Docker pull or native test ran for this correction. Hosted exact-candidate acceptance is pending.
