# Data model and transitions

## Clone
Input: confirmed=true, new clientId/displayName and exact future UTC credentialExpiresAt;
source ID in route and canonical resource-bound If-Match. Existing identifier/display/expiry
limits apply. Source supplies 1..10 canonical HTTPS callbacks, each at most 2048 characters,
and finite assigned scope names. No credential/grant/permission/lifecycle input is accepted.
Destination: new native ID/client ID/hash through OpenIddict manager; policy revision=1,
credentialRevision=1, new issued/expiry/observed times, enabled=false. No grants/tokens/
consent/interactions are copied. Only immediate success returns the new plaintext secret.
Frozen source binds native ID/profile/configuration/hash and all policy fields. Catalogue
observations bind current scope/audience eligibility. Verify all after native/audit saves.
ApplicationCloneAuditDetail: audit ID PK/FK to SecurityAuditIntent, source application ID
and canonical source version. Immutable evidence with exact persisted verification.
Additive migrations preserve all existing rows in both providers.

## Retirement
ApplicationPolicy gains non-null Retired bool, default false, concurrency token.
Database invariant: retired implies enabled=false. Existing enabled states/revisions survive.
ApplicationRetirement is immutable and unique per application: original/retired versions,
actor, finite reason, accepted time, scoped request/request-content hashes, replay deadline,
audit ID, grant job ID and original Submit approval ID. Use existing bounded IDs, grant canonical hash encoding/replay
lifetime. Restrictive policy/audit/job/original-approval references preserve history; no native delete.
Atomic transition: supported non-retired app with no incomplete job -> retired=true,
enabled=false, revision+1, receipt/audit and newly approved queued grant job.
Exact same-key replay within the established window returns the same receipt; changed
body/version binding conflicts. New requests against retired state conflict. Failed
admission leaves all rows unchanged.
Queued/running -> succeeded/stopped; explicit approved continuation requeues eligible stopped
work. None clears retirement. Existing native grants are revoked, not removed.

## Reads and invariants
Exclude retired policies before native ordering/offset/lookahead. Preserve unmanaged/system
visibility. Authorized details include terminal state and bounded receipt/job reference;
editing is disabled while recovery remains accessible through a bounded retired-app list
and terminal detail grant panel after reload/re-login. Client ID stays unique, assignments
remain catalogue references. Issuer snapshots bind retirement and deny later issuance/
current UserInfo; offline token expiry and remote sessions retain their documented limits.
