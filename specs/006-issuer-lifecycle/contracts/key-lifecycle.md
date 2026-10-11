# Staged issuer key lifecycle — T016 design draft

Status: **design draft, no implementation or runtime acceptance**. Restart PR #17 and distribution PR #18 are squash-merged. This is the third sequential M01-E increment, on `016-issuer-key-rotation`; LIFE-007 / US3 remains open. Existing document approval covers this bounded scope. The two file-layout choices below await the user's answer. Independent native-source research is complete; concrete-contract review and the configuration-transition experiment remain required before T017.

## Scope and native ownership

Support deliberate, restart-based publish → activate → overlap → retire transitions for the existing Development pilot's RSA signing/encryption certificates. Preserve the canonical issuer, both database providers, native OpenIddict managers/token storage/crypto, Minimal API decisions, DP ring/wrappers, PKCE, browser binding and current lifetimes. No new production project, schema, credential-generation endpoint, hot reload, automatic key activation, global last-use ledger or cluster coordinator is proposed.

OpenIddict 7.7.1 sorts credentials by certificate validity/expiry; adding a longer-lived staged certificate can make it the default. Registration order is not an activation policy. All registered signing/decryption credentials remain available to native validation, and native JWKS projection publishes public signing material only. This adapter uses protected private PFX inventories, including retained signing keys; public-only historical custody requires separate design.

## Proposed file layout — user decision pending

- Keep ownership, parsing and lifecycle value types in `AuthServer/Features/Issuer/Credentials/`, preserving `IssuerCredentials`' current public active/wrapping properties.
- Add `AuthServer/Features/Protocol/Keys/Setup.cs` and a small native generation-event selector/guard. `Protocol/Setup.cs` calls the feature explicitly. Native OpenIddict continues to construct JWKS; no replacement discovery/JWKS endpoint is introduced.
- Extend existing `Host.Tests/Features/Issuer/` component tests and existing lifecycle `Fixtures/`; add no test project.
- Proposed lifecycle use-case groups: `Features/KeyRotation/Activation/` and `Features/KeyRotation/Retirement/`, with separate bounded provider invocations. The alternative requested from the user is one KeyRotation folder with explicit test-class partitions.

The user has been asked to choose the credential/adapter layout and activation/retirement test grouping. These are structure questions, not a new permission gate for already-approved implementation. Preserve thin Programs, Composition extensions and feature Setup conventions.

## Backward-compatible configuration

Existing `Identity:Protection:Signing:Path/Password` and `Encryption:Path/Password` remain the **explicit active** credentials. Existing configurations require no rename or new field. For each of Signing and Encryption, add:

| Path below that purpose | Meaning |
| --- | --- |
| `Staged:Path` / `Staged:Password` | Optional single replacement PFX, loaded for publication/acceptance but never chosen automatically for issuance. |
| `Previous:{0..3}:Path` / `Previous:{0..3}:Password` | Up to four retained historical PFXs for native verification/decryption. |

A partially supplied slot, malformed/noncontiguous Previous index, excess inventory, duplicate credential within or across purposes, or missing active pair fails with the existing fixed protection diagnostic and startup exit 2. Do not echo paths, passwords, configuration or raw PFX/ring content. Unknown lifecycle fields must not silently select another active key. Retain the existing 1 MiB PFX limit, single certificate/encrypted private-key bag, password MAC, RSA >=3072, usage, symlink/permission and private-key operation checks. Full purpose separation covers every active, staged, previous and DP wrapping key.

Active certificates must be currently valid. Staged certificates may be currently valid or future-valid, but not expired. Previous certificates may be expired for retained native verification/decryption; future-not-yet-valid certificates belong in Staged and are rejected in Previous. Previous keys must never become issuance fallback. Operators must rotate signing sufficiently early for issued artifacts to expire within the signing certificate's validity. Some external RPs enforce `x5c` certificate dates independently of JWT `exp`.

