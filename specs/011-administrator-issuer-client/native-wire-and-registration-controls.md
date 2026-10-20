# Native wire bindings, authorization state and registration drift

Scope: feature 011 T005–T009 and required T018/B2-REG-02/B2-PROOF-01–03 dependencies, FR-004–009/011 and SC-001–003/005. These are native HTTP-pipeline controls over the dormant implementation, with genuine enrollment/cookies and independently constructed issuer service roots. They do not activate product authentication or claim OS-process restart/network transport.

## Verified preceding source

Published `fc9155d7149f693755bb7770f34ecb5ed2890cc1`, tree `ee74ed42c2f353f79dc8aeaadebc3a9b403fb07d`, [run 39487787829](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/39487787829), tested merge `e1c71faff5fcc0e627bf26b8860898e6d406aa59`: independent raw review verifies **80/80 admission cases on each provider**, preserving all 148 preceding identities and passing all twelve peer/recovery/mutation additions. Unit results pass **1,221/1,221**, including 1,126 Host controls.

Both new protocol mutation artifacts independently match their source/test identities and original/mutated hashes. Each provider has **three demonstrated kills, 3/3 passing baseline controls and 3/3 passing restored controls**, with source restoration and zero skips. The published verifier enforces exact bypass markers, matching test stacks and complete reports; the retained artifacts are sanitized `evidence.json` receipts, not the underlying mutation TRX. Independent review does not claim access to unretained raw reports.

Both provider foundation artifacts pass 23/23, making the five core artifacts **1,427/1,427 passing executions** and preserving all 1,415 preceding identities plus twelve additions. The four standard browser artifacts execute 120 cases: **119 pass and PostgreSQL WebKit certificate-negative fails**. The original two-second evidence bound sees an exact-target request cancellation at 12 ms and navigation cancellation at 14 ms, with no certificate-specific rejection signal; page/browser remain alive. Relevant observation source is byte-identical to the preceding failures. PostgreSQL WebKit administrator Security passes all five cases, including the previously failed contrast journey; that does not identify or close the prior contrast cause. The run finishes with **64 successful jobs, two failures** (that certificate job and acceptance) and skipped aggregate coverage. The ten reviewed raw artifacts contain **1,552 executions: 1,551 pass, one fails, zero skip**; mutation receipts are counted separately. No full-CI acceptance is claimed.

## Twenty-six new provider cases

`NativeWireBindingTests` adds twelve cases. Each begins with valid native registration and real artifacts; only the selected HTTP parameter changes.

| Boundary | Deliberate change | Exact expected response and continuation |
| --- | --- | --- |
| PAR | Foreign form client ID | `invalid_client` / 401 from the transport guard; no credential rows. This is form client identity evidence, not a signed foreign assertion-subject test. |
| PAR | Unregistered callback, omitted fixed administrator scope, or `plain` instead of S256 | `invalid_request` / 400; no credential rows; a new valid flow with a fresh assertion succeeds. |
| Token | Different nonempty callback or valid-length incorrect verifier | `invalid_grant` / 400; no credential rows; the same code remains Valid and succeeds with correct parameters and a fresh assertion. |

The code remains usable for those malformed exchanges because native binding validation precedes SignIn redemption. The tests do not assume every malformed request consumes its artifact. Successful controls require native access-token validation, independent ID-token signature/nonce/hash validation, original subject/authorization, redeemed code and exact persisted row deltas.

`NativeAuthorizationStateTests` adds four cases. One uses native authorization-manager revocation in the owned transaction, preserving the code's authorization FK. The other detaches that nullable FK and deletes the authorization in one serialized test-owned transaction, respecting relational constraints. Independent reads verify the resulting state. Hashes preserve the genuine code payload/reference/status/subject/type/dates/application before and after intervention and denial. Both require exact `invalid_grant`, no credential creation and an unrelated genuine code that still exchanges successfully.

The orphan case has a precise limit: native `RestoreTokenEntryProperties` restores authorization metadata from the row and clears the principal's authorization ID when its FK is null. The missing-binding guard then rejects before a native authorization lookup. This proves orphaned-binding denial with unchanged protected payload; it does not claim a lookup of a still-bound nonexistent authorization.

`NativeRegistrationDriftTests` adds eight cases. After the peer successfully processes real PAR and authorization, a separate owned transaction disables the profile, changes its key fingerprint, changes the application's callback through the native manager, or changes the scope resource through its native manager. Fresh assertion authentication must reread the current manifest/application/scope and return exact `invalid_client` / 401 before code consumption. Credential counts stay unchanged and the original code stays Valid. Independent persisted-policy hashes, excluding only the lock counter, must remain unchanged after denial: live requests cannot repair drift. These fixture interventions are not supported audited operator commands or registration-update authorization.

Two additional `NativeIssuerReplayTests` cases consume a genuine PAR reference on one root and replay it on the other after a successful token exchange. Native rejection occurs before request-parameter restoration and must remain a local 400 text `invalid_token` response with no redirect/cookie. The PAR remains redeemed; credential rows and the exact issued access-token ID remain valid; a fresh PAR still succeeds. This complements the preceding actual code-replay tests, which require linked access-token revocation and unrelated-grant isolation.

## Verification and limits

Independent architecture/test and security source reviews pass within this scope. Review strengthened PAR replay evidence from a valid-access count to the exact previously validated access-token ID. The reusable exchange oracle returns that already validated principal without relaxing any of its checks. No local database/Docker experiment is included.

Actual native discovery and inventories require **1,207 cases**, preserving all preceding 1,181 identities/types/traits and adding 26. Admission grows to **93 per provider**; ordinary Administrator selections become **538 PostgreSQL / 643 SQL Server**, aggregate provider totals **895 / 1,000**. Host remains 1,126. All existing mutation targets, coverage floors, browser assertions and deadlines remain unchanged. Administrator build, code-quality, unused-import checks and all **207 Python controls** pass. New both-provider execution is pending.

Continue remaining real issuance/state-creation commit/cancellation/cleanup boundaries, actual process-restart and safe-log evidence, then reconcile coherent B2 acceptance. PR #66 stays draft/unmerged. T005–T009, the six historical findings, 48 human rows and all activation/public-release blockers remain open. Firefox stays deferred only through internal B2 under A-007 and must return before B3.
