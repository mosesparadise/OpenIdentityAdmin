# Design decisions

## Clone authority and state
Decision: applications.write plus fresh MFA, exact source If-Match, explicit confirmation,
new client ID/name/expiry and review of callbacks/scopes; disabled destination with fresh
credential. Existing read authority gates UI preview; mutation follows existing write authority.
Rationale: assignment already requires write and fresh MFA; managed policy requires a
credential hash/revision and valid dates. Configuration is distinct from user consent.
Alternatives: credentialless draft requires a new profile/schema; silently dropping scopes
loses reviewed configuration; immediate enable removes a deliberate activation step.

Decision: new allowlisted descriptor and shared native creation; freeze source/destination
and resolve/verify ApplicationScopeSelection inside governance ownership. Add immutable
source-ID/version detail to one new ApplicationCloned audit targeting the destination.
Rationale: copying a populated descriptor risks copying hashes/settings. Verify source,
catalogue and destination after saves. Never copy dates, revision, grants or history.

## Retirement and references
Decision: irreversible retired policy, enabled=false, retained native identity/client ID,
immutable receipt linking original version, actor/reason, replay identity, audit and new job.
Rationale: native grants and restrictive job/audit FKs remain after revocation; successful
revocation is not absence of references. The user selected retained history.
Alternatives: purge changes retention; ordinary disable permits later re-enable.

Decision: current applications.disable AND grants.revoke plus fresh MFA and exact confirmed
intent; reject any incomplete grant job. Commit retirement/revision/audit/new approval/job
atomically. Use retirement-specific replay scope/hash; never adopt unrelated grant approval.
Rationale: retirement and prior revocation are distinct destructive decisions.

Decision: preserve protected-target helper access for grant cleanup; reject retired targets
at ordinary mutation/clone-source and issuer boundaries. Bind retired state in write/issuer
snapshots. Exclude retired policies before native paging without hiding unmanaged/system rows.
Retained assignments continue to count as catalogue references.

## Durable authority and limits
Existing grant batches recheck current account/grants.revoke after logout/MFA expiry.
Explicit stopped-job continuation requires new current authority/fresh MFA. Failure never
clears retirement. Do not claim both admission permissions are checked each batch.
Offline JWTs can remain usable until expiry; remote sessions are not erased.

## Investigation
Independent architecture/test investigations at base
23a47a69cdac22edc2f87770d8175ef1429f7119 support the planning direction only. They inspected
mutation, scope selection, issuer snapshots, grant admission/worker, list paging and native
references. Both-provider races, persisted fault oracles, host/browser flows, recovery and
causal witnesses remain required. No unresolved product clarification remains.


## Review clarifications
Source ETag binds callbacks and assigned names. Catalogue eligibility is observed after
acquiring governance ownership: reject current ineligibility and any subsequent observed
snapshot drift, not every eligible catalogue edit before that observation. No preview
catalogue version is claimed. Keep one original clone authority receipt, never a nested
synthetic Create operation. Retirement authority checks both permissions under one epoch.
A separate bounded retired-app list and noneditable detail grant panel make recovery
discoverable after reload/re-login/unknown outcome; transient target memory is insufficient.
