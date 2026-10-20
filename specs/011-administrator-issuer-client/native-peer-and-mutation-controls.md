# Independent issuer replay, Sessions read recovery and B2 mutation controls

Scope: feature 011 T005–T009 and their T018/T022/T024 dependencies; FR-001/002/005–011/013 and SC-001–006. These controls extend the dormant implementation. They do not activate product BFF authentication or complete B2 acceptance.

## Verified preceding source

Source `5af4c79c996721b71a829a0bb3e4f000ff9fc657`, tree `286f95fb423f5e79408cabc537e6631e50f6afe7`, [run 39484841392](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/39484841392), tested merge `c50983bd322c4296d607c94fe3f4773f2cf8fed7`: independent raw review verifies both admission partitions at **74/74**, preserving all 124 preceding identities and passing all 24 new authority-race cases. Unit execution passes 1,221 cases, including 1,126 Host controls. PostgreSQL foundation passes 23 cases. These four artifacts contain 1,392 executions, all passing with zero skips.

Final reconciliation verifies both foundation artifacts at 23/23 and all four standard browser artifacts at 120/120. The five core artifacts contain **1,415/1,415 passing executions**, retaining 1,391 prior identities plus the 24 additions. PostgreSQL WebKit administrator Security executes five cases, four passing and one failing: dark desktop enrollment contrast reports black text against the dark background in theme/form controls. Independent source/evidence inspection finds `ready=complete`, loaded/enabled stylesheets and healthy body/sibling styles, but missing inherited token/font/scheme values in the theme subtree. It identifies no supported causal correction; a proposed pre-DOMContentLoaded explanation is contradicted by the retained evidence.

The ten reconciled artifacts contain **1,540 executions: 1,539 pass, one fails, zero skip**. The full run finishes with **62 successful jobs, two failures** (that WebKit security job and final acceptance), and **skipped aggregate coverage**. No full-CI acceptance is claimed. The preceding `17fc6a6` WebKit certificate failure and all historical findings remain recorded; a later passing case does not explain an earlier failure.

## Independent issuer roots and real code replay

`JourneyScenario.CreatePeer` and `AdministratorProtocolScenario.CreatePeer` construct a separate service provider and independently import durable issuer credentials over the same leased database, keys and clock. The peer does not reinitialize registration or repair state. `NativeAccountHttp` takes that explicit root and creates the actual request scope from it.

`NativeIssuerReplayTests` executes native PAR on one root, authorization with a genuine shared administrator cookie on the other, and code exchange on the first. It creates two distinct grants. Replaying the target code on the peer with a fresh assertion must return exact `invalid_grant`, create no rows, revoke the linked access-token entry and leave the unrelated grant valid. Original access/ID tokens are cryptographically validated and bound to their native authorization.

This is actual in-process HTTP-pipeline extraction/validation across independent service roots. It is not a separate-process restart or network-transport claim. The HTTPS client tests supply separate transport evidence.

## Actual Sessions runtime read denial and recovery

Four `NativeClientDatabaseFailureTests` cases cover both providers and challenge/callback boundaries. Each first completes a positive native exchange. Fixture-only permission changes then deny SELECT on the disposable Sessions token table to its actual runtime principal. Independent runtime queries and the native failure must both carry PostgreSQL `42501` or SQL Server `229`; arbitrary 503 responses do not count.

Recovery is armed before the first permission change, runs with independent bounded cancellation, and preserves primary and restoration failures. SQL restoration removes the object-level DENY so the existing schema grant applies; PostgreSQL restores its direct table grant. No global principal, database or connection-pool change is made.

Denial must publish no authentication, response payload, location or cookies; send no extra PAR/token request; create no issuer credentials; and preserve the pending state. After actual runtime access is independently verified restored, a capable fresh client root completes once and another root rejects replay without resubmission. Scope is state-read denial/recovery, not every database outage, ambiguous state creation, cleanup failure or OS-process restart.

## Three decisive B2 mutation targets

`eng/administrator-protocol-mutations.json` adds a separate three-target suite while retaining all seven issuer, eight administrator and six governance targets. Two required provider jobs use the existing clean-checkout, bounded build/execution, native-discovery, exact failure-marker, whole-report and restoration-hash verifier. Target paths are restricted to the three reviewed files; CI acceptance requires the new jobs. Baseline and restored controls must pass, and each mutant must fail solely at its demonstrated behavioral-bypass assertion. Setup, cleanup, cancellation, malformed success and unrelated failures are not kills.

| Target | Intervention | Required demonstrated behavior |
| --- | --- | --- |
| Current authority | Omit only the final persisted-permission predicate in `ProtocolAdmissionProof.ReadCurrentAsync` | Initially valid proof and claims both accept after real membership deletion inside the owned transaction; rollback must first restore a usable positive proof. This is final revalidation evidence, not an external writer defeating serialization. |
| Assertion replay | Return the current observation for an already retained digest in `AdministratorAssertionLedger` | The exact signed assertion consumed by PAR issues actual access/ID credentials in a genuine code exchange on the peer, with native access validation, independent ID-token validation, original subject/authorization, redeemed code, exact row deltas and an unchanged single replay row. |
| Original profile binding | Remove only the original code revision comparison in `AdministratorPrincipal.Matches` | A code signed for the verified old revision issues actual valid credentials after a test-owned committed revision advance. All other profile fields stay fixed; signed old/fresh code revisions are checked, and a fresh flow at the new revision must succeed. |

The profile writer is an explicit test intervention under governance-before-profile locking and a five-second linked deadline. It is not a supported audited operator command. The assertion original/restored path requires native `invalid_client` HTTP **401**, unchanged rows and a still-valid code, then proves a fresh assertion can redeem that code. The profile original/restored path requires `invalid_grant` **400**, durable code consumption and no issuance. Failure messages retain only bounded scalars, never credentials or replay-digest collections.

## Verification and remaining obligations

Independent architecture/test and security source reviews requested precise 401 handling, scalar-only failure assertions and direct protected-revision comparisons; those corrections are included. Source review does not substitute for hosted original/mutant/restored execution. No local database or Docker experiment is included.

The new source adds twelve cases: four Sessions read-recovery, two independent-issuer code-replay and six B2 mutation controls. Administrator build succeeds with no warnings/errors; actual native discovery finds **1,181** cases, preserving the preceding 1,169 identities/types/traits. Admission requires **80 per provider**, ordinary Administrator totals **525/630** and aggregate provider totals **882/987**. All **207 Python controls**, code-quality, unused-import and instruction/register checks pass. All 345 assurance rows remain not assessed. Existing test identities, coverage floors, strict browser oracles and deadlines remain unchanged. Hosted execution of these additions, including all original/mutant/restored phases, is pending.

Continue the remaining T018 wire-binding/registration/commit-boundary obligations, T022 actual restart/state-creation/cleanup/log obligations, hosted mutation evidence and coherent B2 acceptance. PR #66 remains draft/unmerged. T005–T009, all six historical findings, 48 human rows and all activation/public-release gates remain open. Firefox remains deferred only for internal B2 under A-007 and must return before B3.