Replace positional `certificates[0/1/2]` and `WrappingDecryptors.Skip(2)` with explicit purpose inventories and active references. A separate owning list still disposes every imported certificate, including partial loads. Token-key additions must never enter the DP wrapping inventory. Preserve composition-owned disposal after all host consumers stop.

## Explicit native selection and lifetime guard

Register the complete signing and encryption inventories through native `AddSigningCertificate` / `AddEncryptionCertificate`. Native post-configuration must retain all keys for signature validation and decryption. Keep RS256 fixed: native `AttachTokenDigests` derives digest behavior independently from the first asymmetric credential; algorithm agility is outside this increment.

Register a feature-local `GenerateTokenContext` handler at `Protection.AttachSecurityCredentials.Descriptor.Order + 500`, before native `CreateTokenEntry` at +1000. Resolve the explicit active signing credential by loaded key identity. Require non-null native encryption for code/request artifacts and null encryption for access/ID artifacts; fail safely if that invariant changes. Replace the non-null encryption selection with the active encryption credential, preserving clear access/ID tokens and native encrypted code/request payloads. Never reorder/mutate shared options per request, sign/encrypt manually, or fall back to Staged/Previous.

Use the native current `TimeProvider` for each generation decision. Validate the actual principal's creation/expiration bounds and supported token type; reject absent/invalid/excessive lifetimes rather than shortening or replacing them. Hard pilot maxima remain code 60 seconds and request/access/ID 300 seconds. This matters because native request caching can apply per-client lifetime settings before application pass-through eligibility checks. Global options alone do not prove the retention bound. Reject future creation beyond the declared zero skew and expired output. Read `context.Principal.GetCreationDate()` / `GetExpirationDate()`; native principal preparation attaches those dates before generation (access 3678/3707, code 3813/3842, request 4343/4372, ID 4664/4693 in `OpenIddictServerHandlers.cs`). Descriptor dates are attached later and are not the guard input. The guard rejects with native `server_error` and the fixed safe description `The issuer cannot issue tokens with the configured key policy.` Source-derived expected oracles are token endpoint HTTP 500 JSON `server_error` with no returned credentials; validated authorization-code issuance redirects to the exact registered callback with `server_error` and original state; cached-request rejection redirects with `invalid_request` and original state because the pinned native request-caching branch propagates the outer notification error rather than the rejected sign-in event. No code/request_uri or token fields may accompany these denials. Validate these path-specific expectations in the controlled experiment/tests; a generic non-200 or middleware 503 is not an accepted guard oracle. Never add an unsafe redirect to obtain the expected status.

The active signing certificate must be valid now and cover that artifact's original expiration plus the declared relying-party skew (zero for this controlled profile). The active encryption certificate must be currently valid whenever native generation needs it; retained expired encryption certificates remain usable for decryption. Invalid active state yields safe denial with no returned credentials, never automatic fallback. Component and real-host tests cover certificate expiry, lifetime overrides and failure diagnostics.

This guard precedes creation of that generated token entry. Native code redemption may already have happened earlier, and multiple generated artifacts are not wrapped by a new application transaction. Do not promise atomic rollback, unchanged native state on every late failure, or retry of the same code. Observe actual side effects and require fresh-flow recovery where appropriate. An earlier sign-in preflight is optional only if native-order research establishes a concrete need; it does not replace the per-generation guard or create rollback semantics.

## Artifact retention and public-key propagation

| Artifact | Maximum lifetime / skew | Material retained |
| --- | --- | --- |
| Native authorization code | 60s / zero | Old signing verifier and encryption private key. |
| Native cached authorization request | 300s / zero | Old signing verifier and encryption private key. |
| Access and ID token | 300s / controlled RP zero | Public old signing key, including the supported certificate-validity window. |
| Session cookie, form/continuation protections | Existing 900s session and existing interaction policy | Original DP ring and wrapping decryptors; token-key rotation does not retire these. |

