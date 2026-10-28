# Native refresh primitive characterization

## Scope and checkpoint — 28 October 2026

This is the first implementation increment within **T003**, based on merged planning PR109/main `34c5df010df65ac2f2166a525d4028fcd7e86dcd`. T003 remains open. Production grants, endpoints, registration, persistence schema and package versions are unchanged. This suite characterizes installed OpenIddict7.7.2 storage and handler primitives; it does not claim a complete refresh exchange, native client transport, cryptographic interoperability or authenticated family-cleanup endpoint.

The fixture leases existing Aspire PostgreSQL/SQL Server databases, migrates with the normal Migrator and uses the restricted runtime identity. Each operation uses a fresh `ProtocolStorageRequest` and owned service scope; mutations use explicit `ProtocolStorageTransaction` epochs. Observations reload committed state through fresh manager instances. The synthetic principals deliberately enter **after cryptographic validation** and contain only manager-created native row selectors. They are never issued token strings or browser credentials.

`NativeRefreshStages` installs the original native descriptors and filters for `ValidateTokenEntry`, `ValidateAuthorizationEntry`, `RedeemTokenEntry` and `RevokeToken`, using the native dispatcher and real owned EF stores. The original request cancellation token is attached to the native transaction. No claim is made about propagation through native client-created scopes yet.

## Handler and manager boundaries

