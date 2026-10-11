# T031 — Protocol transport acceptance

Authorized next item after T029/PR #13 (`237afa5`), planned before implementation on `011-protocol-transport`. Existing v0.2/A-001 and feature 004 LOGIN-002/003/006/009, US3 AC2, SC-001/003 govern this increment. T032 authorization-state coverage and qualified manual/full-feature acceptance remain separate. No new grant, client authentication method or production topology is introduced.

| Spec Kit prerequisite | Result |
| --- | --- |
| Feature 004 prerequisite resolution | Pass; spec/plan/research/data model/contracts/tasks available |
| Specification requirements checklist | 7 checked / 0 unchecked; pass |
| Extension hooks | None configured |
| Constitution and project structure | Existing Features/Composition/Setup and library ownership retained |

## Matrix and oracles

Use actual Aspire AuthServer processes, trusted HTTPS and both native database providers. Construct raw requests in cohesive `Features/Protocol/Transport*Tests.cs` tests so the existing valid-only exchange helper cannot normalize away the malformed input. Only safe case labels enter theory display names; credentials/codes/tokens remain in memory.

| Group | Inputs / required evidence |
| --- | --- |
| Client authentication | Missing Authorization with/without body client ID; empty Basic credentials, empty client ID, empty secret; malformed Base64 and absent colon; body-only ID/secret; valid Basic plus a conflicting body client ID **without** a body secret; valid Basic plus a body secret as a distinct rejection. Include identical/absent body ID and native percent-decoding positive controls, ordinal case-conflict and duplicate conflicting body IDs. Preserve the original native parameter, not only its typed string projection. |
| Grant selection | Missing/empty grant; unsupported password, client_credentials, refresh_token, device-code and unknown grant. Supply otherwise valid Basic credentials and genuine unconsumed code/proof to avoid a missing-code or unknown-client oracle. |
| Token transport | Reject GET/PUT/DELETE/HEAD and POST with absent, JSON, text, multipart or malformed form-suffix content type. A correct form POST succeeds. HEAD has no response body by HTTP semantics; check status/headers and persisted no-issuance state. |
| Authorization transport | Reject PUT/DELETE/HEAD and POST with absent, JSON, text or multipart content type, with otherwise valid authorization values. Valid GET and form POST reach the canonical cached authorization continuation, protected login and the registered callback. Observe redirects manually; never follow an untrusted destination. |
| Forwarding headers | Separate standard `Forwarded`, `X-Forwarded-*` and combined conflicting header sets. On real HTTPS, spoofed host/proto/prefix cannot alter discovery issuer/authorization/token/JWKS URLs, native cached-request redirect origin, cookie security, final callback or independently validated token issuer/audience. On a test-only real loopback HTTP endpoint, spoofed proto=https must still fail native HTTPS enforcement with exact error identity ID2083, not a missing-credential error. Plaintext probes carry no password, client secret, code or verifier. |

Pinned OpenIddict 7.7.1 source defines native transport outcomes: token `invalid_client` is 401 with a Basic challenge; `invalid_request` and `unsupported_grant_type` are 400. Missing client ID is `invalid_request`; a known client with no secret is `invalid_client`. Disabled body-secret authentication is rejected before Basic extraction. Authorization extraction failures are 400 text protocol errors without a redirect; do not substitute an assumed framework 405 or accept any arbitrary 4xx. Assert no-store, no redirect, no token fields, no sensitive response material, exact expected error and selected native error identity where it distinguishes the tested boundary. Never print raw responses or native row values on assertion failure.

Each token denial starts with a genuine eligible, comfortably unexpired code and independently captured native token/audit snapshot. Assert no added/deleted token rows, unchanged audit history and the exact code still valid/unredeemed; this is an early validation boundary, not a universal native rollback guarantee. Finish with a new successful, independently validated flow, never retry a denied code exchange. Authorization/HTTP transport probes must not create interactions or native issuance state. Positive controls prove the allowed methods and header-invariant flow remain usable.

## Fixture and execution bounds

Use a dedicated serialized transport collection with one disposable Aspire environment per selected provider. Reuse existing fixture cleanup/invalidation and real protected form helpers. A narrowly keyed test-fixture option adds a named HTTP listener only to this collection's test environment; production AppHost stays HTTPS-only. No insecure TLS client, auth shortcut, mock database or middleware override is permitted. Preserve the 600/minute and 16-concurrent request policies, all existing tests, 660s invocation deadline and nine 90% line / 80% branch coverage floors. Reuse genuine sessions within a case to avoid redundant password work; do not reuse expired codes or silently retry failures. Record measured counts/durations before acceptance.

## Source-predicted conflict and conditional repair

Independent security/test review found that native `ExtractBasicAuthenticationCredentials` overwrites body `ClientId` after validating the enabled authentication method. A valid Basic header plus a different body `client_id` alone may therefore succeed, contrary to the approved pilot contract. This is source reasoning, not yet an executed defect. First run the dedicated real-provider regression unchanged against the current product. If reproduced, add a narrow feature-local native extraction event guard that compares the original form identity with the library-decoded Basic identity and rejects a conflict through OpenIddict. Preserve native parsing, secret validation, token processing, response generation and protocol error ownership. Independently review its ordering, matching/absent-body controls, malformed inputs and sanitized diagnostics before accepting it. Any other defect must also be reproduced and documented before a scoped correction.

