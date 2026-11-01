# Managed native issuance — T012

## Scope and checkpoint

T012 implements native authorization-code and token issuance for the approved managed confidential-web profile. It continues T011 at source base `2103478da0974409005d06bd4f0420288852ce34` on remote branch `123-application-editor`; verified remote main remains `4c9fb24f003ecfdeb4b4b4d438ce7c847f3780fa`. The local recovery worktree uses branch `124-managed-token-issuance` because an older worktree registration retains the remote branch name. Existing worktrees and history are preserved.

**T012 is locally complete.** Final verification passes 310 cases on each real provider and 1,820 Host cases, plus 67 selection controls, with zero final failures or skips. Independent source and raw-evidence reviews pass; [source and receipt hashes](native-issuance-evidence.json) bind the accepted results. This is a component checkpoint, not T013 whole-journey acceptance, T014 create/details UI, full feature acceptance, full CI or a merge. The existing native Aspire UI/Applications list remains available; no custom launch script or refresh activation is introduced.

The approved profile remains authorization code, S256, `client_secret_basic`, exact HTTPS callback and `openid` only. Existing administrator accounts supply a new password/MFA ceremony and explicit consent. Managed tokens carry the subject and necessary native OIDC metadata, never administrator roles, permissions, source-session identity or a management audience. The actual managed access token is rejected by the independently protected Admin API.

## Ownership and transaction sequence

Approved consent redirects to the original issuer-local protected request. Native validation and the actual endpoint passthrough establish a private managed handoff. The endpoint constructs a fresh minimal principal; it does not clone an administrator principal.

The managed response child begins without an issuance transaction. Genuine native validation/replay effects and request/code redemption run in an earlier child transaction. That child commits and actually disposes before a consumption receipt is attached. Valid codes are not redeemed during preliminary validation: the native client secret, redirect and PKCE checks must finish first. Later account denial, native-save failure or audit failure cannot resurrect consumed input. Native code replay durably revokes linked tokens; OpenIddict deliberately preserves the authorization itself. An unrelated real grant’s issued tokens remain valid and unchanged.

After native internal-claim restoration, a separate issuance transaction reacquires governance and the shared account/browser/ceremony locks. It rereads current application revision and credential expiry, original human deadline, native artifact binding, account status/stamp/generations, enrollment and factor state. Exact tracked and independently persisted snapshots protect the source through manager saves, final audit and commit. Native creation reserves the precise deltas: one code and one authorization, or two exchange tokens with no new authorization. Original human expiry never renews; retention covers dependent native artifacts.

Separate managed proofs observe the actual native authorization attachment, token preparation, creation, signing/reference payload and query/JSON formatters. Prepared code/access/ID principals receive type-specific subject, claim, scope, audience, issuer, nonce and expiry checks before signing and during final verification. Native `expires_in` cannot exceed either 300 seconds or the prepared access-token lifetime. The retained native destination map is accepted only for the exact subject-to-access/ID destinations. Restored private metadata remains bound to the original protected code.

Original request, native authentication context, principal, parameters, parent DI scope and response capture identities remain bound across the handoff. Response callbacks finish and output is sealed before the single issuance commit. Publication requires actual commit acknowledgement and transaction/context cleanup, then the original request's final deadline and frozen-output checks. Committed-but-withheld output is distinguished from rollback; no automatic retry is added.

Known managed validation phases are recorded with the existing native-error coordinator. Wrong client credentials produce native JSON 401; invalid verifier/callback/code produces native JSON 400. Invalid cached authorization input uses the native local text error, without an unproved callback redirect. Error fields remain finite and sanitized. Managed callback error redirects are not added.

## Verification map

Control references below identify relevant requirements and threats, not completed assurance assessments. All 345 assurance controls remain `not_assessed`.

