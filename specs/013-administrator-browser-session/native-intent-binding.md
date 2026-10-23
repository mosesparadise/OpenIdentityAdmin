# Native administrator intent/completion transport

23 October 2026. Feature 013 T003/T004/T007 prerequisite; FR-001–004/013, B3-AUTH-01–04. Base is accepted PR #74 squash `892267a4b4fc7ea154bba70318cb13b55d536af3`. This slice proves transport representation; durable completion and product B3 routes remain pending.

## Shared representation

`BrowserLoginBinding` reads the all-or-none `admin_login_intent`/`admin_login_completion` pair from original HTTP cardinality, native parameters or a private principal. Each selector is exactly 32 decoded bytes in canonical 43-character unpadded base64url. Empty, partial, duplicate, aliased, array/object/numeric, wrong-type, actor-bearing or multiple-identity representations reject. Absence is a separate result, so malformed presence cannot silently become ordinary B2. Private claims have no public destinations. Copying refuses existing binding; an immutable snapshot can detect later substitution. Parsing and copying grant no authentication or completion authority.

Intent hashing uses SHA-256 of the decoded original bytes, with uppercase hexadecimal output. It intentionally does not reuse the older ceremony digest of ASCII selector text or PKCE hashing. The independent vector hashes bytes 0 through 31 to `630DCD2966C4336691125448BBB25B4FF412A49C732DB2C8ABC1B8581BD710DD`.

The initial Host run caught two noncanonical-pad-bit cases throwing `FormatException` in .NET 10's strict decoder. The attempted `TryDecodeFromChars` alternative also throws for this input in the pinned runtime. The correction catches only `FormatException` around bounded decoding and retains exact canonical re-encoding. The unchanged raw/private regressions must return Invalid without throwing; no assertion or accepted representation is weakened.

## Native boundaries and controls

The provider fixture makes actual authenticated PAR, persists native request tokens, restores them through `RestoreParametersFromRequestToken`, authorizes through existing B2 account admission, validates persisted authorization codes, exchanges tokens and validates access/ID tokens. An independent issuer root restores the original protected pair despite substituted outer authorization parameters. Token-request substitutions cannot replace private code binding.

Only the test probe copies the restored pair after existing account admission and before `EvaluateGeneratedTokens`. No production handler, registration, native validation or B2 allowlist is replaced. The no-transfer control proves ordinary production B2 does not turn carried fields into code binding or B3 claims. Token claims and responses contain neither selectors, intent digest nor the B3 source-expiry claim. Foreign-client and unsupported-response-flow negatives use exact safe error oracles, unchanged native token/authorization counts and genuine PAR controls before and after rejection.

Client tests run the pinned `AttachCustomChallengeParameters` and `AttachPushedAuthorizationRequestParameters`, then prepare and cryptographically protect/validate actual native state. State carries the intent digest; it excludes completion and private pair data. Mutating request data does not rewrite protected expectations. The existing production B2 challenge validator accepts the valid baseline, rejects the extra B3 pair and accepts the restored baseline.

Thirty new Host cases cover independent canonical digest, original input cardinality, native JSON shape, private ambiguity/type/overwrite, real client transport/state and B2 rejection controls. Ten native cases add five per provider. Discovery preserves all 1,311 previous identities and their complete portable type/trait metadata, total 1,321; BrowserSessions requires 40/provider. No coverage input/floor, mutation target, provider, deadline or prior case is removed.

## Review and delivery evidence

Independent architecture/API/test and security source reviews accepted the representation-only boundary and native event order. Review corrections isolate wrong claim type with unchanged canonical value, reject overwrite of a valid existing binding, and require exact native errors with positive controls and unchanged persisted counts. Local Host and Administrator builds pass with zero warnings/errors. Corrected Host execution passes all 1,270 cases, including the 30 additions, with zero failures/skips. Native discovery validates 1,321 exact portable identities; native database execution remains pending hosted CI. All 75 relevant Python selection/evidence/browser-story controls, code-quality rules, whitespace verification, AI-instruction checks and full-source secret scanning pass. Full exact-head hosted acceptance remains pending; this source checkpoint does not assert future CI success.

## Next dependent implementation

T006 must persist continuation/completion bound to the exact intent, fixed client, browser and genuine newly completed password/MFA session. Attach completion exactly once within the existing PAR-owned transaction, preserving original intent/session/request deadlines, native/account lock order and current-authority checks. Private-claim survival is not validation: `AdministratorAccountAuthority.PublicBinding` intentionally excludes private claims, so B3 must separately recheck the original immutable binding at final issuance. Public B3 claims and canonical B1 ticket projection require this durable authority first. T003/T007 remain partial; T005 browser generation and product orchestration remain unimplemented here.

A-009 defers documented Firefox/Playwright issues only for internal work. PR #69 stays draft and issue #70 stays open. Six historical findings, 48 human rows, 345 not-assessed controls, activation and production/public-release gates remain open. A-004 remains expired; A-005/A-006 remain concluded.
