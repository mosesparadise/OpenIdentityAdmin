# Documentation approval

On 10 October 2026, the requesting user stated: **“Version 0.2 is approved.”**

This approves the complete ten-document v0.2 package and its AI skills/subagents, without amendments, and authorizes implementation through GitHub Spec Kit. The archive checksum is `25b113118d0430d44749513f5bef606e787a7f3e1e61c4d9c77937a0bc9acbe5`. The documents under approved-v0.2 preserve the original snapshot wording; this record supersedes their awaiting-approval status.

Production deployment, external certification publication/payment and legal declarations remain separately authorized actions. The mandatory-PKCE/Basic OP compatibility gate is not waived.

Amendment A-001 was approved on 2026-10-10: implementation proceeds with mandatory PKCE; Basic OP suite compatibility is tracked separately and remains a public-release certification dependency. Target repository: https://github.com/mosesparadise/OpenIdentityAdmin.

Amendment [A-002](amendments/A-002-browser-investigation-sequencing.md) was approved on 2026-10-11: **“I approve A-002: merge PR #10 and complete T028 under the documented conditions, keeping T027 unresolved and release-blocking.”** This permits the reviewed PR #10 squash merge after exact-head gates and T028 through its own reviewed squash merge. It expires immediately after T028 merges or on recurrence of the contrast finding, whichever occurs first. T027 remains incomplete and release-blocking; no existing test, coverage floor, deadline, external gate or certification criterion is waived.

A-002 delivery references: [PR #10](https://github.com/mosesparadise/OpenIdentityAdmin/pull/10) squash-merged as `3310d4b`; [PR #11](https://github.com/mosesparadise/OpenIdentityAdmin/pull/11) delivers T028 and records its exact-final-head checks and squash status. The exception is exhausted immediately when PR #11 merges. T027 remains unresolved and release-blocking; proceeding to any subsequent task requires its evidence-based disposition or a separate explicit extension.

**A-002 expired on recurrence before T028 merged.** SQL Server/WebKit reproduced the contrast finding on PR #11 head `7b6b936` in [run 38129076793](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38129076793). PR #10 remains merged; PR #11/T028 acceptance and merge are blocked. The [captured evidence and investigation limits](../specs/004-first-party-login/verification.md#a-002-expiry-and-captured-contrast-recurrence) retain T027 as unresolved and release-blocking. No later progression occurs under this expired exception.
