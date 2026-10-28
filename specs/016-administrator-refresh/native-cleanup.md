# Native authenticated cleanup characterization

## Candidate boundary — 28 October 2026

This T003 increment continues verified main `82006403626128cf3cd0ff2fc9e42c1b2802a57e` after PR110/111. It adds an **isolated test-only native server/client composition**, not production refresh or B3 cleanup activation. No schema, production registration, quota, browser contract or historical approval changes. All T003–T022 checkboxes remain open pending their complete obligations.

The previous manager-stage tests could not establish authenticated target selection. The new fixture runs complete OpenIddict7.7.2 ASP.NET parsing, native authorization-code issuance, PKCE exchange, refresh-token cryptography and native `OpenIddictClientService.RevokeTokenAsync`. Only human authorization is an explicit synthetic identity seam; it is not fresh password/MFA or B3 admission evidence. A component bridge replaces network I/O while preserving native form serialization, client JWT signing, server cryptographic validation and client response parsing. Discarding a committed token response proves that the cleanup caller needs only its retained intent; it does **not** prove actual TLS connection loss.

## Native event and persistence boundary

- After native `AttachAuthorization`, an owned manager creates one custom-type token entry linking the client-qualified intent reference to the actual authorization. It has no payload and is never accepted as an OAuth credential. Manager reference hashing/indexing supplies lookup. Code, authorization and selector share one real provider transaction. Duplicate binding aborts the entire transaction.
- An exact test-only `token_type_hint` selects the adapter. Immediately after `EvaluateValidatedTokens`, only generic-token extraction/validation flags are disabled for this profile. Native client assertion validation and native endpoint permissions remain active. No generic-token principal is fabricated.
- After `Revocation.ValidateEndpointPermissions`, selection requires canonical32-byte base64url selector/nonce, the authenticated application's metadata ownership, and the actual authorization's application ownership. Missing, foreign, malformed or substituted mappings deny without family mutation.
- The ordinary native token-type/authorized-party/attach/revoke descriptors retain their installed order and filters, with an added exact-selector exclusion. A genuinely issued refresh-token positive control verifies that ordinary target-only revocation still works.
- Selector handling uses native authorization and linked-token managers to deny the family, retaining the metadata tombstone for fresh duplicate cleanup. `ApplyRevocationResponse` stages the exact two-field nonce-bound test confirmation after native response replacement. The component host releases its in-memory result only after real commit and owned scope disposal. This is not the final product confirmation schema or production HTTP publication integration.
- The client uses OpenIddict's own service-created scope and native `private_key_jwt`. The bridge suppresses execution-context flow, passes the real request cancellation into an independent issuer owner, checks the exact destination, disables native HTTP retry mechanisms, and records only finite counts and irreversible proof hashes. Full original-deadline/client-ownership proof remains T004 work.

## Finite inventory and traceability

Native discovery preserves all1,983 previous identities and portable metadata, appending22: **11/provider**, total2,005. The existing SessionRefresh partition becomes27/provider; ordinary administrator totals become PostgreSQL904 and SQL Server1009. Existing95 CI jobs,76 coverage inputs,15 module floors,64 mutation controls and exact A-009 browser deferrals remain unchanged. Complete identities are in `eng/administrator-test-matrix.json`; all classes below use the `IdentityPlatform.Administrator.Tests.Features.SessionRefresh` namespace.

