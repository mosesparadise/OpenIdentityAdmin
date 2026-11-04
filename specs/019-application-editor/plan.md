# Implementation Plan: Application creation and editing

**Branch**: `123-application-editor` | **Date**: 2026-10-29 | **Spec**: [spec.md](spec.md)

## Summary

Deliver real confidential-web registration, sanitized details, concurrency-safe editing and explicit secret replacement in the existing workspace, plus the necessary ordinary-client issuer path: code/S256, OpenID-only identity, fresh password/MFA on existing administrator accounts and explicit consent. A stored record without a proven issuer journey is not completion. User selected all three recommendations; no repeated implementation permission is required.

## Technical Context

- .NET 10/C#, locked OpenIddict 7.7.2/EF Core/native Identity, existing React/TypeScript and generated browser client.
- Existing extensible relational providers; additive Platform migrations on both PostgreSQL and SQL Server; no Sessions schema change expected.
- Native Aspire disposable Development composition, genuine MTP/xUnit/provider/browser/contract tests; Chromium/WebKit under existing A-009 boundaries.
- Native storage retains original 10-second request/5-second transaction/5-second cleanup bounds. Management requests link these to the existing original five-second API budget and authentication deadline; no retry or deadline restart. Human interaction has one original five-minute expiry.
- One managed profile, openid only, stable subject plus necessary OIDC protocol metadata. Native code exchange may return an access token but it has no management scope/audience and cannot authorize Admin API requests. No refresh/UserInfo/API-resource claim is made.

## Constitution Check

Approved FR-01/02/03/07/08/09/11/12/16/17/19/21/24 and current user choices authorize this slice and its prerequisites. Preserve native protocol validation/cryptography and manager ownership, exact browser origin/CSRF, Features organization, provider equivalence, unchanged prior identities/coverage, original deadlines and all external gates. Keep system registrations and administrator proofs separate from managed registrations. No speculative generic repository or alternate auth stack. Independent architecture/test/security design review and native caching characterization precede affected integration; root owns edits and review disposition.

## Project Structure

- `src/core/IdentityPlatform.Core/Features/Applications/`: finite managed input, safe DTOs, profile/expiry/redirect/version contracts.
- `src/persistence/IdentityPlatform.Infrastructure/Features/Applications/`: product policy/model, native aggregate/manager mutation, version/audit and owned current actor observation.
- `src/persistence/IdentityPlatform.Infrastructure/Features/Identity/ManagedApplications/`: exact-purpose fresh authentication, interaction capacity/state, consent and current account/factor observation; shared verification below administrator-session creation.
- `src/backend/IdentityPlatform.Admin.Api/Features/Applications/`: details/create/edit/secret-replacement endpoints and typed owned current authority; existing ApiRead remains rollback-only.
- `src/backend/IdentityPlatform.Admin.Bff/Features/Applications/`: native bound antiforgery token endpoint, finite JSON proxy and credential-safe transport projection.
- `src/backend/IdentityPlatform.AuthServer/Features/Protocol/ManagedApplications/`: native cache/authorize/exchange admission, current policy, minimal principal, durable consumption, issuance and publication receipts.
- `src/backend/IdentityPlatform.AuthServer/Features/Accounts/ManagedApplications/`: password/MFA/consent pages with separate correlation purpose and no administrator/BFF cookie establishment.
- `src/frontend/IdentityPlatform.Admin.Ui/Features/Applications/`: create/details/edit/replacement forms and one-time output, generated transport and finite recovery states.
- Provider-owned migration assemblies/descriptors and corresponding Host/Administrator/AppHost/contract/frontend tests; exact inventory/coverage/mutation updates.

## Management transaction and credential design

Initial create uses current applications.write. GET details uses current applications.read. PUT uses applications.write and exact aggregate ETag. Replacement uses applications.credentials plus persisted MFA freshness within five minutes; it requires explicit confirmation and current ETag. PUT never accepts credential-expiry or secret fields. Create/replacement require an explicit future UTC expiry, no default, rechecked under locks and immediately before commit. Generate a cryptographically random secret, hash using the native manager, return once, never retain plaintext for replay. Lost output requires fresh details and explicit replacement; no automatic resubmission.

