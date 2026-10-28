# Native refresh primitive characterization

**Follow-on candidate (28 October 2026):** [Native authenticated cleanup characterization](native-cleanup.md) extends the delivered primitives with complete native code/offline issuance and private selector client/server cryptography. Its22 new cases are pending hosted acceptance. It does not close T003 or establish B3, TLS or durable assertion replay integration.

## Scope and checkpoint — 28 October 2026

This is the first implementation increment within **T003**, based on merged planning PR109/main `34c5df010df65ac2f2166a525d4028fcd7e86dcd`. T003 remains open. Production grants, endpoints, registration, persistence schema and package versions are unchanged. This suite characterizes installed OpenIddict7.7.2 storage and handler primitives; it does not claim a complete refresh exchange, native client transport, cryptographic interoperability or authenticated family-cleanup endpoint.

The fixture leases existing Aspire PostgreSQL/SQL Server databases, migrates with the normal Migrator and uses the restricted runtime identity. Each operation uses a fresh `ProtocolStorageRequest` and owned service scope; mutations use explicit `ProtocolStorageTransaction` epochs. Observations reload committed state through fresh manager instances. The synthetic principals deliberately enter **after cryptographic validation** and contain only manager-created native row selectors. They are never issued token strings or browser credentials.

`NativeRefreshStages` installs the original native descriptors and filters for `ValidateTokenEntry`, `ValidateAuthorizationEntry`, `RedeemTokenEntry` and `RevokeToken`, using the native dispatcher and real owned EF stores. The original request cancellation token is attached to the native transaction. No claim is made about propagation through native client-created scopes yet.

## Accepted internal delivery — 28 October 2026

