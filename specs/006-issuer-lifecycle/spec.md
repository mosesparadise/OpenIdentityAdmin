# Feature Specification: Issuer lifecycle continuity

**Feature Branch**: `014-issuer-lifecycle`  
**Created**: 11 October 2026  
**Status**: Draft under approved M01-E; project structure and PR sequencing await the user's requested clarification. No implementation or acceptance yet.  
**Input**: Proceed after the T021 handoff in PR #16 to M01-E: real issuer restart, two-instance behavior and staged signing/encryption overlap.

## User Scenarios & Testing

### User Story 1 — Restart without losing valid protection (Priority: P1)

An operator restarts the issuer process while retaining its identity, database and protected key material. Previously valid browser/session and protocol state continues within its original lifetime; restarting does not revive invalid state.

**Why this priority**: The current durable-key adapter has component evidence, but real cookie/code/token process continuity remains unproved.

**Independent Test**: Establish genuine protected logins and protocol state, stop the actual issuer process, prove its termination, start a replacement against the same durable resources and verify both success and denial outcomes on each database provider.

**Acceptance Scenarios**:
1. An existing eligible session completes a silent authorization after replacement with the same subject and original authentication time, without password entry or session recreation.
2. A pending protected login form and a completed-but-not-resumed interaction retain their original browser/request binding across replacement; each can continue once. Another browser cannot adopt them.
3. A code issued before shutdown redeems once after replacement while still unexpired; its bound proof/client/redirect remain mandatory. A code redeemed before shutdown remains unusable afterward. Expired codes remain expired.
4. Public tokens issued before shutdown still validate independently with the same issuer and public key set after replacement, only while unexpired. Metadata does not change identity or expose private material.
5. Persisted revoked/expired session state and representative account restrictions remain denied after replacement. Fresh eligible requests can recover where the restriction is deliberately removed through its owning test fixture.
6. Evidence proves real old-process termination and new-process identity, unchanged canonical issuer/resources, bounded recovery and complete teardown; a no-op restart cannot pass.

### User Story 2 — Operate two issuer instances consistently (Priority: P1)

An operator serves one canonical issuer using two actual processes sharing only the required stores and protection material. Requests may move between nodes without weakening browser binding or single-use decisions.

**Why this priority**: Concurrent database contexts inside one process do not establish deployed cross-node behavior.

**Independent Test**: Route genuine requests deliberately to each process under one trusted HTTPS issuer; prove that both nodes actually handled their assigned requests and exercise simultaneous decisions on both database profiles.

**Acceptance Scenarios**:
1. A bound login started on one node completes/resumes on the other, preserving subject, request, antiforgery and session protection.
2. A code issued on one node redeems on the other; simultaneous redemption on different nodes has exactly one winner and later replay is denied with native token-store consequences verified.
3. Committed account/session/client/scope restrictions are respected by the other node at subsequent decision boundaries; no assertion of immediate offline JWT invalidation is made.
4. Stopping one node does not prevent an eligible request from succeeding through the surviving node. Shared state remains authoritative; no automatic retry of an ambiguous security mutation is introduced.
5. Wrong/missing protection identity or key material cannot silently cause a new trusted identity or unauthorized success. The operator receives a safe failure and the known-good topology can recover.

### User Story 3 — Rotate keys with an explicit overlap (Priority: P1)

An operator publishes replacement public signing material before using it, activates replacement signing/encryption credentials deliberately, and retains historical verification/decryption material until affected artifacts expire.

**Why this priority**: Multiple certificates and their registration order alone do not prove safe activation, public-key propagation or retirement.

**Independent Test**: Execute publish, activate, overlap and retire stages with real issuance/validation and both database profiles; record selected key identities and safe failure controls without exporting private material.

**Acceptance Scenarios**:
1. Staged public signing material is visible before it signs tokens; newly issued tokens still use the explicit active key until the activation stage.
2. After activation and process replacement, new tokens use the new signing key while unexpired old tokens remain verifiable through the retained public material.
3. Codes/request artifacts encrypted with the previous key remain decryptable through their permitted lifetime; new issuance uses the intended active encryption key.
4. Premature removal is documented as unsafe and fails the relevant old-artifact control; properly timed retirement occurs only after the applicable maximum lifetimes, clock skew and publication/cache policy are accounted for.
5. Two-node staging/activation does not create inconsistent acceptance. At-rest wrapping-key rollover remains separate from token signing/encryption overlap.

### Edge Cases

