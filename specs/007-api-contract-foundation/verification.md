# Verification record

## Current status

Implementation in progress in draft [PR20](https://github.com/mosesparadise/OpenIdentityAdmin/pull/20), based on `0c1de1e`. The user confirmed both foundation-first sequencing and the proposed structure on12October2026. Production docs remain unmapped; no real administrator authentication or full FR-16 acceptance is claimed.

Current local evidence: full solution build passes with no warnings/errors;20 .NET core contract cases pass;34 Node controls cover nonempty inventories, compatibility mutations, native schema validation and malformed tool reports. Actual entry points export repeatable OpenAPI3.1.1 documents with zero management operations, without network/service effects. Runtime UDP/Unix descriptor allocations are counted; connect/bind/listen/send/receive calls are rejected under strace. Generated types/documents reproduce without drift. Process execution reuses the tested process-group cleanup runner; fixture partial construction and nested disposal are handled explicitly.75 npm toolchain entries have reviewed integrity/license identities and zero current npm audit findings. Exact renderer NuGet archives and preserved upstream notices are hash-checked; vendor advisory/component limitations remain separately assessed.

Read-only architecture, API and test reviewers independently confirmed that the bounded foundation can proceed before M02 only with shipped docs unavailable and real authenticated acceptance retained. These are scope recommendations, not implementation approval or runtime evidence. Draft API/test review found two documentation defects: NFR-10 was incorrectly used for coverage, and runtime consumer tests were conditional despite being promised. Both were corrected: NFR-11/NFR-02 now trace coverage/provider obligations, and a test-only generated consumer must execute against the synthetic HTTP fixture. The later plan must explicitly cover ready-host 404 controls across Development/Staging/Production and npm/bundled-asset supply-chain checks; those obligations are now in test-strategy.md. Design convergence is recorded below; final runtime evidence remains pending.

## Prior delivery clarification

PR #19 final Actions run `38211833498` passed all 25 checks, 1,149 ordinary cases, 24 TRX reports and 17 coverage inputs. AuthServer line/branch coverage was 96.3%/98.6%; critical mutation and browser gates passed. Final head `505b704d5804621091cb3e6c7b3ba4f57b1966b7`, tested merge `7b978b381d761350e18edf76c2e5b1aa0db66cc6`, and squash `0c1de1e8b5dbee5f53d0efff7814fdc03dfd6e93` shared tree `14a8f0c7ac1488eb6909491bcfb5637deed7caa8`. These are prior M01-E results, not M01-F evidence.

Final SQL issuer report time 653.141s leaves little room below the unchanged 660s limit. Keep contract tooling separate and track the performance concern without treating a larger deadline or blanket retries as a fix.

## Remaining work

Complete browser/schema/security acceptance, vendor advisory dispositions, source-review follow-up and full exact-head CI; resolve findings and normally squash only after required checks and review state allow it. Full protected docs and real resource acceptance remain M02/M04 follow-through.

## Design convergence

User confirmed foundation-first sequencing and proposed structure on12October2026. Independent architecture/test reviewers passed the completed bounded design with stale-status wording corrections, now fixed. Scalar anonymous-asset hazard is recorded explicitly. Spec Kit prerequisite analysis found all design artifacts, all10 CONTRACT requirements map to cases/tasks, all3 stories have independent tests, no unresolved clarification or constitutional conflict remains. Requirements checklist passes; no execution result is inferred. No extension hooks are configured.

## Implementation review and interim CI

Architecture source review passed the bounded composition boundary: Programs, Composition and normal storage validation remain unchanged; production rootSetup maps only health; documentation and representative routes are fixture-only. Unneeded renderer references were removed from Admin.Api; rendering belongs to Admin.Bff.

API/security/test review findings addressed include POST antiforgery inventory, empty-BFF bearer rejection, fixed native validator configuration/local-only resolution, exact Git-baseline comparison, malformed diff-report rejection, exact Node identities/counters, strict MTP identities/classification, owned process-tree cleanup, broader socket-effect auditing and fixture failure cleanup. Viewer tests now check both document identities and all three representative operations, retain synthetic captures, and inspect CSP violations and axe findings without suppressions. Final review/acceptance remains pending.

Interim run120 (`38224445792`, head18e6328) passed Contracts core but revealed a stale existing provider selection assertion (300→302 after two additional environment cases/provider), Swagger contrast/semantic defects and Scalar CSP/prefetch findings. These were fixed rather than counted as acceptance. Run122 (`38226480962`, headf301a75) confirmed Scalar light/dark and generated-consumer cases in all three browsers; Swagger retained an invalid landmark-role finding, corrected in the next candidate. Zod's documented pre-import `jitless` option preserves the strict CSP; the real Scalar selector avoids aborting its background document fetch. Full merged acceptance is not inferred from these interim results.

## Embedded renderer supply-chain boundary

Independent security review accepts the health-only foundation posture, conditional on retaining T015 as partial and vendor inventory/advisory/notice resolution as a mandatory production-documentation activation and public-release gate. The candidate npm bulk scan is not clean: it reported8 advisories, including two high nanoid findings. Inspection of the exact Scalar browser bytes supports bounded not-affected rationales for the high findings (the affected non-secure/Node implementations are absent), plus specific tree-shaken Scalar findings; actual Swagger DOMPurify configuration supports two specific non-applicability findings. None is a blanket package exemption.

Swagger's published map does not consistently match its bundle, so the inferred component inventory is provisional, beyond merely four unresolved versions. Two candidate qs advisories remain unresolved. See [dependency boundaries](../../eng/contracts/README.md) and the archived reviewer records. Archive hashes prove stable bytes, not complete SBOM/advisory/legal clearance. Current production hosts do not map either viewer; no vulnerable viewer path is exposed through those hosts. M02/M04 authentication alone will not satisfy the vendor activation gate.

Final API/test source review found two additional tooling defects: raw reference headers could bypass stricter precondition comparison, and arbitrary tool exceptions could count as mutation kills. Unsupported referenced/path-level header forms now fail closed (supported inline forms retain unchanged controls), with seeded regression cases. Mutations require a dedicated ContractViolation from semantic validation or well-formed diff findings; transport, timeout and malformed/contradictory report errors cannot satisfy them. The generator validates local-only schemas before generation or output writes.
