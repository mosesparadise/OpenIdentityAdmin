# Application editor prerequisite research

## Verified checkpoint and authority

Remote main `4c9fb24f003ecfdeb4b4b4d438ce7c847f3780fa` preserves PR #118 implementation and PR #119 delivery records. Source work starts on `123-application-editor`. The current user request continues the already approved visible-first application creation/editing sequence in `docs/approval.md`; it does not authorize unrelated activation or release.

Feature 018 remains accepted: 100 job obligations with explicit retained-success/bounded-rerun provenance, 4,869 ordinary passes, 20 separate frontend cases, 94 accepted artifact bindings, 78 coverage inputs/15 floors and 68 mutation controls. Its startup exit-255 cause remains unknown. No old implementation or diagnostic approval is repeated.

Managed-environment observations are current and the restricted HTTP policy is enforced, with both MCR layer hosts allowed. No configured credential bindings or VPN are reported. Existing source, caches, worktrees and evidence are preserved. No application test or database experiment has run for feature 019.

## Approved decisions, not open questions

- Approved PRD FR-01/02/03 and its “Register a web application” journey establish confidential web/code/S256, exact redirects, immutable unique client IDs, supported scopes and native manager credential handling as the concrete R1 journey. The current questions narrow the initial increment and its missing dependencies; they do not reopen protocol weakening.
- `docs/approved-v0.2/04-data-api-contracts.md` requires details ETag, If-Match updates, 428/412/409 distinctions, current `applications.write`, separate credential permission and atomic sanitized audit. Do not request repeated approval for those invariants.
- `docs/roadmap.md` M04 requires real offered protocol capabilities and registration-to-token evidence. A newly inserted native row is not a demonstrated usable client.

## Confirmed source dependencies

| Boundary | Current source | Consequence |
| --- | --- | --- |
| API authority | `Admin.Api/Features/Authentication/ApiCurrentAccess.cs` hardcodes `applications.read`; `ApplicationListOperation.cs` uses `CeremonyTransaction.OpenApiReadAsync` | Introduce explicit mutation ownership with current write grants under the committing transaction. Preserve rollback-only read mode and its mutation-kill control. |
| Native ownership | `Infrastructure/Features/ProtocolStorage/ProtocolStorageTransaction.cs`, `ProtocolStorageSelection`, owned native stores; Migrator `AdministratorRegistrationUpgrade` | Reuse stable ownership mechanisms, proving manager internal saves use the exact owned context and rollback with audit failure. A registration upgrade receipt is not a generic mutation/idempotency service. |
| Issuer admission | `AuthServer/Features/Protocol/Hosting/ProtocolDecisions.cs`, `Ownership/ServerStoreSelection.cs`, `Policy/PilotEligibility.cs` | Only fixed pilot/administrator registrations are currently admitted. Add the selected ordinary-client path before claiming actual sign-in. Never copy management scopes or administrator claim destinations. |
| Scope/user policy | `PilotEligibility` pins one pilot user/manifest and exact `openid` plus `pilot_api`; administrator policy is separately fixed | Ordinary scopes/audiences, permitted users and consent need the selected prerequisites; current seed data is not a general catalogue. |
| System registrations | `AdministratorRegistrationPolicy` verifies exact descriptor/manifest material; pilot has fixed native bindings | Generic CRUD must reject reserved client IDs and persisted system IDs. Do not loosen existing manifests to accommodate arbitrary registrations. |
| Browser writes | `BrowserRequestPolicy` is form-POST oriented; `BrowserAntiforgeryData` requires trusted preloaded `BrowserRequestBinding` | New JSON endpoints need real session/generation preload and antiforgery/origin enforcement, not only declared header metadata. |

## Initial independent review dispositions

Architecture and test reviewers independently confirm the above dependencies. No implementation or runtime acceptance is claimed. Their required tests include genuine current-permission removal, same-version and duplicate races, native-save/audit rollback, protected-system substitution, cross-browser/stale antiforgery, malformed bounded input, original-deadline/cancellation and ambiguous response loss, credential secrecy, and real React → BFF → API → issuer journeys on both providers.

The user has three pending scope questions recorded in the specification. Dependent issuer/profile/credential implementation waits for those answers. Independent prerequisite research and specification work continue. Credential expiry/initial issuance/recovery details must be resolved in the chosen profile's plan before affected implementation; existing replacement safeguards must not be weakened.

Final independent architecture review passes this planning checkpoint only. The approved create contract uses `applications.write`; replacement explicitly requires `applications.credentials` and recent MFA. Any stronger initial-issuance policy must be identified as a proposed choice, not claimed to be already mandated by that create contract. Shared-secret expiry remains a selected-profile planning obligation.