Stale Aspire health snapshots; reused process IDs; changed listener targets behind a stable issuer; process failure during restart; 60-second code expiry during orchestration; old and new cookie cohorts; pending antiforgery forms; completed interactions; second redemption and response loss; shared key-ring access; database availability; poisoned or missing key material; public-key cache delay; not-yet-valid/expired certificates; retained decryption material; failed teardown. No retry may turn a denied/replayed security mutation into success.

## Requirements

- **LIFE-001**: Establish and preserve the exact canonical HTTPS issuer, callback, database identity and durable protection resources across a real issuer process replacement. Traces ISS-002/003, FR-15/18, NFR-14.
- **LIFE-002**: Preserve genuine unexpired session, pending form and completed-interaction continuity without substituting a new login for the tested old state. Traces ISS-003/005/008, LOGIN-003/004/005.
- **LIFE-003**: Preserve native code lifetime, proof/client/redirect binding, exactly-once redemption and persisted replay denial across restart and nodes. Traces ISS-003/006/007, LOGIN-002/006, NFR-01/08.
- **LIFE-004**: Independently validate pre-existing public tokens after lifecycle changes using public metadata, while retaining original issuer/audience/nonce/signature/lifetime constraints. Traces ISS-003/010, FR-09/15.
- **LIFE-005**: Invalid persisted eligibility and expired/consumed state remain rejected. Evidence distinguishes committed decision-boundary enforcement from offline-token validation and concurrent administrative ordering. Traces ISS-009, LOGIN-005.
- **LIFE-006**: Prove actual cross-node dispatch, shared protection/store behavior, a single concurrent redemption winner and surviving-node availability. Traces ISS-003, FR-18/24, NFR-14.
- **LIFE-007**: Define explicit publish/activate/overlap/retire semantics for signing and encryption credentials; retain historical material for every affected artifact lifetime. Traces ISS-003, FR-15/18.
- **LIFE-008**: Fail safely on unsuitable protection/topology configuration; never generate runtime fallback keys, relax TLS/PKCE/binding or log sensitive material to recover. Traces ISS-002/003/012, NFR-01/08.
- **LIFE-009**: Execute equivalent lifecycle evidence on actual PostgreSQL and SQL Server, with positive/negative controls, identity-checked test selection, child-process coverage and verified teardown. Traces ISS-012, FR-17/21/24, NFR-11/14.
- **LIFE-010**: Document operator sequencing, observed limits, requirement-to-test evidence, exact revisions and independent review; retain incomplete manual, product, restore and certification obligations explicitly. Traces SC-001/004 in the issuer backlog, FR-18/25, NFR-06/14.

### Key Entities

Existing native accounts/applications/scopes/authorizations/tokens; existing browser interactions, sessions and audit intents; canonical issuer; process-generation evidence; durable protected key ring; active/staged/retiring credential identities; public validation-key cache. New production persistence is not presumed necessary.

## Success Criteria

- **SC-L01**: Every accepted restart scenario has actual old/new process evidence and both-provider results; a no-op process operation cannot meet acceptance.
- **SC-L02**: Every accepted valid artifact survives only within its original validity, while the specified invalid/replayed states never yield unauthorized issuance.
- **SC-L03**: Actual two-node tests prove cross-node continuation and exactly one concurrent code-redemption winner on each provider.
- **SC-L04**: Publish/activation/overlap/retirement evidence identifies which public key protects new and old artifacts, with no private-key disclosure or undocumented retention gap.
- **SC-L05**: Required cases execute without skips or weakened deadlines, test/coverage policies or security checks; cleanup is verified on success and failure.
- **SC-L06**: Each delivered increment has its own reviewed plan, implementation evidence and squash delivery; M01-E remains partial until all three stories pass.

## Assumptions and scope

The approved v0.2 M01-E/ISS-003 backlog authorizes this work. It remains an isolated Development pilot with synthetic identities, native protocol ownership, existing lifetimes and no production deployment. T021's human accessibility/password-manager assessment, M01-F API contracts and wider product/certification obligations remain open. Process restart is not backup restore/RPO/RTO evidence under NFR-06. Two-instance pilot evidence does not establish future BFF refresh serialization or administrator five-second enforcement.

The user requested questions before project/pattern changes. Two decisions are pending: dedicated lifecycle test project versus existing issuer project, and three sequential PRs versus one combined PR. The recommended options are documented in the plan; no elapsed time or preselected choice is treated as the user's answer. Production topology and key-activation configuration will be made concrete and reviewed before their implementation, without inventing operator/deployment approval.
