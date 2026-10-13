# SQL diagnostic readiness amendment A-006

Status: **APPROVED, now CONCLUDED with SQL execution blocked — 13 October 2026 (Europe/London).** The user explicitly approved: “I approve A-006 for bounded SQL diagnostic readiness and the specified experiments only, keeping PR #48 unmerged and all acceptance/public-release blockers open.” [A-005's bounded execution](../../specs/008-administrator-foundation/governance-diagnostics.md) has concluded with SQL failures lacking post-Setup observations, not a diagnosis of the original governance failures. A-004 remains expired; PR48 and T020/T021 delivery remain halted.

## Recommended decision

Authorize one bounded preparation/investigation increment that establishes the SQL diagnostic prerequisites and distinguishes safe fixture-setup phases before attempting the affected tests again. This is not approval of a governance correction or merge.

On diagnostic source `efa2aea`, PostgreSQL passed both ordinary cases. SQL ordinary failed 2/2 and SQL mutant failed 1/1, each retaining only the initial witness Setup checkpoint and an unknown exception-token category. Neither SQL attempt retained the observations needed to diagnose GOV-EVID-01/GOV-PRIV-01. A subsequent read-only Docker inspection found the exact pinned SQL image absent. Image absence is a concrete prerequisite gap, not causal proof of either local failure or the original GitHub failures.

## Authorized scope

1. Verify the managed local Docker daemon and current network/CA policy. Attempt to acquire the repository's exact pinned `mcr.microsoft.com/mssql/server@sha256:4402d880dd4c34bfa7d8705e56a86cd6c88da80a1f6bbbe741f999e76264a090` once through the existing approved proxy/TLS path if absent. Record only bounded outcome/status categories and image digest/availability. Do not change the digest, network allowlist, registry identity, daemon configuration or TLS validation. A denied or unavailable acquisition stops database execution; report the prerequisite instead of retrying or bypassing it.
2. In the existing administrator fixture paths, add only closed setup observations around builder creation/build/start, database readiness, auth readiness, lease/database provisioning, native enrollment and witness/request/code preparation, where source review finds the distinction necessary. Retain all original operations, deadlines, assertions, resource ownership and exception behavior. Preserve primary failure when emitting observations; do not refactor lifecycle behavior or fix an unknown setup issue. Extend the existing safe receipt projection with exact known code-owned harness failure categories and reviewed type-token categories. Never publish raw messages, stacks, dynamic resource names, connection details, SQL, credentials or child output.
3. Add seeded privacy, malformed/unknown outcome, sink-failure and cancellation controls in existing test/tooling locations. Preserve all 892 original native identities and 16 A-005 additions, the original mutation manifests, discovery bindings, 55 coverage inputs and nine module floors. Any added pure diagnostic test rows must be explicitly reviewed and inventoried. Review any retained A-005 method-size exception at this checkpoint rather than silently renewing it.
4. Obtain independent architecture/security/test source review, publish the diagnostic-only revision on the separate branch with no PR, and prepare a fresh disposable checkout. With the pinned image confirmed present, run one SQL ordinary comparison selecting only the same witness and runtime-principal permission methods, using unchanged 180-second execution/build and 60-second discovery ceilings. No repeated identical attempts. A setup failure, incomplete report or unmodified trust/privilege bypass ends the experiment; retain the safe evidence and stop.
5. Only if both ordinary SQL cases pass on that exact revision, run one SQL omitted-witness-lock diagnostic with the original source substitution, discovery identities and strict marker/stack oracle. This remains a single-target diagnostic; it does not replace the mandatory six-target mutation suite or create acceptance evidence. Restore and independently hash-check source on every exit; do not reuse mutated builds as product output.
6. Publish observations and an evidence-based recommendation, then stop. Unknown production fixes, changes to accepted denial codes, changes to security-mutation assertions, full CI, T020/T021 acceptance and PR48 merge require a separate decision. Another passing execution cannot erase retained failures or close an incident without its own causal evidence.

## Unchanged limits

No production logic, schema, permissions, API/BFF/React activation, workflow selection or time-limit changes. No new project or endpoint. PR48 remains draft and unmerged. GOV-EVID-01, GOV-PRIV-01, all four historical findings and human/public-release gates remain open. Runtime experiments remain synthetic, disposable and explicitly diagnostic. Network policy changes and external actions are not authorized by this proposal.

This authorization expires when this bounded disposition is delivered or any scope expansion becomes necessary. It does not reactivate A-004 or grant delivery authority.

## Why this is recommended

The local SQL experiments retained no post-Setup observations about the disputed security boundaries. Repeating governance tests without establishing prerequisites and preserving setup classification is unlikely to provide useful evidence. This increment addresses that precise evidence gap, then permits a single meaningful SQL comparison and a conditional mutant experiment. Holding all execution remains available, but does not resolve the missing evidence.

## Approval wording

> I approve A-006 for bounded SQL diagnostic readiness and the specified experiments only, keeping PR #48 unmerged and all acceptance/public-release blockers open.

## Final disposition

The single permitted image acquisition failed; the exact pinned image remains absent. No SQL ordinary or mutant experiment ran. Independently reviewed instrumentation and seeded controls are documented in [the A-006 disposition](../../specs/008-administrator-foundation/sql-diagnostic-readiness.md). This bounded authorization expires with that publication; all findings, PR48 and acceptance/public-release blockers remain open.
