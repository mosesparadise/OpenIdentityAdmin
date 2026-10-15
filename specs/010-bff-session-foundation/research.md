# M02-B dependency decisions

15 October 2026. Read-only review of main `5f4fce7fe0c7761e6eab7308f2cd38908937af51`, PR #50 head `6fcb96216eca0974041d3e8346adc4ce8458789d`, approved v0.2 architecture/protocol/data contracts, current roadmap and feature 008 delivery evidence.

## Existing foundation

PR #48 delivered internal administrator governance as squash `f499518`; PR #49 recorded it as `5f4fce7`. Bootstrap, administrator MFA/recovery and current-permission/last-administrator services exist. Wider human/overall acceptance remains incomplete.

`AdminSessionDbContext` sets the `bff` schema, and `PersistenceRegistration.ConfigureDatabase` applies `UseOpenIddict()`. Both initial Sessions migrations already create native OpenIddict applications, scopes, authorizations and tokens. There is no product administrator ticket entity/store. Preserve native state-token machinery; the schema is not empty.

BFF currently registers session persistence, health, contract tooling and dormant documentation settings, without a browser authentication/client pipeline. Existing `sessions_runtime` grants and persistence tests establish BFF-state access and identity-store denial. New tables still require actual upgrade/grant verification.

## Decisions

| Decision | Rationale / alternative rejected |
| --- | --- |
| Complete internal durable ticket store first | Observable restart, expiry and race behavior independent of issuer login; a lone crypto/lifetime helper does not deliver the store outcome |
| Native ticket serialization and existing native state tables | Avoids a replacement authentication/state-token implementation; client integration follows later |
| Separate BFF ring/application/purpose and ownership | Approved ADR-06; reject AuthServer private-handler dependencies and shared cookies/rings. Narrow reusable Infrastructure mechanics may be extracted without sharing credentials. |
| Random store-owned handles, hashed lookup and authenticated envelope | Avoids caller-controlled fixation and cross-row substitution; no OAuth token or row key becomes the session credential |
| Portable revision-checked updates and irreversible revocation | Both providers; no provider-only rowversion or automatic mutation retry. Does not solve OAuth refresh coordination. |
| Session-owned atomic audit intent | BFF retains session-only access. Commit minimal intent in its own transaction; no platform business/audit writes. Export remains separate. |
| 30-minute idle/eight-hour absolute maxima | Approved protocol defaults, with shorter limits allowed; renewal cannot reset original absolute lifetime |

## Sequencing matrix

| Dependency | Verified state | Effect |
| --- | --- | --- |
| Internal governance | Delivered in PR #48 | Available internal dependency; no HTTP activation |
| BFF schema/native state tables | Present on both providers | Add product ticket/audit tables through additive migrations |
| Dedicated protected BFF keys | Missing | First store slice includes ownership and startup/restart tests |
| Administrator protocol admission | Missing; `PilotEligibility.UserAsync` rejects MFA-enabled users and users with roles | B2 needs a distinct current-account/session/MFA path; do not relax pilot |
| Code/S256/PAR/client authentication | Server enables code/S256 and `client_secret_basic`; no PAR endpoint | B2 must prove native client/PAR/`private_key_jwt` compatibility; package presence is not proof |
| Refresh/end-session | Neither grant/endpoint is configured | Explicit B3/C issuer work; local revocation is not remote token revocation |
| API/BFF/React access | Unimplemented/unactivated | B3/C/D/E; readable ticket is not current authority |
| Six findings / 48 human rows | Open / `not_run` | Acceptance/public-release blockers remain; no AI replacement |
| PR #50 source | Refactor implemented; required CI failed | No BFF code dependency; do not repeat its implementation |
| Witness allowance on main | Expires 2026-10-15; `Allowances.Validate` rejects expiry on or before UTC today | Shared executable-delivery blocker: retire through reviewed source and passing gates. No renewal/copied allowance/skipped gate. Source inspection only; no new gate execution is claimed. |

## Authority and delivery

The latest user instruction advances M02-B scope preparation and dependency resolution under approved v0.2. No A-004/A-005/A-006 renewal or runtime activation acceptance follows. Ordinary implementation decisions within approved architecture do not require an invented approval cycle; material changes retain AGENTS.md's concrete-decision requirement.

Planning can be delivered through existing prose checks. Executable changes receive full applicable verification. Independent implementation can be prepared, but a clean executable merge from today's main cannot be promised until the expired shared allowance is actually retired. This planning PR supplies no substitute product acceptance.

## PR #50 investigation limit

The [current disposition](../009-engineering-standards/witness-ci-disposition.md) preserves failed required CI and both passing hosted diagnostics. Further native diagnostics are paused. The remaining proposed comparison needs a reviewable hypothesis, exact historical path/configuration/unique-ID bindings and a single-run stop condition before launch. A pass does not automatically lead to another experiment or full-CI retry. Corrected-source acceptance and normal squash remain the delivery target.