| Class / scenario suffix | Requirements | Decisive evidence |
| --- | --- | --- |
| NativeCleanupTests / DiscardedInitialResponse | REF-INIT/END, FR-001/009/013 | Real initial offline issuance; cleanup only by retained intent; exact authorization and all linked rows revoked; unrelated refresh works; fresh native proof and nonce on duplicate cleanup |
| NativeCleanupTests / UnexchangedCode | REF-END/LOSS, FR-008/009 | Cleanup commits before exchange; genuine native late code exchange denied; no refresh row created |
| NativeCleanupTests / IssuanceRollbackOrDuplicateIntent | REF-INIT/LOSS, FR-001/013 | Exact causal faults and reached counters; no orphan authorization/code/selector; unchanged native identities; restored real exchange |
| NativeCleanupTests / CleanupRollback | REF-END/LOSS, FR-009/013 | Successful native confirmation staged before exact injected rollback; no response escapes; original refresh remains usable; fresh cleanup succeeds |
| NativeCleanupTests / EmptyOrOrdinaryRevocationResponse | REF-END, FR-009 | Empty RFC7009 response and deliberately replaced confirmation remain unconfirmed; committed family denial persists; fresh retry confirms; wrong nonce does not |
| NativeCleanupTests / RealRefreshCredential | REF-END, FR-009 | Ordinary native revocation affects only real refresh target; authorization, access token and metadata remain valid; no private confirmation |
| NativeCleanupAuthenticationTests / InvalidClientProof | REF-AUTH/END, FR-003/009 | Missing proof, wrong key, wrong audience and expired proof denied before mutation; native client positive control |
| NativeCleanupAuthenticationTests / InvalidSelectorOrNonce | REF-END, FR-009 | Unknown/malformed selector and malformed nonce deny; unchanged family and restored positive control |
| NativeCleanupAuthenticationTests / NativePermissionChanges | REF-AUTH/END, FR-003/009 | Missing revocation permission denies; restoring it while removing token permission permits cleanup but denies issuance |
| NativeCleanupAuthenticationTests / ExactClientAssertionReplay | REF-AUTH/END, FR-009/013 | Exact still-current proof accepted twice by native-only crypto; explicitly demonstrates the unimplemented durable replay prerequisite |
| NativeCleanupOwnershipTests / ForeignApplicationOrFamilySubstitution | REF-AUTH/END, FR-003/009 | Distinct registered client/key authenticates but cannot select another client's intent; separately corrupted metadata application/family bindings deny; both families survive; restored owner cleanup works |

Fault tests require the exact injected exception instance in the causal chain, reached-after-dispatch counters and independent committed-state reads. Token IDs are sorted ordinally before comparison. Native rows used for hostile ownership substitution are explicitly synthetic corruption controls; they do not replace genuine code/token issuance in the acceptance cases.

## Candidate verification

Local SDK10.0.403/runtime10.0.14 builds the administrator project and discovers the complete2,005-case catalogue. Real-provider execution and full hosted acceptance are pending; no provider pass is claimed at this candidate checkpoint. The local root filesystem remains nearly full, so no database container or SQL experiment was started locally. Required hosted commands are:

```text
python3 scripts/verify.py PostgreSql --partition administrator --story SessionRefresh
python3 scripts/verify.py SqlServer --partition administrator --story SessionRefresh
```

Architecture and test reviews requested stronger exact-fault causation, actual caller cancellation, ordinary real-token revocation, foreign-client/family negatives and deterministic native identity comparisons. Those controls are included. Independent architecture/security and test source rereviews pass for this bounded component prototype. Final local build, exact2,005-case discovery/metadata agreement,51 selector/browser controls, code-quality rules and assurance integrity pass. Source review also required the issuance rollback witness to distinguish successful code redirects from OAuth error redirects; the witness now checks the exact callback, no error and exactly one nonempty code. Hosted evidence remains pending.

## Remaining integration gates

This prototype deliberately does not satisfy the production cleanup contract. Native crypto accepts assertion replay, so a separately typed durable consumption receipt is mandatory before selector handling, including downstream failure and cross-endpoint replay. Current B3 registration and assertion-ledger readers reject disabled profiles; explicit deny-only cleanup eligibility remains unimplemented. The native-permission test above is not evidence of production disabled-profile cleanup.

The real B3 code issuance budget counts every token and exact creation deltas. This isolated graph does not reserve an additional metadata slot or alter that budget. Integrated original-login/authorization binding, pre-exchange capacity, tombstone retention/pruning, immutable source expiry, genuine fresh password/MFA, actual TLS loss/redirect/cancellation, client child-scope deadlines, separate early/final native refresh epochs and current-authority checks remain blocking T003/T004 dependencies. The custom fields are test-local characterization names, not a new public protocol contract.

All six historical findings,48 human rows,345 not-assessed assurance controls,14 viewer findings, actual Windows014 T014, precise A-009 deferrals and production/public-release gates remain open. Continue the bounded integrated B3 cleanup receipt/admission work after accepting this evidence; do not activate refresh or start dependent T005 schema implementation on this prototype alone.
