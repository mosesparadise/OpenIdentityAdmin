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

## Validation checkpoint — 8 November 2026

Source `ba8fb3456597c464d1c8a8afd52f66d21d8f8844` is preserved on branch138.
Isolated branch139 adds only its validation workflow and must not be merged.
Focused run42434182462 retains all four failed browser receipts: PostgreSQL and
SQL Server each pass24 non-browser cases; the new browser journey times out on
both retained engines. This is not acceptance. The reviewed correction reopens
normal navigation after the reused secret-erasure helper clears the page's session
view, adds explicit stale-save focus recovery, and improves bounded source-line
failure diagnostics. The original failed run is retained.

Local checks pass1,905 host cases,44 native contract cases,46 Node contracts and132
UI cases. The new UI controller/client/validation modules meet unchanged95% line,
90% branch and100% function floors. A first transport run correctly rejected an
unknown JSON field but failed its incorrect exception expectation; the corrected
13-case report passes. Initial Node execution lacked the pinned diff tool; after
installing it from its SHA256-verified retained archive and updating the explicit
additive operation inventory, all46 cases pass. No failed attempt is acceptance.

Actual native discovery preserves all2,871 prior administrator cases and adds52,
for2,923 total. Both-provider ordinary counts are1,333/1,438; the browser story adds
one case per provider/engine, for76 per provider/engine. Host discovery preserves
all1,892 prior identities and adds13. A new exact-permission mutant is included in
the existing catalogue suite, with one explicitly selected control per provider;
its runtime kill and every required full-CI/coverage gate remain pending.

Architecture and UX source reviews closed the bounded-read, async expiry,
unknown-commit wording and focus findings. Test review tightened persisted race
oracles, mutation evidence and provider-operation joining. The new observed
assignment/deletion contention and corrected browser journey still require hosted
execution. T008 remains open; no main merge or historical/external gate closure is
claimed by this checkpoint.