Tagged upstream source: [protection](https://github.com/openiddict/openiddict-core/blob/7.7.2/src/OpenIddict.Server/OpenIddictServerHandlers.Protection.cs), [sign-in](https://github.com/openiddict/openiddict-core/blob/7.7.2/src/OpenIddict.Server/OpenIddictServerHandlers.cs), [revocation](https://github.com/openiddict/openiddict-core/blob/7.7.2/src/OpenIddict.Server/OpenIddictServerHandlers.Revocation.cs).

- Native protection places `ValidateExpirationDate` after principal validation, then presenter/audience/proof checks, `ValidateTokenEntry`, and `ValidateAuthorizationEntry`. The stage suite dispatches the last two using native descriptor order; expiry is exercised directly with zero skew. Earlier crypto and protocol checks are not bypassed in production or represented as tested here.
- `ValidateTokenEntry` reads native token status through `IOpenIddictTokenManager`. Replay can revoke linked token rows via `RevokeByAuthorizationIdAsync`; this alone does **not** revoke the authorization row. `ValidateAuthorizationEntry` independently consults `IOpenIddictAuthorizationManager`.
- Native `RedeemTokenEntry` consumes the stored authentication notification during sign-in. For refresh, its ignored false return from `TryRedeemAsync` cannot override the owned store's poisoned state. Two independently tracked real EF rows establish the stale-write conflict; a test-only observer requires exactly one concurrency exception for the expected token.
- Native `EvaluateGeneratedTokens` selects refresh for an authorization-code principal carrying `offline_access`. `PrepareRefreshTokenPrincipal` retains the existing expiry only with sliding disabled, and honors a shorter attached lifetime. These flag/principal controls do not establish a cryptographically issued offline grant.
- Revocation `ValidateTokenType` accepts refresh but rejects even a synthetic prevalidated authorization-code principal. `ValidateAuthorizedParty` checks presenters. `RevokeToken` revokes only the selected token through its native manager. `NormalizeErrorResponse` can erase an invalid-token error without any family mutation; an empty response cannot confirm family denial.

The planning packet's scope/audience ambiguity is corrected: the scope is `identity.admin`; the resource/audience is `identity-admin-api`. No runtime value changes.

## Finite test inventory and obligations

Authoritative full identities, parameter types and provider traits are in `eng/administrator-test-matrix.json`. Native discovery preserves all **1,951** prior identities and metadata, adding **32**: 16 per provider, total1,983. Ordinary administrator counts become PostgreSQL893 and SQL Server998; browser identities are unchanged. `SessionRefresh` is required in both ordinary provider partitions and adds two mandatory coverage inputs, 74→76. Existing 15 coverage floors, 64 mutation controls, browser deferrals and deadlines are unchanged. No new product security decision is implemented by this increment.

All classes below are in `IdentityPlatform.Administrator.Tests.Features.SessionRefresh`; the suffixes identify exact `Given_..._When_..._Then_...` native identities in the inventory.

| Class / scenario | Requirement group | Decisive observations and control |
| --- | --- | --- |
| Storage: redeemed strict replay | REF-ROTATE / FR-004 | Both linked tokens revoked, authorization still valid, unrelated family untouched |
| Storage: missing redemption timestamp | REF-ROTATE / FR-004 | Zero leeway accepts; null leeway rejects, with separately observed authorization status |
| Storage: replay rollback | REF-ROTATE / FR-004/013 | Uncommitted replay mutations disappear from a fresh read |
| Storage: explicit family denial | REF-END / FR-009/013 | Manager authorization plus linked-token revocation rolls back, then commits; successor denial changes accordingly |
| Storage: authorization-only denial | REF-AUTH / FR-003/004 | Both tokens remain valid while authorization is revoked; native validation denies this family and accepts unrelated successor |
| Storage: separate epochs | REF-ROTATE / FR-004/013 | Committed redemption survives rollback of a later revocation epoch; subsequent replay rejects |
| Concurrency: stale native row | REF-ROTATE/LOSS / FR-004/013 | Independent winner commits; exact-token EF conflict observed once; native acceptance still cannot commit poisoned owner; fresh redemption control succeeds |
| Concurrency: swallowed revocation fault | REF-LOSS / FR-013 | Interceptor throws only after provider acknowledges a real token UPDATE; native catch rejects; exact owned commit error; rollback and restored success |
| Lifetime: offline selection | REF-INIT / FR-001 | Same principal without/with offline scope changes native refresh flags; access-token flag stays enabled |
| Lifetime: sliding policy | REF-TIME / FR-006 | Fixed original cap versus extended sliding cap, preserved authorization/subject and removed predecessor token ID |
| Lifetime: shorter initial lifetime | REF-TIME / FR-006 | Attached20-minute lifetime overrides native default |
| Lifetime: before/equal/after expiry | REF-TIME / FR-006 | Native zero-skew equality is accepted; strictly later is rejected; product equality guard remains required |
| Revocation: target only | REF-END / FR-009 | Predecessor revoked while authorization/successor remain valid; successor validation succeeds |
| Revocation: token types | REF-END / FR-009 | Refresh positive, prevalidated-code unsupported; no durable mutation |
| Revocation: presenter | REF-END / FR-009 | Bound client positive, foreign client denial; no durable mutation |
| Revocation: empty response | REF-END / FR-009 | Native error normalization empties response while all family rows remain valid |

Fault assertions require reached-boundary observations and exact ownership failure with an uncancelled request, rather than accepting any exception as causal evidence. Synthetic values and database selectors are not logged. The deterministic stale-row test orders two real independent owners; it is not a claim of simultaneous HTTP races or two-process refresh serialization.

## Verification status

Local SDK10.0.403 / runtime10.0.14 build and native discovery pass. Selector controls30/30, browser-matrix controls7/7, assurance integrity and skill configuration checks pass. Static quality initially reported unnamed fixture literals; these are corrected with named values without expanding allowances. Independent architecture/security and test/Aspire source reviews pass after tightening conflict causation, authorization-only denial and cancellation propagation.

The local root filesystem has only about15MiB free. No database container or SQL experiment was started locally; real provider execution belongs to the required hosted jobs. Full CI and exact-head evidence are pending. No provider pass, coverage result or merge is claimed by this candidate record.

Required hosted commands retain the ordinary driver timeout and native TRX/inventory validation:

```text
python3 scripts/verify.py PostgreSql --partition administrator --story SessionRefresh
python3 scripts/verify.py SqlServer --partition administrator --story SessionRefresh
```

## Remaining T003 gate

Before dependent integration, prove genuine initial offline issuance and native cryptographic refresh/revocation transport, actual native client-created scopes and request budgets, authenticated ownership-bound cleanup after total initial token-response loss, pre-exchange capacity reservation, disabled-client cleanup and exact first-party confirmation schema. Complete native early-consumption/final-issuance integration and product authorization-wide replay denial with current authority and immutable deadlines. Manager-only family revocation above proves a storage primitive, not authenticated target selection or remote confirmation.

Native ordinary revocation cannot accept an authorization code or confirm family denial. The BFF cannot decrypt an issuer code or query Platform storage; a guessed selector is not an acceptable cleanup contract. Resolve and review that specific integration dependency before implementing dependent state/activation. T003–T022 remain unchecked. All six historical findings,48 human rows,345 not-assessed controls,14 viewer findings,actual Windows T014,A-009 exact deferrals and production/public-release gates remain open.