Codes and cached requests are reference tokens in this profile. Their browser-visible strings are not JWEs. Tests correlate the exact native entry using native managers, inspect its persisted protected payload privately, and verify actual signing/encryption `kid` plus original identity/status/expiration. Never print those payloads, tokens, codes or reference identifiers.

Current middleware sends `Cache-Control: no-store` and `Pragma: no-cache` on discovery/JWKS. Preserve those headers. They do not bound an RP's application-level key cache. For the controlled pilot, every participating RP must acknowledge a fetched overlap key set before activation; the test captures an immutable prepublication JWKS snapshot and performs later validation without refreshing it. An old-only snapshot must reject new-key tokens and recover after explicit refresh. No universal external cache TTL or propagation duration is invented.

Let `L` be the **last possible issuance using the old signing/encryption pair on any node**, including a rollback. Retention must extend strictly beyond the maximum affected artifact expiry (`L + 300s` in this bounded profile), the actual verifier/server skew, and any additionally documented participating-client or rollback retention requirement. A known outage or stale node invalidates the assertion that old issuance ended. If `L`, issuer inventory or a participant's cache policy is unknown, retirement is not authorized by this procedure. Certificates must remain valid for supported public-token validation; expiration is not a safe rotation trigger.

This is an operator-enforced rollout/retention contract, not a runtime proof of cluster-wide last use. The host cannot detect that an entire historical slot was omitted from a new configuration without authoritative history. The approved spec requires premature omission to fail the relevant old-artifact control; it does not require a new durable anti-omission ledger. Record that limit explicitly in tests and the runbook. Backup-required keys remain under separate protected retention; DP wrappers are never deleted merely because token TTLs elapsed.

## Four-stage two-node operation

1. **Publish/preload:** provision distinct replacement signing/encryption PFXs outside repository/web/publish roots. Install the same old-active/new-staged inventories on every node by controlled replacement. Verify both nodes' public overlap and decryption inventory, stable issuer/store/DP identity, and explicit old issuance. A staged certificate deliberately has later expiry than active in the regression control.
2. **Acknowledge propagation:** every participating RP captures/acknowledges the overlap public set. Retain a stale old-only set as a negative test. Do not equate successful JWKS fetch by one process with universal cache convergence.
3. **Activate with overlap:** on B, move new to root active, move old to Previous and remove the now-active Staged duplicate; replace B. A still issues old, B issues new, and both must accept original artifacts from either. Then activate A. Verify actual key identities on newly generated public and internal artifacts on both nodes. No configuration/value/file substitution may secretly retain old active selection.
4. **Retain and retire:** after every old-active process has stopped, record the last possible old issuance boundary and supported cache/rollback obligations. Wait strictly beyond both the operator boundary `L + 300s` and every observed affected original expiration (plus any additional declared allowances), with keys still present; establish native expiry denial and public lifetime expiry before removing old material. Remove the old generation on both nodes, replace them, verify new-only public signing publication and issuance, and retain original eligible DP cookie continuity.

Rollback changes explicit active selection only while that certificate can satisfy validity/expiry guards. Keep the newer generation in Staged/Previous everywhere for already-issued newer artifacts; an old-only rollback is unsafe. A rollback resets the last-old-issuance boundary. A failed activation does not justify generating replacement keys, deleting the ring, weakening TLS or replaying an ambiguous redemption.

## Test-only configuration transition seam — experiment required

Reuse the accepted two-node graph, immutable `NodeTransport`, actual process/listener provenance, bounded stop/start and cleanup. Stage transitions must load the reviewed production configuration in a **new actual AuthServer process**, not replace OpenIddict options inside the test process.

Proposed seam: each test node gets a private content/config directory with a bounded `appsettings.json`; the actual host loads it through its standard content-root configuration on process replacement. Remove only that node's exact `Identity__Protection__` environment overrides, preserving `Identity__Issuer`, environment, least-privileged database references and all unrelated settings. Standard nonsecret content/web-root arguments may locate configuration/assets; passwords remain private files or secret injection, never command-line arguments or logs. Production hosting/configuration code should not need a new provider just for tests.