Use ProtocolStorageRequest/fresh scope/Owned selection/ProtocolStorageTransaction. Acquire governance → administrator profile → actor-account locks through existing mechanisms before current eligibility/grant reads. Application mutations and issuer admission share governance serialization initially; do not add reverse lock order. A private mutation receipt binds current actor, action, expected aggregate version and original expiry. Native manager saves, policy/version and one sanitized audit intent commit together. Verify after native internal saves; publish only after actual cleanup. Reject fixed system client IDs and persisted system application IDs. Concurrent duplicate/stale operations cannot add successful audit intent.

## Native issuer and authentication design

Enable a separate managed profile only in the explicit Development graph. Keep Owned native stores. The installed native authorization-request caching pipeline owns request serialization, reference tokens and redirects. Initial anonymous ProcessSignIn caches one validated request and no authorization; it requires a cache-specific publication receipt, not endpoint passthrough. Resume restores native parameters, then binds the exact application/revision/request-token/typed demand/browser/original expiry. Managed policy allows only cached-authorization subtype, exact code/query/S256/openid and native client_secret_basic. Preserve fixed BFF PAR/private-key proofs and pilot behavior.

Use a separate managed interaction and fresh password/MFA purpose. Reuse account locking, native password policy, factor verification/replay and budgets, never JourneyCompletion.Grant or AdministratorTicketPermit. Managed completion creates no AdministratorSession, BFF ticket or administrator cookie. Consent is explicit protected POST for the exact verified subject, client, configuration and scope; GET grants nothing. Existing issuer cookies cannot replace the fresh ceremony.

Managed capacity is separate from B3: one original 300-second human/cache deadline, 8 live interactions per browser, 128 globally, 10,000 retained interaction rows, cleanup batches of 100. Reserve under the common serialization lock before native cache creation, then bind the actual created token ID before commit. Retain tombstones for at least 14 days and beyond every associated native artifact deadline plus configured validation skew and the existing 30-second rollback allowance. Expired unfinished interactions become terminal without reopening authentication. Each native creation also reserves exact managed-owned token/authorization/request deltas against finite historical limits; interaction counts alone are insufficient. Equality/overflow, concurrent admission, lost initial redirect and safe unfinished cleanup are required provider tests. These are ordinary initial implementation bounds derived from existing issuer policy, not modifications to B3 capacity.

Final authorization uses a fresh native response child and current locked policy/account/factor checks. Native token/code/authz creation, one-use consent consumption and sanitized audit share commit; seal callbacks and dispose before publication. Native cached-request redemption/replay effects run in an earlier durable child before final authorization code issuance; password/MFA/consent display does not consume it. Native code replay checks/redemption likewise run in an earlier durable child before final exchange issuance. Later denial, audit failure or issuance rollback cannot resurrect either artifact. Recheck application aggregate revision, credential expiry, current source account/factor generation, native token/authz and original deadlines. Construct a new identity-only principal; never clone an administrator principal or assign management scopes/resources. Native error redirects require proven validated callback provenance.

## Failure and verification boundaries

Exact malformed-field/precondition/unsupported-profile rejection; duplicate and same-version races; native-save/audit rollback; source/credential expiry under actual lock waits; no-write/read-only and current grant removal; anti-forgery cross-browser/generation/origin negatives; system-binding substitution; genuine credential validation before/at expiry and after replacement; request/consent/code replay and configuration drift; no administrator session/cookie/claim growth; actual create/edit/sign-in visible journeys and native Aspire initialization on both providers. Preserve all 2,053 administrator metadata records and prior frontend identities; introduce exact new inventories and behavioral mutation oracles without relaxed gates.

## Complexity Tracking

Extract only shared verification/transport observation where genuinely reused; keep profile-specific capabilities typed and separate. Review 40/300 limits, native callback ownership and documentation with each affected change. No new legacy allowances. Final source/test/UX/evidence reviews and required exact-head checks precede normal squash.