| Requirements | Scenarios and tests | Boundary |
| --- | --- | --- |
| FR-002–004/010 | `ManagedTokenHttpTests`, `ManagedTokenFlow`, `ManagedTokenOracle` | Actual protected API creation, native cache, fresh password/MFA/consent, registered callback, Basic/original S256 exchange; public JWKS signature verification, ID recipient/nonce/access hash, exact native row/timestamp bindings, identity-only claims and real Admin API rejection |
| FR-002–006 | `ManagedTokenFailureTests`, `ManagedTokenReplayTests` | Wrong proofs leave input usable; a separate connection observes committed redemption before late save failure; actual audit INSERT denial rolls issuance back; original replay cannot restore input; linked token revocation leaves an unrelated real grant’s issued tokens valid and unchanged |
| FR-004/006/008/010 | `ManagedTokenDriftTests`, `ManagedTokenRaceTests` | Current policy/credential/account/stamp/authentication/factor/enrollment/lockout checks at both phases, exact valid-versus-redeemed outcomes, original expiry during actual provider-observed lock waits and two issuer roots competing for one code |
| FR-002/004/006/012 | `ManagedTokenCapacityTests`, `ManagedTokenRaceEvidence` | Exact final code/authorization/exchange capacity and overflow; no extra token type or owner binding; zero-or-one committed audit reconciles to zero-or-one issued token pair even when replay preempts publication |
| FR-003/004/006 | `ManagedTokenClaimTests`, `ManagedPreparedTests`, `ManagedPrincipalTests` | Actual prepared role/nonce injection, plus independent code/access/ID positive and role/scope/audience/actor policy controls |
| FR-006/008/012 | `ManagedTokenAtomicityTests`, `ManagedTokenPublicationTests` | Completed source/final-save mutations, actual callbacks, commit acknowledgement, disposal and post-cleanup original expiry; exact rollback or committed-row retention with no published credential |
| FR-002/009/012 | `ManagedTokenTransportTests`, `ManagedTokenOwnershipTests`, `ManagedConsumptionTests`, `ManagedTokenResponseTests` | Exact urlencoded framing, multipart/alias/duplicate rejection, same-request foreign parent ownership, pre-redemption input substitutions and actual lifetime metadata bounds |
| FR-012 | Complete affected ManagedApplications, ProtocolAdmission, Accounts, Ceremonies and Host selections | Both real providers; exact prior discovery metadata/order and provider traits; no skipped required case, weakened floor or new legacy allowance |

T010/T011 tests retain their original identities. Approved consent now expects the exact original local native continuation; cancellation remains local and terminal. The stale cached-policy test now expects the bounded native local 400 error made possible by the recorded validation transaction, preserving its no-observation/no-new-artifact assertions. No assertion accepts either an old or a new result indiscriminately.

## Failure provenance and recovery

The environment interruption preserved uncommitted source and both database images but lost the temporary SDK/cache. Recovery restored SDK 10.0.403/runtime 10.0.14, locked packages and native HTTPS trust without changing network policy or bypassing TLS. Remote branch and main were reverified before work resumed. No expired diagnostic approval was reused.

Initial genuine PostgreSQL tests reached consent and real native code issuance but failed exchange with 503. Source review identified native JWT metadata and the retained `oi_cl_dstn` destination map rejected by the initial allowlist. The correction accepts only the native metadata and exact subject map. Further review added original parent/input identities, explicit prepared-principal constraints, protected original nonce/private-metadata binding and completed validation-phase registration for native error publication.

The first successful exchange passed cryptographic claim checks before the test oracle failed because it used response field names rather than native persisted token-type URNs. That oracle was corrected to exact native types. Earlier test attempts also expected 503 instead of native errors, JSON instead of native local authorization text, `invalid_request` instead of native `invalid_token`, or omitted the exact earlier decision observation-watermark commit. Corrections require the specific native response and intended durable delta; they do not ignore arbitrary errors or row mutations.

Build/format/analyzer failures and all rejected runtime receipts are retained separately. The earlier parallel MSBuild child-node termination has no proven cause; it is not described as an out-of-memory failure. Subsequent builds use one MSBuild node and no node reuse. Final review also added the prepared-token lifetime ceiling to response metadata, followed by its own controls and final-source verification. Earlier runs do not establish acceptance of that later change.

## Remaining delivery

The core T013 created-client success, proof denial, code replay, source drift and late-save/audit-failure replay journeys are already exercised by T012 tests and must not be reimplemented. Remaining T013 integration checks should concentrate on the original authorization request after explicit consent cancellation, cached-request replay after successful issuance, and substitution between two genuinely API-created managed clients; final coverage reconciliation precedes checking T013. T014 still owns the visible create/details/one-time-output UI. T007 retained Chromium/WebKit execution remains pending after the documented download-policy failure. No blocked browser download was repeated. Initial Firefox exclusion, A-009 limitations, actual Windows/human acceptance, all six historical findings, vendor/production/public-release gates and disabled refresh remain unchanged. Normal full-feature review, CI and squash remain T022/T023.