Write complete well-formed JSON into a private mode-0700 directory, with mode-0600 files, and atomically replace it only while that node is stopped for supported operational transitions. The running-process rewrite below is a deliberate negative experiment exception: it must prove that JSON reload does not activate another key. Accepted activation must exercise process replacement rather than standard JSON live reload. Use syntactically valid JSON with invalid credential configuration for the established safe exit-2 check; malformed JSON fails earlier in the standard builder and is a separate configuration failure boundary.

Before T017, prove this seam against the existing real host: an atomic configuration rewrite leaves the already-running process on its original credential selection, the replacement process reads the new selection, unaffected production assets/Razor behavior and safe invalid-config diagnostics remain intact, secrets do not appear in snapshots/logs, and cleanup removes private files after process exit. If standard content-root loading cannot preserve these invariants, revise/review the seam rather than inventing a hidden production switch. Do not silently reuse the one-file distribution variation as proof of a complete active/staged/previous configuration transition.

Allow only the expected credential/config inventory delta for each stage. Canonical origins, native stores, completed migrator/bootstrap generations, DP ring/wrappers, original artifacts and unaffected node identities remain invariants. Capture failure paths and restore/dispose within owned bounds; no generic security mutation retries.

## Minimum acceptance matrix

| ID | Required oracle / control |
| --- | --- |
| KEY-01 | Legacy config behavior, all inventory/slot/count/index/duplicate/purpose/usage/validity failures, safe diagnostics and partial/full disposal. |
| KEY-02 | Native sort places longer-lived staged signing/encryption first, but old explicit active `kid` remains on actual new public/internal artifacts. Both nodes publish exactly the approved public overlap, with no private fields. |
| KEY-03 | Frozen overlap JWKS validates subsequently issued new-key tokens; stale old-only snapshot rejects exact new bytes, explicit refresh recovers. Preserve issuer/audience/nonce/lifetime/signature checks and explicitly recompute RS256 `at_hash` from the exact returned access-token bytes; merely allowing the claim is insufficient. |
| KEY-04 | B-first mixed activation accepts old→new and new→old original codes and cached requests, with exact native payload key IDs and unchanged original expiration. Then both nodes issue with the new pair. |
| KEY-05 | Original cookie, pending antiforgery form and completed one-use continuation survive rotation without new credentials, refreshed proof, replacement session or changed authentication time. Retain browser-binding/replay controls. |
| KEY-06 | Premature old signing publication removal makes a fresh JWKS verifier reject still-live exact old public tokens; retained public keys validate them, restoration recovers. This is not immediate revocation for already-cached verifiers. |
| KEY-07 | Premature old encryption removal rejects exact live code **and** cached-request artifacts through native decryption, with no illicit issuance/consumption. Restore historical material and recover those same original artifacts before original expiry. |
| KEY-08 | Roll back active choice while retaining both generations; verify interoperability and renewed retention anchor. Unsafe old-only rollback rejects newer artifacts; recover using retained material. |
| KEY-09 | Reject active expiry/future activation, insufficient signing certificate tail and actual token-lifetime overrides without selecting staged/previous keys; account for native redemption side effects rather than inventing rollback. Use exact path-specific errors, never arbitrary non-success. |
| KEY-10 | Capture a conservative last-possible-old-issuance boundary only after every old-active generation terminates; rollback resets it. Require actual time strictly beyond `max(boundary + 300s + skew, original observed expirations, extra retention requirements)`, with historical material still available. Then retire on both nodes; new-only issuance/publication and original eligible DP cookie still work. No shortened production TTL, expiry rewriting or clock jump substitutes for the real retirement case. |
| KEY-11 | Every transition proves real process replacement, expected configuration/material delta, unchanged store/DP identity, exact discovery/execution inventory and successful bounded cleanup on both providers. |

