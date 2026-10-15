# Native integration prerequisite inventory

15 October 2026. Implementation branch `054-admin-protocol` starts from verified planning squash `98139cc942467f4094434691c30668ba72dbeb70` (PR #55). This checkpoint supplies the locked client dependency and executable API/transport controls for T004, and advances T005. **It is not B2 implementation acceptance. T005 remains open.** No administrator protocol composition, credential loading, registration, callback or route is enabled by this change.

## Dependency and composition delta

`Directory.Packages.props` and the Admin BFF project add only `OpenIddict.Client.AspNetCore` and `OpenIddict.Client.SystemNetHttp`, both pinned to 7.7.1. Native EF/server packages remain 7.7.1. The original client addition changed four lock graphs: BFF, Host.Tests, AppHost.Tests and API.Contracts.Tests. The later MessagePack correction below additionally updates AppHost and its four Aspire test graphs (AppHost.Tests overlaps). The new client/HTTP resilience closure accounts for the additions; existing package resolution differences remain explicit.

In the BFF, JsonWebTokens, Tokens, Logging, Abstractions and Protocols resolve to 8.19.2; Protocols.OpenIdConnect and System.IdentityModel.Tokens.Jwt remain 7.7.1. AppHost tests retain 8.16.0 for the latter two. Host.Tests resolves JsonWebTokens/Tokens to 8.19.2. A central requested version is not proof of a resolved version. The actual four changed graphs contain 203 distinct package/version pairs; the complete solution license inventory contains 252.

Executed with SDK 10.0.401:

```sh
dotnet restore --force-evaluate --disable-parallel -v minimal
dotnet restore --locked-mode --disable-parallel -v minimal
dotnet package list --vulnerable --include-transitive --format json --output-version 1
python3 scripts/check_supply_chain.py vulnerabilities <retained-report>
python3 scripts/check_supply_chain.py licenses <retained-inventory>
```

Both restores passed across all 23 projects. The current advisory report covers all 23 with no known vulnerabilities reported; all 252 package/version license declarations pass existing policy without a new exception. These are dependency results, not an interoperability or security-assurance claim. No ephemeral key or credential fallback was introduced.

The native client must eventually be registered in the separate BFF/test-client service provider, using `AdminSessionDbContext`. `AddSessionPersistence` alone still does not register native core/client managers. Adding a second `UseDbContext` registration in the issuer provider would replace its native context mapping; it is not a way to isolate the client. Existing production composition remains unchanged.

## Pinned event and ownership inventory

Source references below are at OpenIddict commit [`9146a148b3333570704a0211d037cd318093770a`](https://github.com/openiddict/openiddict-core/tree/9146a148b3333570704a0211d037cd318093770a/src). Installed-package controls verify the usable descriptors and scoped context identity. They do not prove database transaction participation.

| Boundary | Exact extension point and implementation obligation |
| --- | --- |
| Assertion consumption | `ServerHandlers.cs` 607–911 validates native assertion signature/audience/issuer/subject. Insert the fixed-profile product handler after `ValidateClientCertificate`, before `ValidateRequestToken`. Require exact canonical audience and strict product claims/time. Commit the replay ledger independently before grant work; grant failure cannot unconsume it. The obsolete `ValidateClientAssertionAudience` class is not an active handler and must not be used as an integration anchor. |
| PAR identity | `ServerHandlers.cs` 4384–4407 persists distinct request-type claims; `Authentication.cs` 1905–1964 enforces the per-client PAR requirement. Preserve native type/client validation and protected parameter restoration. Ordinary authorization-request caching does not satisfy this requirement. |
| Native redemption | `ServerHandlers.cs` 2992–3105 intentionally redeems the native entry early. The issuance transaction must not roll this back on later rejection. A rejected operation need not issue a credential merely because its input was consumed. |
| Issuance owner | A `ProcessSignInContext` handler after `RestoreInternalClaims` (its order + 500) is after native redemption and before `AttachAuthorization`. Establish profile/governance/account ownership, reread current authority and native bindings, then keep ownership through native generation. Use a dedicated issuance commit capability; do not reuse governance mutation approval. |
| Issuance commit | Authorization, token and PAR apply-response handlers run at order 500000. A commit hook at 499000 follows native generation/parameter attachment and precedes response dispatch. Final time/authority checks and database commit must finish there. Denial, exceptions and cancellation need deterministic rollback before error dispatch, with request-finally cleanup as fallback. Descriptor ordering alone does not prove those paths. |
| Shared EF owner | EF builder 124–129 registers a scoped context wrapper; context 29–44 returns its injected context. Token store 120–128 and 1198–1214 saves that context. Repository persistence registration disables entity caching. Prove native manager writes actually join the owned transaction on both databases; clear clean stale tracking before authoritative reread. |
| Invalidation serialization | Existing `CeremonyLocks` orders governance before accounts. Governance changes, logout and credential revocation share those boundaries. Add profile ownership in one consistent order and retain the required locks until issuance commit; an earlier detached cookie observation is insufficient. |
| Client state | Wrap native `GenerateLoginStateToken` (order 100000) with a scoped Sessions quota/transaction owner. Nested `Protection.CreateTokenEntry` then `AttachTokenPayload` persist the row and protected reference. Commit and release before `SendPushedAuthorizationRequest` (172000), never across HTTP. Use try/finally for exception/early-rejection cleanup. Removing a native descriptor also removes its DI registration, so a delegating wrapper must explicitly register the native implementation. |
| Client identity | Set a stable client URI, explicit RegistrationId and exact callback across instances. State issuer otherwise derives from BaseUri. Native URI validation can tolerate a trailing-slash variation; exact product binding needs its own post-native equality check. |
| State replay/expiry | Native state redemption precedes error/correlation processing. Preserve it for error callbacks. Native expiry uses `exp + skew < now`; add strict expiry-at-equality and a final check after token HTTP before accepting callback output. |
| Cleanup and quota | Client native rows have no ApplicationId. Count state by native type in its isolated Sessions context. Native `PruneAsync` can execute 1,000 batches of 1,000 without profile filtering; it cannot implement the scoped 100-row contract. Use bounded owned selection/deletion with native status/expiry/dependency predicates and no live eviction. |

Native optimistic code redemption supports at most one successful concurrent use. Ordinary valid-code replay after issuance invokes native linked-token-row revocation while retaining the authorization. A losing concurrent redemption does not itself invoke the same revocation path. Tests must distinguish those cases; this checkpoint adds no stronger authorization-revocation policy or offline API enforcement claim.

## Executable controls and their limits

Permanent tests under `Host.Tests/Features/AdministratorProtocol` check:

- actual installed descriptor ordering for the independent assertion transaction, issuer issuance window and native client state generation;
- native core resolving the exact scoped `PlatformDbContext` or `AdminSessionDbContext` in separate component providers, without opening a database;
- the real native HTTP factory sending one PAR/token POST through an in-process transport on 503 and simulated response loss when both retry mechanisms are removed;
- configured ten-second HTTP timeout and disabled automatic redirects before substitution of the final network transport.

The native no-retry API is `UseSystemNetHttp(...Configure(...))`, setting both `HttpErrorPolicy` and `HttpResiliencePipeline` to null. Dedicated setter methods reject null. `ConfigureHttpClient` sets the timeout and `ConfigureHttpClientHandler` disables redirects. The tests deliberately configure these APIs directly: no production client setup exists yet. They exercise neither TLS nor an actual PAR/token exchange. Inspecting `AllowAutoRedirect` does not prove execution against 307/308; real redirected-request counts remain future integration evidence.

A separate local installed-package probe reproduced a deadline gap: a cancelled `GenerateTokenContext` still reaches the token manager's `CreateAsync` with a non-cancellable token. Native client handlers omit cancellation arguments in creation, lookup, update and redemption. Server paths have equivalent omissions. This is an integration obligation, not a newly discovered defect in an activated product path.

## Local verification of this checkpoint

The Host project and its referenced production projects compile with zero warnings/errors. All 400 Host tests pass, including the seven new dependency controls, with zero skips. The test matrix changes only Host's unit inventory from 393 to the observed 400; no native partition, mutation target, coverage input or floor is removed or weakened.

A bounded negative control enabled one immediate retry in the native factory configuration. All four PAR/token 503/lost-response cases failed at the intended request-count assertion (expected one send, observed two). Restoring the test source byte-for-byte returned all seven focused cases to passing. This proves the new test oracle detects transport resubmission; it is not a B2 security mutation result or a real network exchange.

The first compile failed on a missing namespace and formatting; corrected before execution. The first full Host run had 399 passes and one repository-path failure caused by relocated generated binaries. Moving only the new output beneath this repository fixed that environment issue; the subsequent 400-case run passed. Prior binaries and failed receipts were preserved. Code-quality checks pass without changing allowances; scope, instruction, assurance and selection controls pass. Full exact-head CI and final independent review are still required before this executable prerequisite change can be accepted or merged.

## T005 remains open: next implementation boundary

Before completing T005 or claiming a coherent issuer path, add and review explicit scoped cancellation propagation into the native EF operations, bind it to the exact context/transaction owner, and prove rollback on rejection/exception. An outer cancellation source or five seconds per command is not the total five-second authority deadline. Candidate store overrides must cover every database-performing method, including enumeration, navigation loads, bulk revocation and manager validation queries; forwarding only writes is insufficient. Preserve native manager validation, cryptography, replay and concurrency semantics. No blanket retry or weaker linearization rule is authorized.

Then complete the actual both-provider manager transaction/rollback proof and T006's native partition/mutation plan before US1–US3. Remaining acceptance includes authority invalidation races, registration/audit atomicity, assertion consumption, all native storage caps, partial/unknown commits, strict expiry, real client/server interoperability, two-instance state, keys, cancellations and real redirect/lost-response behavior. No database or browser B2 result is supplied by this prerequisite checkpoint. All six historical findings, 48 human rows and production/public-release gates remain open.

## First full-CI failures and corrections

PR #56 head `ab37bc60b2ccf403cbe5873999fa3304c3835637`, run `38787608672`, exposed two required-check failures. Static formatting found an incorrectly ordered namespace import in the new dependency test; the targeted native formatter verification passes after sorting it. The supply-chain job found [GHSA-qhrr-8q5h-9q3h](https://github.com/advisories/GHSA-qhrr-8q5h-9q3h) in Aspire's existing transitive MessagePack 2.5.302 across five projects. The advisory was added/reviewed in the public database on 15 October; the earlier local lookup did not report it and is not current acceptance evidence.

Pin MessagePack 2.5.307 explicitly in the AppHost, using central version management. That supported 2.x servicing release includes the 2.5.303 nested-array allocation fix and the later LZ4 integer-overflow fix ([release](https://github.com/MessagePack-CSharp/MessagePack-CSharp/releases/tag/v2.5.307)). Avoid an unrelated major-version upgrade. Both MessagePack and its annotations now resolve to 2.5.307 in AppHost, AppHost.Tests, Issuer.Tests, Issuer.Lifecycle.Tests and Administrator.Tests; the AppHost reference propagates to all four test consumers. No vulnerability suppression or license exception was added.

Force-evaluated and locked restores pass. A fresh isolated HTTP-cache advisory lookup covers all 23 projects and reports no known vulnerable packages; the corrected 252-pair license inventory passes. Retain the hosted failed advisory artifact (`11997784756`) and initial run. Corrected-head full CI must establish solution and native Aspire compatibility before merge; the earlier local Host pass alone cannot validate this dependency correction.
