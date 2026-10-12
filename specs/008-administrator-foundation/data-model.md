# Data model — increment 1

Existing native Identity user, role, claim and membership tables remain manager-owned. New product rows use the platform schema on both providers and portable numeric versions; generated migrations are provider-specific. No cascade may erase bootstrap consumption or audit.

| Entity | Fields / constraints | Meaning |
| --- | --- | --- |
| AdministratorGovernance | Id=1 fixed singleton, Revision nonnegative long, BootstrapConsumed bool | Migration seeds the only row with revision 0/false. An UPDATE obtains the shared transaction lock before eligibility reads. Both-row deletion/corruption is not an authorized reset operation. |
| AdministratorBootstrapReceipt | Id=1, OperationId nonempty Guid, OriginalNormalizedUserName max256, UserId max450, BundleVersion=1, CompletedAt Unix seconds | Immutable after successful bootstrap; no user foreign key/cascade, so deletion cannot reopen bootstrap. |
| AdministratorEnrollment | UserId max450 primary key, State=`Pending` max32, Revision long | Pending identity only; no factor material in this increment. Later activation/recovery semantics require reviewed expansion. |

Native reserved roles: PlatformAdministrator, ApplicationAdministrator, IdentityAdministrator, Auditor. Claims use one exact permission type and a finite v1 catalogue derived from approved API permissions. Each role bundle is created only when absent; unexpected pre-existing reserved roles are conflicts, not adopted or repaired. Manager-created account is disabled until verified enrollment, with lockout enabled.

Repeat transaction: lock singleton, validate receipt/consumed consistency, compare operation/normalized identity, report already-initialized, rollback lock-only revision update. It does not inspect current credentials or repair later account/role changes.

Creation transaction: lock singleton; require unconsumed/no receipt/no enrollment/reserved-role conflict; create roles, role claims, user and initial membership through managers; add pending enrollment, immutable receipt and one success audit; set consumed; commit. Any failed IdentityResult/SQL/audit/cancellation rolls back all state. Roles/user and audit use the same scoped DbContext/transaction.

Future invariants: a pending row, role membership or TwoFactorEnabled alone cannot count as a usable administrator. Later transitions must share governance serialization; this schema alone does not claim last-admin protection or permission enforcement.

## Exact v1 permission manifest

Native role claims use type `identityplatform.permission`; values are case-sensitive ordinal identifiers. Wildcards and user-supplied claims do not confer authority. No persisted claim grants a future permission that is absent from this v1 manifest.

| Bundle | Exact values |
| --- | --- |
| ApplicationAdministrator | applications.read, applications.write, applications.credentials, applications.disable, scopes.read, scopes.write, audiences.read, audiences.write, configuration.read, configuration.evaluate |
| IdentityAdministrator | users.read, users.write, users.disable, users.unlock, users.recovery, roles.read, roles.write, roles.permissions, grants.read, grants.revoke, sessions.read, sessions.revoke |
| Auditor | applications.read, scopes.read, audiences.read, users.read, roles.read, grants.read, sessions.read, audit.read, configuration.read, keys.read |
| PlatformAdministrator | The 24 distinct v1 identifiers across those three bundles plus administrators.assign (25 total). |

`administrators.assign` is separate from ordinary role editing. Later enforcement must also require it for indirect privilege changes (including changing privileged bundles or inserting this permission). This first increment stores the manifest but exposes no role-editing operation. No application Operator role is seeded.

Database check constraints restrict both singleton IDs to1 and numeric revisions to nonnegative values. Enrollment has no cascading user FK in this increment; deleting a native user leaves the pending product row and permanent receipt as evidence and cannot reopen bootstrap. Future cleanup is a separate governed operation. Application code rejects missing singleton and inconsistent bootstrap records rather than reseeding them.
