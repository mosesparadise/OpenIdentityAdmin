# Management and browser contracts

## Clone
POST /api/v1/applications/{id}/clone; fixed same-origin BFF mirror under /bff/api/v1.
Require exact source If-Match. Body fields: confirmed, clientId, displayName,
credentialExpiresAt. Reject missing/duplicate/unknown/wrongly typed/oversized input using
strict existing bounded readers. Require current applications.write and fresh MFA under
original ownership/publication budget; BFF cookie/antiforgery remains independent.
201 uses existing ApplicationCredentialOutput, new destination ETag, enabled=false and
fresh one-time secret. Source ID differs from destination; response validation binds
destination client ID/name/expiry, revision 1, enabled=false, supported profile and
new-ID/source-ID inequality rather than requiring source ID/+1 revision. No plaintext replay or auto retry.
400 invalid; 403 authority/protected; 404 missing; 409 duplicate/unsupported/ineligible;
428 missing precondition; 412 invalid/stale version; existing finite unavailable outcomes.
UI reviews details/assignments for one matching source version, exact read-only callbacks/
scopes, explicit new identity/name/expiry/confirmation. Cancel restores focus. Success
explains disabled state/separate enablement. Hide/expiry/navigation clears secret.
Unknown outcome directs lookup/explicit replacement.

## Retirement
POST /api/v1/applications/{id}/retirement and fixed BFF mirror; exact If-Match.
Body is exactly {"reason":"security","confirm":true}; reason accepts existing exact
maintenance/security/configuration vocabulary. A single Idempotency-Key header is a
nonempty lowercase D-format UUID. No jobVersion is accepted on retirement admission. Require current
applications.disable AND grants.revoke plus fresh MFA. Replay scope binds actor/action/
application/key and exact original version/body.
202 returns {applicationId, applicationVersion, retiredAt, requestKey, job}, where job uses the
existing GrantJobView shape. ETag is the job ETag (matching grant status semantics);
applicationVersion is the new application ETag in the body. Location addresses the existing
application grant-job status route. The immutable receipt binds the original admission
application version, while current status may expose a later job revision.
Accepted means permanent retirement and admitted revocation, not completed revocation.
Incomplete prior job conflicts and offers existing authorized recovery; no silent adoption.
Normal list excludes retired apps. A separately navigable Retired applications view uses
GET /api/v1/retired-applications (and fixed BFF mirror), existing bounded page/prefix
parameters and applications.read authority. This view returns only retired managed rows
and makes detail/status/recovery discoverable after full reload, re-login or response loss.
Authorized terminal details expose retired=true, editable=false, enabled=false, existing
managed profile/credential metadata, current strong application ETag and bounded retirement
job reference. Preserve the strong ETag for explicit grant continuation. Protected/unmanaged
noneditable projections remain distinguishable and have no such ETag. BFF/UI parsers must
validate these distinct shapes. Terminal details render the grant panel independently of
ordinary editability; the requestKey echo binds retirement acceptance to the submitted key. All ordinary
mutations/clone-source attempts conflict, even after successful revocation. Client ID stays
reserved. UI explicitly explains retained history, offline token expiry and remote sessions.
Cancelled/stale/denied/pending/stopped/resumed/unknown outcomes remain distinguishable.

Authoritative OpenAPI stays in feature-owned endpoint metadata/transformers. Regenerate
documents/TypeScript with existing tooling; this design is not a second executable contract.
