# Catalogue interfaces and acceptance matrix

## Management and browser surfaces

Real URL-segment major version1; Admin bearer and BFF cookie/CSRF contracts remain
separate. `/scopes` and `/audiences` each expose bounded GET collection, POST create,
GET detail, PUT edit and DELETE unreferenced detail. Browser adds native antiforgery
acquisition. Application scope assignment is an additive GET/PUT subresource using
the current application version and applications.read/write authority, with fresh MFA
for policy changes. No existing editor DTO becomes breaking or silently clears scopes.

Editable detail/assignment GET emits strong ETag; PUT/DELETE require exact If-Match.
Missing428, stale412, malformed400, missing resource404, current permission403,
unauthenticated401, duplicate/referenced409, safe transient503. Successful create201,
edit200, deletion admission202 with a job id/status URL. All sensitive policy projections use no-store. No response returns
native secret hashes, token contents, private snapshot data or audit internals.

Deletion is a durable worker operation. Admission requires current write authority,
fresh MFA, exact version and no current references; the worker rechecks actor authority,
original approval deadline, target version and both current/retained references under
governance before atomic delete/audit/completion. No automatic reapproval after expired
authority or stale version. Interrupted jobs recover idempotently from persisted state;
UI displays queued/succeeded/conflict/denied/expired status without claiming deletion
from202 alone. Finite status read remains permission protected.

No arbitrary route forwarding, permission string, native property bag or claim name.
One immutable typed intent is validated before any send; finite action/path pairs,
strict bounded JSON (unknown/duplicate members rejected), current same-origin browser
binding and native CSRF. No automatic mutation retry on ambiguous output.

## Protocol

Existing managed code/S256/password/MFA/explicit-consent flow remains the only enabled
ordinary client profile. Openid-only defaults remain unchanged. Additional scopes must
be explicitly assigned; authorization grants their requested/allowed/eligible/consented
intersection and at most one mapped API audience. Unsupported/unassigned/disabled scopes
reject instead of being silently accepted. Scope policy is bound through cached request,
consent and code exchange. Fresh current policy is checked after serialization waits.

Email policy releases exact current email plus boolean email_verified to ID token and
UserInfo, never as API authorization claims. No email means neither field. Access token
contains only required native protocol/scope/audience claims; internal policy bindings
have no public destination. No arbitrary profile properties are inferred.

`/connect/userinfo` accepts GET/POST and genuine native access tokens; no query token
fallback. Native validation and current account/application/token/authorization/policy
are required. Response exactsub must equal associated token subject. Only consented
current identity claims are returned. No machine/fixed-admin/pilot profile is admitted.
Native errors, no-store and original request budgets remain. Fixed BFF discovery accepts
only the exact expected endpoint and retains both no-UserInfo-send guards.

## Required scenario groups

| ID | Behavior | Requirements |
| --- | --- | --- |
| C01 | CRUD/list bounds, ordinal identities, duplicates, reserved rows, strict DTO/version | FR-001/003/004/011 |
| C02 | Current read/write permission, removed role/account/session, fresh-MFA equality | FR-002/011 |
| C03 | Native manager/product/audit atomicity, suppressed save, commit/disposal/cancellation | FR-002/004/013 |
| C04 | Concurrent stale edits, assignment/deletion, resource deletion/reference races on both processes | FR-003/004/007/013 |
| C05 | Fresh and populated provider upgrades, exact old histories/data, second run, runtime grants | FR-013 |
| C06 | Explicit application assignment and unchanged old/system profiles; contract compatibility | FR-007/011 |
| C07 | Requested/assigned/enabled/eligible/consented intersection; maxone audience; reserved claims | FR-005/006/008 |
| C08 | Cached/consent/code policy drift, cross-process native cache, claim expansion/narrowing | FR-008/009 |
| C09 | Independently validated native signatures/types/audience/scopes/claims; first replay and unrelated success | FR-005/006/009 |
| C10 | UserInfo GET/POST, exactsub, present/absent/unconfirmed/changed email, current policy/account denials | FR-006/010 |
| C11 | Wrong type/profile/client, expired/revoked token/authorization, forbidden machine identity, HTTPS/current authority | FR-009/010 |
| C12 | BFF exact discovery pin and zero optional UserInfo sends with existing real login/logout | FR-010/013 |
| C13 | Visible catalogue/assignment journeys, light/dark/reflow/keyboard/axe/errors; no token/PII leakage | FR-011/012 |
| C14 | Both-provider full regression, preserved identities, original coverage inputs/floors and behavioral mutations | FR-013 |

Bind actual discovered native test identities and original evidence in verification.md
before acceptance. Scenario labels are plans, not fabricated test names or passes.