KEY-09 also requires maximum and maximum+1 client lifetime values for every native request/code/access/ID principal-preparation path, and missing/zero/negative/future-created/expired date controls. Use the real native principal-preparation handlers in component tests and a genuine positive control. The enabled pilot rejects nonempty client settings before ordinary code/access/ID issuance, so preserve that application policy rather than weakening it to reach the guard. The request-cache override has a genuine real-host path before application pass-through and requires real-provider evidence. Cover late ID-generation rejection after access generation in the appropriate native-pipeline integration scope: record actual code/earlier-token state, no returned credentials and fresh-flow recovery; do not label this as a normally reachable pilot HTTP path or claim atomic rollback.

Add pure retirement-boundary controls for absent evidence, a stale/unknown or still-live old-active node, exact boundary equality and rollback reset; none may authorize retirement. The real test must also fail if the last-old-generation boundary cannot be established.

Separate Activation and Retirement invocations are proposed because real retirement includes a five-minute wait. Each retains the existing 660-second invocation and 20-minute job limits; no old suite is enlarged, dropped or hidden. New selection must be disjoint/exhaustive against discovery, mandatory on both providers, and included in child-process coverage. Extend the explicit story/coverage manifests and seeded evidence-rejection controls only after the grouping decision. Final counts come from implementation/discovery, not this design inventory.

Full existing unit, standard-provider, restart, distribution, six-cell browser, mutation, contrast, supply-chain, static-analysis and assurance checks remain mandatory. Coverage floors remain 90% line/80% branch for every required module. Required experiments/reviews and exact source/run/merge trees are recorded before acceptance. No rotation result, certification, production failover/RPO/RTO or manual accessibility acceptance is claimed by this draft.

## Native-source review references

Pinned OpenIddict 7.7.1: `OpenIddictServerConfiguration.cs` 544–580 (sort and validation inventories); `OpenIddictServerHandlers.Protection.cs` 1450–1515 and 1769 onward (attachment, entry creation and native serialization); `OpenIddictServerEvents.Protection.cs` 69/78 (credential properties); `OpenIddictServerHandlers.Discovery.cs` 1168–1331 (public JWKS); `OpenIddictServerHandlers.cs` 4343–4372 (request lifetime overrides), 4928–4936 / 5146–5154 (native code/request generation), 5391 (digest selection); `OpenIddictServerOptions.cs` 197 (zero server skew); `OpenIddictServerBuilder.cs` 748 (private signing certificate requirement).

Repository sources: `Features/Protocol/Setup.cs` (pilot limits and native registration), `Features/Protocol/Protection/PilotRequestProtection.cs` (cache policy/safe failures), `Features/Pilot/PilotProfile.cs` (interaction/session limits), `Features/Issuer/Credentials/IssuerCredentials.cs` (ownership/loading), and the actual-node lifecycle/RP fixtures. Source research is not an executed runtime result.


## Traceability and current review disposition

KEY-01/09 trace LIFE-007/008; KEY-02/03/04/06/07/08/10 trace LIFE-003/004/007/008; KEY-05 traces LIFE-002/004/007; KEY-11 plus runner/coverage gates trace LIFE-001/006/009/010. T016 owns this contract, structural decisions and the required seam experiment; T017 owns implementation; T018 owns actual both-provider acceptance and squash delivery.

Architecture/security/test native-source reviews informed this draft. Concrete-contract review tightened actual principal date access, expected encryption nullability, staged versus historical certificate validity, exact environment override removal, private atomic stopped-node JSON replacement, safe credential versus malformed-JSON boundaries, and conservative all-node retirement timing with rollback controls. Architecture, security and test reviewers passed this bounded concrete design after those corrections. These are design dispositions, not executed tests. User layout answers and the real-host configuration experiment remain open before T017.
