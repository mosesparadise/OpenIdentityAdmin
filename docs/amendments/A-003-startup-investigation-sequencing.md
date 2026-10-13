# Startup investigation sequencing amendment A-003

Status: **Approved on 13 October 2026 (Europe/London), for PR #35 only, under the conditions below.** The exception expires at that merge or a recurrence, whichever happens first. It does not resolve ADM-OPS-01.

## Approved decision

Permit the normal squash merge of [PR #35](https://github.com/mosesparadise/OpenIdentityAdmin/pull/35), after every exact-final-head required check passes and source findings are resolved, while retaining **ADM-OPS-01** as an unresolved acceptance and public-release blocker. This exception would finish only T029's scoped telemetry/operations handoff. It would not accept the historical failure, close full T029/M02-A acceptance, or authorize subsequent task implementation through the exception.

## Evidence and reason for the exception

PR #35 fixes the missing production subscription for administrator ceremony/cleanup meters, proves exact registration through real-host exporter controls, and supplies administrator operations and human-assessment instructions. Worker startup and administrator authority are unchanged.

At head `cab9dbe`, [run 38337949308](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38337949308) failed the required SQL Server Development AppHost profile during its health wait after 41.958 seconds. Aspire's generic message does not identify the exact unavailable state. The original logs and downloaded TRX/coverage artifact contain no state/exit evidence establishing the cause. No product, harness, runtime or infrastructure explanation is established. [The incident record](../../specs/008-administrator-foundation/operations-evidence.md#adm-ops-01-unresolved-required-profile-incident) preserves the artifact identity and investigation limits.

The follow-up adds bounded failure diagnostics and eight seeded controls without retrying a failed action, extending deadlines, excluding cases, suppressing assertions or changing production Worker behavior. Independent test review accepts this source. Independent architecture review correctly distinguishes source acceptance from disposition of the historical verification failure. A passing new run verifies its own execution; it does not close ADM-OPS-01.

A separate [run 38340018934](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38340018934) passed all six SQL Server AppHost cases but exceeded the 660-second issuer invocation deadline. That run is rejected too. The reviewed correction splits the unchanged 292 issuer cases into fixed 130/162 groups, preserving entire fixture cohorts and proving exact native selections/TRX identities. Every invocation still has a 660-second limit, but the aggregate available budget increases to two invocations per provider. Four independent issuer reports become mandatory, raising coverage inputs from 45 to 49. **This amendment does not waive that timeout, any new failure or missing coverage:** the complete final matrix must pass before a merge decision.

Repository guidance says, “No skipped/flaky required case counts as a clean pass.” Constitution §23 says, “Failing, cancelled or unexpectedly skipped required work MUST block acceptance.” Consequently, ordinary task/merge authorization does not supply this exception. A-002 concerned a different browser finding and does not cover this incident.

## Approved conditions

1. Merge PR #35 only after its exact final head passes all required static, supply-chain, instruction, unit, both-provider, browser, contract, mutation and combined coverage checks. Preserve every deadline, inventory, negative control and all nine module floors. Respect GitHub protections; no self-approval or bypass claim.
2. Retain the failed run and ADM-OPS-01 as unresolved. Do not call the diagnostic follow-up a causal fix, label the failure an infrastructure transient, or count the rejected run as clean acceptance. The exact final run and review are recorded on the PR.
3. Keep original profile assertions and failure diagnostics mandatory. A recurrence immediately blocks the affected delivery; investigate its evidence before any new acceptance decision. No workflow rerun substitutes for investigation, and no security or reliability check is quarantined or made optional.
4. The implementation lead owns follow-up, with independent test/architecture review. Closing ADM-OPS-01 requires the evidence-backed disposition stated in its incident record; nonreproduction alone is insufficient.
5. This exception authorizes only the normal squash merge of PR #35. It expires at that merge or a recurrence, whichever occurs first. It does not authorize a later implementation PR, deployment or external declaration under the same unresolved finding; later sequencing requires its disposition or another explicit decision.
6. Whole T017/T018/T028/T029 and M02-A acceptance, human Windows/password-manager/screen-reader assessment, production/public release and independent assurance/certification remain blocked by their outstanding requirements. No security assurance level or certification is claimed.

## Recommendation and alternative

Recommend approving this narrow sequencing exception **only if the exact final matrix is fully green**: it delivers useful telemetry, operator guidance and tested diagnostics while making the unresolved reliability risk explicit. The alternative is to leave PR #35 open until ADM-OPS-01 has a causal disposition; this is stricter but delays these independent improvements. Neither option treats the historical failure as resolved.

## Approval record

On 13 October 2026 (Europe/London), after the recommendation to approve this exact amendment and merge PR #35 while retaining ADM-OPS-01 as an unresolved release blocker, the requesting user stated: **“Proceed with your recommendation”** This approves only the bounded sequencing decision above. It is not inferred from A-001/A-002 or the earlier general instruction to process three tasks.

Implementation head `4f5cf3fa4a1b7194f66963aac2580dd3cfe62ecf` passed all 62 jobs in [run 38343066101](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38343066101), including 1,880 ordinary .NET cases with no skips, all 49 coverage inputs and nine module floors. Source/document reviews converged and no review threads remained at approval. Recording this decision changes the PR head; its exact final checks and normal squash result are recorded on [PR #35](https://github.com/mosesparadise/OpenIdentityAdmin/pull/35). Approval does not waive those checks or any recurrence.
