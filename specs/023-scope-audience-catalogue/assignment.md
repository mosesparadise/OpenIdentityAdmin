# T008 application scope assignment

Continuation from main `6e015a4a7b6edf3fe119b1d8fbb78dd2c9f442dd`, 8 November 2026.
The user requested the next recommended task. T008 implements explicit application
assignment; T009–T014 protocol policy, consent, claims and UserInfo remain separate.

## Contract and behavior

Admin API `GET/PUT /api/v1/applications/{id}/scopes` and the fixed versioned BFF
counterpart use applications.read/write respectively. PUT requires fresh MFA, native
browser CSRF, one canonical application If-Match and strict JSON `{ "scopes": [...] }`.
Names are ordinal, unique and bounded to 32. Empty explicitly removes all additional
scope permissions. Every successful PUT, including an unchanged set, advances the
application revision once and commits exactly one ApplicationScopesAssigned audit.
Credential, callbacks, lifecycle and fixed protocol permissions remain unchanged.
GET and PUT return applicationId, sorted scopes and bounded expiresAt, with strong
ETag and no-store. Existing editor DTOs stay unchanged. Invalid framing/input is400,
missing precondition428, stale412, missing application404, current denial401/403,
unsupported application/scope policy409 and unavailable503.

All selected names resolve through current native managers and managed catalogue
policy under governance serialization. Missing, unmanaged, reserved, disabled scopes
and disabled/missing audiences reject. Several audiences may be assigned: the approved
one-audience restriction applies to each later authorization. Freeze and verify scope
and audience snapshots after saves; exact native permission verification detects lost,
added or substituted assignments. Assignment and catalogue deletion share lock order.

Management accepts only the fixed original profile plus bounded scope permissions.
Ordinary edits, lifecycle actions, credential replacement and grant cleanup preserve
those permissions, including disabled scopes that must remain removable. No separate
authoritative assignment table or migration is introduced.

## Intermediate protocol boundary

The exact legacy issuer profile remains unchanged until T009–T011. A nonempty
assignment therefore blocks that application's sign-in and token issuance, including
openid-only requests. The editor must explain this before save and in persisted status.
Clearing assignments restores profile eligibility subject to current lifecycle,
credential and revision checks. Untouched applications retain their existing behavior.
No new consent, claims, grant revocation, refresh or UserInfo activation is implied.

## Required verification

C04/C06/C07 and FR007/011/013 require both-provider assignment/removal, exact audit and
native state, current permission/MFA, stale/version and delete races, rollback/publication
faults, old editor preservation, protocol closure and unchanged unrelated success.
Host/contract/BFF tests cover strict framing, exact one-send destinations, original
browser authority and output binding. UI and retained browser journeys cover explicit
selection/removal, saved-state warning, stale/denied/ambiguous results, themes, reflow,
keyboard and accessibility. Existing discovery identities, mutation controls and module
floors remain mandatory. Implementation and evidence are pending; T008 stays unchecked.
