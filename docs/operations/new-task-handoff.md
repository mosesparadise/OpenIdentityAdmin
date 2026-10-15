# OpenIdentityAdmin new-task handoff

**Current acceptance continuation (15 October 2026):** PR #50 preserves its witness refactor and current-base integration `2581cfac`; required run `38746001120` finished 65 success/four failed/one skipped. All six mutation artifacts reconcile 42 kills and complete controls, but three native startup failures block acceptance. Two expired startup while downloading the pinned PostgreSQL image; the third reached migrations before a separate DCP creation timeout. A bounded image preparation correction now precedes native execution, preserving every existing deadline and security assertion. [Disposition](../../specs/009-engineering-standards/witness-ci-disposition.md) records exact evidence and limits. Current-candidate CI and final review remain required; no squash yet. Further diagnostic launches remain paused. PR #52 separately implements the BFF store and published observer correction `35c3a562`; do not repeat that implementation. All six findings, 48 human rows and activation/public-release blockers remain open.

## Historical planning continuation — before PR #51 merged

The user requested visible roadmap delivery and approved M02-B scope/dependency preparation. Resume [feature 010](../../specs/010-bff-session-foundation/spec.md), its [plan](../../specs/010-bff-session-foundation/plan.md), [tasks](../../specs/010-bff-session-foundation/tasks.md) and [verification](../../specs/010-bff-session-foundation/verification.md). First implementation target: a complete internal durable protected BFF ticket store, including both-provider persistence, separate keys, expiry, concurrency, revocation, audit intent and cleanup. This planning packet does not implement or activate login, proxy, API or React access.

Latest verified main before this packet is PR #49 squash `5f4fce7fe0c7761e6eab7308f2cd38908937af51`; recheck actual remote head and planning PR/merge. PR #50 remains draft/unmerged at `6fcb96216eca0974041d3e8346adc4ce8458789d`; its refactor exists and must not be repeated. [Current CI/diagnostic disposition](../../specs/009-engineering-standards/witness-ci-disposition.md) preserves failed required CI, passing nonreproducing diagnostics and the pause on further native runs. Main's witness allowance expired on 15 October and blocks clean executable delivery until properly retired; no renewal.

M02-B planning is independent of PR #50 source; executable delivery still requires unchanged checks. Existing `bff` native state tables are reusable; the product store and BFF key boundary are missing. Actual administrator login needs a distinct issuer/client integration: the pilot rejects administrator roles/MFA and lacks PAR/refresh/end-session support. Do not broaden the pilot or infer current authority from a stored ticket. Later B2/B3/C/D/E packages remain explicit.

All six findings, 48 human rows and activation/public-release gates remain open. A-004 is expired; A-005/A-006 concluded. Preserve `046-governance-diagnostic-proposal` at `3699718603d90665117e384128803aa5f75ad12e`; the retired WebKit diagnostic branch must not merge. Earlier sections retain historical checkpoints, superseded by verified later delivery records.

## Latest delivered checkpoint

