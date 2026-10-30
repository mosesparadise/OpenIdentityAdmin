# Application editor data model

Native OpenIddict Application remains authoritative for ID/client ID, display name, exact redirects, protocol permissions/requirements and manager-hashed secret. Never duplicate native permission sets as a second policy authority.

## ApplicationPolicy

Key: native ApplicationId (string, supported managed identifier limit 400; native PG text/SQL nvarchar450 schema remains unchanged); fixed profile `managed-web-v1`; aggregate Revision and CredentialRevision (positive portable long concurrency markers); CredentialIssuedAt, CredentialExpiresAt and ObservedAt (UTC Unix seconds). New records start revision 1. Every edit/replacement advances aggregate revision; replacement additionally advances credential revision. Expiry must exceed observed issuance time. Client ID is immutable. No automatic adoption of existing native/system rows. Protect reserved client identities and persisted administrator/pilot application bindings independently. No destructive cascades into audit/replay history.

The safe details projection supplies ID/clientId/displayName/redirectUris/profile, credential metadata, editable status, aggregate ETag and original observation deadline. Never project native secret hashes, arbitrary properties, private keys or plaintext later.

## ManagedLoginInteraction

Opaque unique public selector; native ApplicationId; aggregate revision; exact native cached request-token ID; bounded canonical typed demand digest; browser proof digest; original creation/expiry/retention boundaries; state and portable revision. Authentication binding includes a distinct managed-purpose ceremony ID, verified user ID, account authentication generation, factor generation, enrollment revision, stamp and actual password/MFA completion times. No AdministratorSession ID or BFF establishment grant.

Transitions: pending password → pending MFA → pending consent → approved/denied → consumed. Each transition compares current version, original browser/native demand and deadline. Terminal/replayed/foreign rows cannot reopen. Code principals bind the managed interaction/application revision; edits/replacement invalidate stale pending or issued-code admission. Bound concurrent live interactions per browser and globally, prune only expired terminal data after native replay windows, and fail closed on capacity rather than evicting live rows. Use named bounds: original300 seconds, per-browser8 live, global128 live, total10000 retained, cleanup100. Reserve before native cache creation, bind its actual token ID before commit, and retain for at least14 days plus all associated native lifetimes/skew/30-second rollback grace. Expired unfinished rows become terminal. Independently bound native artifact creation using exact row deltas. Test equality, overflow, concurrent reservation, lost initial redirects and dependent-artifact cleanup.

## Existing ceremony and audit state

Add one exact managed-login purpose to shared lower credential admission/replay machinery if required. Existing administrator/B3 completion families must reject that purpose and any cross-family substitution. Preserve current account lockout, factor replay and browser budget accounting. Record finite action/actor/target/outcome/correlation/time only; no input serialization or credentials.

## Provider evolution

Add new Platform tables and any required exact-purpose constraint changes via one new migration per provider. Keep old migration IDs/files and all old data unchanged. Append exact descriptor histories and extend frozen upgrade witnesses by a named exact delta; no permissive wildcard table/history allowance. Reapply existing runtime role provisioning after migration. Test fresh and populated upgrade, repeated upgrade, native manager/audit failure rollback and model parity on both providers.
