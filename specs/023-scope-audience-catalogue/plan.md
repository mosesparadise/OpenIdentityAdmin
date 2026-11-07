# Implementation plan: Scope and audience catalogue

## Technical context and baseline

Start from main `7c458f4329b945a12ea913b3d79984a058390a80`, accepted feature022.
Use the existing .NET10/EF relational provider plugins, OpenIddict7.7.2 native
managers, React/BFF workspace and native Aspire. No dependency upgrade is included.
The user selected M03-A–C plus consented email/email_verified. All13 feature
requirements are authorized; implementation and acceptance remain incomplete.

## Constitution check

Native managers remain protocol owners; product policy stays separate from native
permissions/resources. Browser tokens remain server-side. Both provider migrations,
current authority, original deadlines, atomic audit, race/failure tests, explicit
versioned contracts, retained-browser UI and independent reviews are mandatory.
No generic repository, new database provider switch, weakened regression floor or
release waiver is required. See research/data-model/contracts for concrete boundaries.

## Design and ownership

1. Core `Features/Catalogue/` defines bounded identifier/input/version/projection and
   finite claim policy. Infrastructure `Features/Catalogue/` owns audience/policy
   persistence and native scope-manager operations. Compose model/dialect additions
   through PlatformDbContext and both existing migration assemblies.
2. Use the existing governance serialization boundary before catalogue reads/writes,
   application assignments and protected managed scope decisions. Consistent order:
   governance → current actor/user authority → native registration/policy reads.
   Do not hold any database transaction across network calls.
3. Native scope resources and application scope permissions remain authoritative.
   Reload native state through owned storage; caches cannot authorize stale values.
   The catalogue cannot adopt or mutate unmanaged/system native rows. Before deletion,
   check native resources, application permissions and native authorization/token
   dependencies under the same serialization as assignment and issuance.
4. Catalogue mutation intent is immutable and action-bound; acquire current permission
   and fresh MFA inside the transaction, stage native/product changes and exact audit,
   recheck state/authority/deadline, consume the one-use commit receipt, commit and
   complete actual cleanup before publishing. Reuse tested owned-storage/publication
   mechanisms; extract only truly common management boundaries with preservation tests.
5. Publish `/api/v1/scopes`, `/audiences` with durable DELETE admission/status and an additive application scope-assignment
   route. BFF publishes corresponding fixed browser routes and native antiforgery.
   Finite strict DTOs, ETags, bounds and denial statuses are specified in contracts.
   Register a bounded catalogue deletion processor in the existing Worker/Aspire
   lifecycle. Persist job admission and terminal outcome; recheck current actor grants,
   original approval deadline, version and current/retained references at execution.
   No automatic reapproval or extra native revocation is introduced.
6. Add Scopes/Audiences navigation and complete list/detail/edit/delete states to the
   existing React workspace; add application scope assignment without replacing the
   established application editor or revealing credentials.
7. Extend managed applications only after explicit assignment. Preserve exact previous
   openid-only behavior; fixed administrator/pilot profiles never become catalogue
   clients. Bind canonical requested/granted scopes and revisioned disclosure/resource
   policy to protected interactions and native codes, with indexed non-authoritative
   retained-decision references for safe deletion. Every later phase verifies the
   current persisted policy; mapping/claim changes invalidate stale pending decisions.
8. Identity scopes may release the selected email pair only to ID token/UserInfo after
   consent; API scopes never release human identity fields. Access tokens carry only
   required protocol claims/scope/resource, not email. Scope consent is explicit and
   cannot expand through a display-name or mapping edit.
9. UserInfo GET/POST authenticates through the native server scheme, then checks current
   managed user/client/authorization/native token/scope/audience and returns exactsub
   plus permitted current email fields. No machine/human-profile fabrication. Keep
   native errors and no-store responses; no custom JWT validation or manual signing.
10. Configure native discovery/UserInfo only for the implemented managed surface.
    BFF metadata may accept only the exact known same-issuer endpoint while its two
    existing no-UserInfo-send fences remain active. Preserve fixed native client tests.

## Implementation sequence

See tasks.md. US1 catalogue can be tested independently but its delivery does not mark
M03-B/C complete. Policy/assignment follows catalogue; actual token/UserInfo follows
bound current policy and consent. All selected stories converge before full milestone
acceptance. Planning and schema preparation are not user-visible delivery.

## Verification and review

Use the scenario matrix in contracts/catalogue.md and actual discovered identities.
Extend existing real-provider Applications/ManagedApplications and persistence fixtures,
plus separate feature-local catalogue tests and Host/contracts/UI controls. Add explicit
cross-process stale-cache, policy race, reference race, no-userinfo-send and native
crypto/error tests. Preserve original discovery identities, all current coverage inputs,
module floors and mutation targets; register additions before final exact-head CI.

Independent architecture/security and test reviewers assess packet before affected
implementation, then actual source/evidence. API/UX/provider reviews apply at those
boundaries. Local SQL startup failure is not retried without changed evidence; use
unchanged hosted SQL for required execution. Preserve all frozen producer/evidence
paths. No production actions or external assurance declarations are included.