## Delivery tasks

- [x] TR-A Finish independent architecture/security/test design review and resolve findings.
- [x] TR-B Implement credential/grant/transport/header tests and the isolated fixture; reproduce any predicted failure before a product correction.
- [ ] TR-C Resolve source reviews; run static/build, strict discovery/count checks, local available-provider regressions and required final CI on both providers/browser cells, mutations and combined coverage. No skipped-as-passed acceptance.
- [ ] TR-D Reconcile T031/T018 and current matrix/roadmap according to actual scope; retain T032, T021/manual/M01-E/M01-F and external gates. Publish, inspect reviews and exact final head, fix findings and normal squash-merge under a Conventional Commit title. Final-head delivery evidence belongs on the PR to avoid a self-referential commit.

Assurance references remain candidate evidence; this work does not assess ASVS controls or claim certification. Independent reviewers report source reasoning separately from executed evidence. The lead owns edits and integration.

Design review disposition: independent architecture, security and test reviewers accept the bounded plan. Their conditions above include native multi-value identity preservation, exact HTTP error ID2083, case/percent-decoding and form-charset controls, keyed listener isolation and manual redirect validation. Native StartsWith media-type recognition may pass a malformed form suffix to ReadFormAsync and produce 503; add both endpoint regressions before any narrow extraction-event guard, rejecting malformed media through the native context while preserving valid media parameters/case. These are source predictions until executed.

## Before-fix local evidence

The unchanged production source at PR #13 was exercised with all 49 new PostgreSQL cases: 44 passed, five failed, zero skipped, in 100.791s. Three rows (different body ID, case-different body ID and duplicate conflicting IDs) issued access/ID tokens that passed independent issuer/audience/signature/subject/nonce validation. Both malformed form-suffix rows returned 503 instead of a native client error. These are executed regressions, not accepted results. The separate SQL Server baseline remains required through normal CI; the local SQL image redirect remains outside this environment's network policy.

Initial test-source review corrected Basic-credential canaries, response cleanup on failed navigation assertions, all-outbound destination validation, cookie Path/SameSite checks and explicit normal-fixture/AppHost listener absence checks. Strict MTP discovery reports 49 new HTTP cases per provider, taking each HTTP issuer partition from 206 to 255; existing browser counts remain 29 per cell. No product correction has been applied at this checkpoint.

## Correction and local verification

Two feature-local extraction guards preserve library ownership. `Token/ClientIdentityConsistency` captures the original native parameter immediately before Basic extraction and compares it immediately afterward; native equality retains ordinal and duplicate-value distinctions. `Protection/FormMediaType` rejects only POST content types whose native form prefix would pass extraction but whose actual media type is not a supported form. Both use native rejection/response handling and relative native handler ordering with the HTTP-request filter. No grant, authentication method, middleware, Program, deadline or rate-limit policy changes.

Independent architecture and security reviewers accepted the corrected source; the test reviewer confirmed all initial source findings were fixed. Reviews are source reasoning, not executed tests. Corrected local PostgreSQL passed all 49 transport cases with zero failures/skips in 65.948s (native test duration; outer runner 66.398s). The immutable pre-fix tree was published in PR #14 at `0b953ad`; an empty CI-trigger commit `c65c684` retains the identical tree for [baseline run 38157375368](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38157375368). SQL baseline and full corrected-head CI remain pending at this checkpoint. Existing seven isolated critical-decision mutations remain mandatory; the new guards additionally have the recorded failing-before/passing-after real HTTP evidence.

Local verification also passed the strict 243-case unit/architecture matrix with zero skips. Full solution build, whitespace/import checks and static-analysis enforcement controls passed: the real build rejected seeded CA5350, IDE0055 and IDE1006 defects and accepted their clean controls. An initial import-order failure was corrected before that clean run. Assurance integrity retains all 345 controls as not_assessed; the eight skills/eight reviewer-configuration integrity check passed. These do not replace required final CI.

The full corrected local PostgreSQL partition at product source `03a9d07` passed 263/263 with zero skips: Persistence 4, Aspire lifecycle 4, HTTP issuer 255 (449.668s, within the unchanged 660s deadline). Strict discovery/execution identity checks passed. Pre-fix CI PostgreSQL at `c65c684` likewise executed all 255 issuer cases: 250 passed, exactly the five predicted transport defects failed, zero skipped/aborted; the new subset was 44/49. Its issuer duration was 403.799s. These CI failures remain deliberate before-fix evidence, not acceptance.

SQL Server baseline in run 38157375368 completed all 255 issuer cases in 566.974s: 250 passed, the same three independently validated identity-conflict issuance cases and two malformed-media 503 cases failed, zero skipped. Persistence 4 and Aspire lifecycle 4 passed. Both-provider reproduction is established; the corrected source now proceeds to full CI. All six baseline browser cells, both existing mutation jobs and baseline non-provider checks passed; combined coverage was skipped because the intentionally failing provider jobs did not qualify. No baseline pass is claimed for the whole pipeline.