[PR #110](https://github.com/mosesparadise/OpenIdentityAdmin/pull/110) is normally squash-merged as `5cccabe559f24822d2827f86a7b3ae221a1e723e`. [Run40678800558](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40678800558) passed all **95 jobs**, including the final acceptance gate. Exact source `042376bc16d895fc7386e40e2068b91a667f2e27`, tested merge `aa8b88623e0037715173649040dea5d05c6b4c15` and squash all have tree `d5c8a7b7d4c56a818e91099a0b671c72700df057`; the tested base was `34c5df010df65ac2f2166a525d4028fcd7e86dcd`. GitHub reported95 successful head checks, no change-request review and no unresolved inline thread before the expected-head squash. No restriction was bypassed.

Independent architecture/security and test/Aspire source reviews pass. Final independent evidence review verified all **89 downloaded ZIP digests** against GitHub's artifact index and all extracted bytes against their archives. The **79 native TRX reports contain4,671 executed/passed cases**, with zero nonpassing counters and consistent native IDs, result/definition/execution joins. All32 new identities match corrected native discovery exactly. Native unit display names may be truncated; unique native IDs and joins remain decisive rather than assuming every unit display name is unique.

All **76 required coverage inputs** exist and all **15 unchanged90%line/80%branch floors** pass on the downloaded merged report. Ten source-bound mutation receipts establish **64 exact behavioral kills**, with matching manifests, independently recomputed original/mutant hashes, passing baseline/restored controls and source restoration. These are the existing mutation suites, not invented new refresh product mutants. SDK10.0.403/runtime10.0.14, OpenIddict7.7.2 and Playwright1.63.0 were retained. Chromium/WebKit obligations ran; A-009's Firefox and two exact issuer certificate deferrals remain deferred, never passed.

| New provider evidence | Artifact ID / ZIP SHA256 | Raw TRX SHA256 / result |
| --- | --- | --- |
| PostgreSQL | `12812525606` / `63659bae3f08dc9319219a287ec9e462ee31e149400bfd65cf70d1b8b77bec4a` | `af433d5426db13c02946d07adcb973f05266ad2f736c803698a464f6b750ce8f` /16 passed,0 skipped |
| SQL Server | `12812215930` / `9a88908166d712e4e1878bd1a0b44b6351e213e902765c76e1e9542e5c76f90d` | `8ba99b9f68a09075c7594e9b615dd5217a4b2a405b2c2c84b16139c5df3a0419` /16 passed,0 skipped |

Merged coverage artifact12812154848 ZIP SHA256 is `faefb7b903e009948589ee2522d48271cc3bac47f5af42bb7424b0579e6e54e4`; its XML SHA256 is `f8a2c9a78b6dd63a73076adb1a7729f690428b5f447ba0f693d18e02c6c2ece3`. Full corrected local discovery SHA256 is `adabdc00b0b26621c1b5384af1b269d995a641769d0525b0b692914cbb9e3172`. The independently checked artifact-index receipt SHA256 is `72bc452139694af76060d302d421b9e3c8099870f1f233ba1205fe976f4cd2f2`; per-report receipt SHA256 is `65e0c8ec4544e701a7827f80cff65f4e8aa3cedf3439c4206ff70014c6d7c2ac`. Archives remain attached to the run under its repository retention policy.

Local verification actually included build, native discovery/exact inventory binding, IDE0005 style, code-quality rules, selector/matrix/scope controls and evidence-verifier controls. Hosted acceptance adds real provider execution and full unchanged obligations. This is internal acceptance of the primitive tests only: **T003–T022 remain open**, and no product refresh, crypto/client interoperability, authenticated cleanup, production or release capability is claimed.

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

## Historical candidate verification

Local SDK10.0.403 / runtime10.0.14 build and native discovery pass. Selector controls30/30, browser-matrix controls7/7, assurance integrity and skill configuration checks pass. Static quality initially reported unnamed fixture literals; these are corrected with named values without expanding allowances. Independent architecture/security and test/Aspire source reviews pass after tightening conflict causation, authorization-only denial and cancellation propagation.

The local root filesystem has only about15MiB free. No database container or SQL experiment was started locally; real provider execution belongs to the required hosted jobs. Full CI and exact-head evidence were pending at this candidate checkpoint; the accepted delivery above supersedes that status.

Required hosted commands retain the ordinary driver timeout and native TRX/inventory validation:

```text
python3 scripts/verify.py PostgreSql --partition administrator --story SessionRefresh
python3 scripts/verify.py SqlServer --partition administrator --story SessionRefresh
```

## Rejected hosted attempt and correction

[Run40677653626](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40677653626), source `f79fd670862852fd961313ed95645d32c3ffb6c7`, was superseded/cancelled after correcting two dependent verifier totals; no acceptance is taken from it.

[Run40677785020](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40677785020), source `0255d6953b84140a4138ec457cd1d1eaa6e4c2a3`, executed16 new cases per provider:15 passed,1 failed,0 skipped on each. The expiry case threw `NullReferenceException` at its test setup before any expiry assertion because its manually constructed native context had not run `ResolveTokenValidationParameters`. The correction runs that original native initialization handler before setting zero skew and invoking expiry validation. Before/equal/after assertions are unchanged. This is a test fixture correction, not a runtime or native-library defect disposition. Static formatting also rejected two unused imports in the new fixture; both are removed. Corrected-source full execution was required and is now recorded above.

Rejected artifacts are retained by the run: PostgreSQL12810844635 / SHA256 `6003e9d9aa7c1d58ff31934ae6410168a78ea8677d0875118e3c053b2fa24e4e`; SQL Server12811442174 / SHA256 `7dc3d265e16a9ba6d392149dae249a4301c17be4a3c5ae521a62cc07b391f5e5`. These rejected ZIP digests were subsequently verified against downloaded bytes. Their raw TRX hashes are PostgreSQL `b3384874535c354bfa1032c1692cb50ffcde11786f28bbf980794bd56c775625` and SQL Server `fe8b565c649761b912ee9d5b96e8aac1bac82e01a422459dcb2729821d044855`. Both reports confirm15 passes/1 failure/0 skips; no acceptance is taken from this superseded run.

## Remaining T003 gate

Before dependent integration, prove genuine initial offline issuance and native cryptographic refresh/revocation transport, actual native client-created scopes and request budgets, authenticated ownership-bound cleanup after total initial token-response loss, pre-exchange capacity reservation, disabled-client cleanup and exact first-party confirmation schema. Complete native early-consumption/final-issuance integration and product authorization-wide replay denial with current authority and immutable deadlines. Manager-only family revocation above proves a storage primitive, not authenticated target selection or remote confirmation.

Native ordinary revocation cannot accept an authorization code or confirm family denial. The BFF cannot decrypt an issuer code or query Platform storage; a guessed selector is not an acceptable cleanup contract. Resolve and review that specific integration dependency before implementing dependent state/activation. T003–T022 remain unchecked. All six historical findings,48 human rows,345 not-assessed controls,14 viewer findings,actual Windows T014,A-009 exact deferrals and production/public-release gates remain open.

## Next bounded integration step

Prove a fixed-client cleanup selector derived from the existing protected login-intent digest, using native-authenticated requests and real managers. The BFF already has the intent before exchange; issuer code issuance already observes the actual native authorization ID. A proposed minimal issuer binding joins those facts before the initial response can be lost. Existing `IssuerContinuation` does **not** contain that authorization ID, so the required durable mapping is not delivered by PR110.

The next tests must establish exact client/intent/family ownership, retained denial against late issuance, safe fresh-proof duplicate cleanup, disabled-issuance cleanup eligibility, unrelated-family survival, and absent/lost/forged confirmation remaining unconfirmed. Native `private_key_jwt`, assertion replay consumption, input bounds and response ownership remain mandatory. The fixed first-party selector adapter's exact native event placement and bounded confirmation schema still need proof and review; the ordinary revocation token path cannot accept a login-intent selector unchanged. This is the next T003 characterization dependency, not authorization to skip it or activate dependent implementation.
