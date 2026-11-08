# Catalogue research and decisions

## Approved policy

- **Decision:** M03-A–C plus consented email/email_verified; see docs/approval.md and
  spec.md. Current Identity account is the source; absent email omits both fields,
  unconfirmed email is paired with false. No username/arbitrary claims/admin roles.
  Email claims are identity disclosure, not API authorization.
- **Decision:** one API audience, immutable ordinal identities, blocked referenced
  deletion, current permissions, atomic audit and explicit consent come from approved
  `03-protocol-security.md` and `04-data-api-contracts.md`. No new product choice needed.
- **Decision:** changed policy rejects stale pending/cached/consented/code decisions;
  it never silently expands prior consent. Offline tokens retain expiry limits while
  online UserInfo checks current policy. Automatic grant revocation is not implied.

## Current source findings

- `ManagedApplicationProfile` fixes permissions and no resources; `ManagedDemand`
  accepts exactopenid; `ManagedPrincipal` permits onlysubject. A catalogue alone
  cannot deliver real authorization. Add explicit assignment and policy binding;
  never turn extra display metadata into authority.
- `OwnedScopeStore` already forwards native scope CRUD/list through explicit owned
  context/transaction and cancellation. `PlatformDbContext` already applies provider
  ordinal scope-name collation. Reuse these instead of a second native scope store.
- `ManagedRegistration.LockAsync` acquires the same governance lock as application
  writes. Reuse it as the ordering boundary for assignments/catalogue policy, with
  current native reloads and no network call inside a transaction.
- `ApiMutationAuthority` currently handles application and grant actions with private
  one-use receipt identity; reuse the mechanism without accepting arbitrary permission
  strings from request data or borrowing application-write permission for scopes.
- `NativeClientMetadata` currently rejects any UserInfo metadata. Publishing the
  endpoint requires a narrowly pinned compatibility change; `NativeClientUserInfo`
  and `NativeClientUserInfoFence` must continue preventing any optional BFF send.
- `PlatformUser` has Identity email/EmailConfirmed but no public profile schema.
  The user-selected email pair avoids inventing a display-name/username policy.

## Alternatives rejected

No parallel authoritative scope-resource or application-permission store; no generic
raw claims editor; no managed-client auto-adoption; no new machine grant; no global
BFF UserInfo retrieval; no silently reused stale consent; no manual token creation.

## Evidence status

These are source/design findings, not runtime passes. Independent initial architecture
review confirms settled boundaries and identified the now-resolved claim disclosure
choice. Final packet/source/provider evidence remains required.