**Latest delivered checkpoint (13 October 2026):** PR [#48](https://github.com/mosesparadise/OpenIdentityAdmin/pull/48) was normally squash-merged as `f4995183453ba194e8136143f3ec6878fb16a7c9` after reviewed head `2e17aca6` passed all 70 jobs in run `38479414481`, all six mutation suites (42 kills with baseline/restored controls), all 55 coverage inputs and nine module floors. Feature 008 T020/T021 and correction F005 are complete for internal development scope. All six historical findings, 48 human rows and activation/public-release gates remain open. Earlier correction/hold notices below describe prior checkpoints. [Final evidence and remaining work](../../specs/008-administrator-foundation/governance-delivery.md).

Resume from current remote main after verifying its actual head; the merged checkpoint is `f499518`. Preserve diagnostic branch `046-governance-diagnostic-proposal` at `3699718` and the historical receipts. Do not repeat completed SQL/network/implementation work. The next bounded engineering candidate is the exact expiring witness-method exception under feature 009 T015; human assessment remains with the user.

**Historical full-CI follow-up checkpoint:** the first correction run exposed a stale shared inventory assertion and newly reported Pkcs advisories. Provider totals are now independently checked as 790/895; Pkcs 10.0.13 replaces both vulnerable resolved versions across all 18 affected lock files. Local locked build, 32 credential controls, 182 Python controls, current 23-project advisory and 233-pair license checks pass. Final exact-head CI remains required; no historical or human/release finding is closed.

## Historical correction checkpoint before full CI — 13 October 2026

Read the [current correction record](../../specs/008-administrator-foundation/governance-failure-corrections.md) and latest [approval](../approval.md), [roadmap](../roadmap.md) and [tasks](../../specs/008-administrator-foundation/tasks.md) before the historical handoff below. The user subsequently requested requirements review and conditional squash of PR48, then explicitly requested fixing its failed checks. Corrections and necessary verification proceed under those current instructions; A-004 remains expired and A-005/A-006 concluded.

The current managed runtime applies the two Microsoft regional host entries and has the exact pinned SQL/PostgreSQL images. The guarded SQL privilege oracle passes with raw1088 retained, and the populated native-user fixture makes the omitted-witness mutant reach its exact unsafe-commit assertion. SQL baseline/restored controls and PostgreSQL controls pass 2/2 each. All 916 prior portable identities remain unchanged; 105 SQL-only pure controls give 1,021 total. Independent source/evidence/integration reviews pass. The initial test correction changed no production logic and temporary mutations/observers were restored; the subsequent Pkcs production dependency correction is recorded separately.

The reviewed diagnostic work is deliberately integrated with the correction for PR48, preserving the separate handoff branch and all failed receipts. Recheck the actual PR head and full CI/review state; this file cannot self-certify its future commit. PR48 remains draft/unmerged at this checkpoint. Next: exact-head full checks, all required mutation artifacts, 55 coverage inputs/nine floors, and delivery review before the conditional squash. All six findings and 48 human `not_run` rows remain open; no management activation, production correction or external release is authorized.

## Historical handoff at 3699718

Prepared 13 October 2026 (Europe/London). This is an operational context transfer, not a verbatim transcript or a new implementation authorization. A new task must verify current repository/environment state and later user instructions. It cannot assume access to the previous chat, attachments, credentials, agent sessions or ignored local artifacts.

## Verified source checkpoint

- Repository: https://github.com/mosesparadise/OpenIdentityAdmin
- Current diagnostic branch: `046-governance-diagnostic-proposal`.
- Source checkpoint before this handoff: `4247c80b363d379383dbbb1603168c433b86b124`, verified against the GitHub branch. Working tree was clean; all authored implementation changes were committed.
- [PR48](https://github.com/mosesparadise/OpenIdentityAdmin/pull/48): open, draft, unmerged. Title: `feat(identity): implement internal administrator governance`.
- Frozen PR head: `f78f1962dc6c0c4eb403804b94e2905714b813e8`, branch `045-administrator-governance`.
- Observed main/base: `a4c2ea206593830defe5406106b46eda492598a4`; recheck for subsequent changes.

The diagnostic branch descends from the frozen PR head and contains its unmerged governance work. Diagnostic changes intentionally have no PR. Do not merge this branch or automatically transfer changes to PR48. Checking out only main loses sight of the current unmerged work.

Published checkpoints: `efa2aea` A-005 diagnostic source; `5b43fdd` A-005 disposition/A-006 proposal; `86942ff` reviewed A-006 instrumentation/readiness disposition; `b866e2f` user-requested failed acquisition retry; `4247c80` registry/proxy diagnosis and exact-host handoff.

## Read first

1. [AGENTS.md](../../AGENTS.md), [.specify/memory/constitution.md](../../.specify/memory/constitution.md), [approval](../approval.md) and [roadmap](../roadmap.md). Latest notices govern; older sections retain historical statements.
2. [Approved v0.2 package](../approved-v0.2/), including the PRD, architecture, protocol/security, verification, Spec Kit, design system and assurance plans.
3. [Feature008 tasks](../../specs/008-administrator-foundation/tasks.md), its plan, governance-design.md, governance-implementation.md and governance-verification.md.
4. [A-005 diagnostic results](../../specs/008-administrator-foundation/governance-diagnostics.md) and [A-006 readiness/investigation](../../specs/008-administrator-foundation/sql-diagnostic-readiness.md), including their `evidence/a005` and `evidence/a006` receipts.
5. [A-004](../amendments/A-004-governance-sequencing.md), [A-005](../amendments/A-005-governance-diagnostics.md) and [A-006](../amendments/A-006-sql-diagnostic-readiness.md).
6. [Acceptance reconciliation](../../specs/008-administrator-foundation/acceptance-reconciliation.md), [user-owned manual handoff](administrator-manual-handoff.md), and [separate engineering debt tasks](../../specs/009-engineering-standards/tasks.md).

## Product and engineering decisions

Greenfield OpenIddict-based alternative to skoruba/Duende.IdentityServer.Admin; no Duende migration scope. React/TypeScript administration behind Admin BFF cookies, with no browser-held OAuth tokens. STS Razor Pages; Minimal APIs for application protocol decisions, OpenIddict for protocol validation/cryptography. Both PostgreSQL and SQL Server required. Aspire local/distributed testing. Modern enterprise slate/indigo light/dark theme. Scalar and Swagger UI, versioned management APIs, compatible current OpenAPI tooling, appsettings-backed validated presentation configuration. Activation and formal assurance gates remain mandatory.

Features-based vertical slices within grouped projects and solution folders. Preserve approved hierarchy; ask before introducing new project/structural patterns. Thin production Programs with Composition extension methods (`AddConfigurations`, `ConfigureServices`, `ConfigureApplication`), explicit `Features/Setup.cs` and feature-local `Setup.cs`. Preserve native Aspire/Migrator lifecycle, startup failures and coverage behavior.

Use xUnit v3/Microsoft.Testing.Platform. Meaningful unit, real both-provider integration, concurrency/failure, regression, E2E/browser/accessibility, contract and operations coverage. Given–When–Then scenario names. Never weaken timeouts, assertions, mutation oracles, TLS or coverage floors to obtain passing results; no blanket retries.

Principal-engineer standards: clear, maintainable, extensible and testable code, cohesive reuse, no unexplained domain/protocol literals. Review thresholds: 40 logical lines/method and 300/class, with explicit scoped exceptions. Meaningful handwritten XML documentation; safe examples for externally consumed APIs/reusable public classes. Generated EF artifacts excluded; tests do not need example boilerplate. Enforce new/changed code immediately and track exact legacy debt separately; never expand its baseline to hide new violations. OpenAPI presentation comes from appsettings.

Use pinned GitHub Spec Kit and repository AI skills/reviewers. AGENTS requires narrowly scoped independent architecture/test/security reviews as applicable. User prefers autonomous progress within existing authorization, not repeated approval questions. Normal PR delivery uses Conventional Commit titles, resolved reviews, exact-final-head checks and squash merges; never impersonate human approval or bypass protections. The current diagnostic hold overrides earlier generic merge requests. AI review is not human acceptance, independent certification or formal compliance assurance.

## Implemented versus accepted

Merged main includes native persistence and both-provider migrations; grouped projects/thin composition; xUnit v3/MTP/static analysis/coverage/supply-chain gates; Spec Kit/AI governance; health-only default Aspire hosts; durable issuer credentials/encrypted Data Protection; explicitly enabled Development-only first-party login/code-S256 pilot; issuer restart/distribution/key lifecycle work; API contract tooling with authenticated documentation still gated; explicitly enabled administrator bootstrap, TOTP enrollment/password-MFA login, restricted recovery, factor/code replacement and local logout.

Production administrator management APIs, BFF/React access and broader M02-B–D functionality are not implemented/activated. Passkeys remain M05. Full milestone, manual, certification and release acceptance remain incomplete. Read the roadmap for the complete backlog; scoped merged deliverables do not establish whole-increment acceptance.

Feature008 T020/T021 internal governance implementation exists in draft PR48: current persisted permissions, command-bound recent proof, atomic mutation/audit, concurrency and usable/last-administrator preservation. It is unmerged and not accepted. Diagnostic task E005 is **not_run**.

## Approval boundaries and unresolved findings

User approved v0.2 on 10 October 2026. A-001 permits mandatory-PKCE sequencing while Basic OP suite compatibility/certification remains a public-release gate. Other amendment histories remain in approval.md.

A-004 authorized T020/T021 under explicit conditions while retaining four historical findings and human/release blockers. It expired on incomplete mandatory SQL governance mutation evidence. A-005 and A-006 authorized bounded diagnostics only and have concluded. The user separately requested one additional image acquisition (failed), approved registry investigation and repeatedly requested readiness rechecks. Those instructions do not authorize unlimited retries, production corrections, full CI, acceptance or merge. A script's `A-006` label does not renew authorization.

PR48 and all six findings remain open: the four historical incidents concerning Worker startup (ADM-OPS-01), Firefox history evidence (ADM-EVID-01), certificate evidence and mutation-verifier evidence, plus:

- GOV-EVID-01: SQL omitted-witness mutant failed outside the demonstrated bypass assertion; mandatory mutation evidence incomplete.
- GOV-PRIV-01: SQL runtime-principal schema-denial case failed ambiguously; privilege isolation unverified.

The user volunteered for Windows/WSL2 manual assessment; all 48 proposed human case/cell rows remain not_run. Do not invent observations or formal certification. Restoring image-download access cannot itself close historical governance findings.

## Completed diagnostics

A-005 source `efa2aea`: PostgreSQL ordinary 2/2 passed; SQL ordinary 2/2 failed; SQL mutant 1/1 failed, with no demonstrated kill. SQL retained only Setup checkpoint, so exception location/cause is unknown. Source restored byte-for-byte, original failures retained, no acceptance artifact.

A-006 added 23 finite setup labels, safe harness categories, bounded receipt projection, strict SQL readiness/same-source successful-ordinary prerequisites, and seeded controls. Review corrected mixed checkpoint dictionary access and eviction of privilege observations by long setup traces. Projection reserves first observed bypass and latest privilege observation within 32 entries.

Scoped local verification: clean administrator build; 24 pure C# diagnostic tests; 56 Python controls; 17 selection controls; code-quality and secret checks. Native discovery preserved all 908 prior portable identities and added 8 setup rows, total 916. Ordinary inventory 445/provider; Governance.Failures 61/provider. This is not whole-inventory execution or full CI acceptance. Original mutation targets, 55 coverage inputs and 9 module floors unchanged. Four obsolete legacy allowances retired; no baseline expansion. Existing witness-method exception was reviewed with unchanged 15 October 2026 expiry; recheck expiry before future work.

## Current environment blocker, including later chat-only observations

Pinned image:

```text
mcr.microsoft.com/mssql/server@sha256:4402d880dd4c34bfa7d8705e56a86cd6c88da80a1f6bbbe741f999e76264a090
```

Initial one-shot acquisition failed in 10.071s; separately requested retry failed in 10.240s. Safe category Forbidden, image absent at last inspection. No subsequent successful image acquisition or SQL experiment is claimed.

Registry `/v2/` and exact manifest return 200 with verified TLS; hash matches pin. Layer HEAD requests return 200, but bounded GET requests redirect 307 to `westcentralus.data.mcr.microsoft.com` and `centralus.data.mcr.microsoft.com`. Both receive proxy CONNECT 403/curl 56 before origin TLS/HTTP. Both were absent from the effective allowlist during the published investigation. No proxy bypass, TLS weakening, credential/daemon change or image substitution occurred. Daemon logs were unavailable to the task user. This proves a current obstruction, not historical product causation.

After the published investigation, the user supplied an actual Environment settings screenshot: OpenIdentityAdmin, correct GitHub repository, internet enabled, Package managers preset, two additional domains. A separate screenshot listed both exact hostnames but enforcement Unknown. Repeated live checks in this chat reported no custom hosts. A later startup completed; runtime running/connected, revision 833, allowed_hosts empty, enforcement unknown, both probes still CONNECT 403. This suggests saved configuration/runtime propagation or attachment mismatch; do not assume the user's entries are wrong or that the mismatch is resolved.

Latest explicitly requested command:

```sh
curl -I -v --max-time 30 https://westcentralus.data.mcr.microsoft.com
```

Observed 13 October 2026 at 19:55:44 Europe/London (18:55:44 UTC): configured HTTPS proxy `http://proxy:8080`; CONNECT rejected 403, server envoy, curl exit 56. No TLS connection to Microsoft established. This later repeated observation was previously chat-only; the same blocking boundary is already in committed receipts.

Old task's cloud connector exposed status only, no reload/restart/reattach/configuration tool. Do not invent UI controls. A new task may bind a different runtime: inspect fresh policy and actual access. Editing `/etc/codex/network-policy.json` cannot grant access. Network changes require supported environment settings/user review. US hosts came from Microsoft's redirects; do not guess regional replacements as a workaround.

## Resume sequence

1. Read this handoff and linked authority/evidence; inspect fresh PR/branch heads and later user instructions before coding. Do not redo completed work.
2. Attach the intended environment with the two Microsoft hosts. Read cloud runtime guidance, inspect current observed policy/enforcement and, within an active user-requested readiness investigation, test CONNECT through the configured proxy with TLS preserved. This handoff itself grants no execution authority. Registry HEAD alone does not establish layer readiness.
3. If blocked, prepare a support handoff describing the precise saved-settings/runtime discrepancy for the user; submit externally only if explicitly requested. Do not loop identical pulls or bypass policy.
4. If access is restored, establish current bounded execution authority. Concrete next scope: one acquisition of unchanged pinned image if absent, a fresh isolated checkout, one SQL ordinary comparison, conditional omitted-witness mutant only after both cases pass on exact source, unchanged deadlines/oracles and source restoration. Keep PR48 and every blocker open. Do not silently revive concluded amendments or overwrite prior receipts/one-shot claims.
5. Publish the diagnostic disposition. Production corrections, full CI and eventual delivery require their separate decision and unchanged required evidence. No false incident closure.

## Transferring the workspace

Old checkout was `/workspace/work/OpenIdentityAdmin`; new task need not use that path. Fetch/check out the diagnostic branch in a fresh checkout, verifying `4247c80` or inspecting subsequent commits. Preserve existing local user changes; never reset/discard them. SDK pin is .NET 10.0.401 with MTP (`global.json`). Old local setup used `/workspace/work/dotnet-env.sh`, serial builds `-m:1 -nr:false`, Docker 28.4.0; local helpers/installations may not transfer. Restore dependencies from repository pins and use supported environment setup.

Ignored logs/TRX, bin/obj, disposable worktrees and agent sessions are not durable across tasks. Sanitized evidence is committed. Do not transfer raw credentials, signed redirect URLs, connection strings or mutated binaries. Recreate disposable resources when authorized. Original uploaded editorconfig/gitignore references may not transfer; current approved repository files govern unless original-file comparison is requested.

This handoff is sufficient to locate the authoritative decisions and resume the current investigation; retaining every conversational detail requires separately retaining/exporting the original conversation if the client provides that capability.


## Historical engineering continuation — 14 October 2026

On 14 October 2026 (Europe/London), the user approved the recommended retirement of the witness-test size exception. Main was verified at PR #49's squash `5f4fce7fe0c7761e6eab7308f2cd38908937af51`. Feature 009 T020 is the bounded child of T015; [scope and current evidence](../../specs/009-engineering-standards/witness-refactor.md). Verify its actual PR/head/check state before continuing. The refactor extracts preparation and committed-effect checks while preserving native race ownership, cancellation/drain, test identity and the original decisive mutation assertion. Delivery requires both-provider evidence, independent review and full exact-head CI. Do not renew the exception or close historical/human/release gates.
