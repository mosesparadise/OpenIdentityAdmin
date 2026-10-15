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
