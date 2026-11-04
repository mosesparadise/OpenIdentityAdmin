# Lifecycle design research

**Date**: 2026-11-04. **Baseline**: main `b27669f04ad5992ae829c1250bd13f90e8c72446`,
tree `748854d1efa15f5999d53894cb5c1fe2ffbc8626`. Remote main was read with
`git ls-remote`; the new isolated worktree starts at that exact commit.
Existing worktrees and delivery branches were preserved.

## Decisions and evidence

| Decision | Existing evidence | Rationale / alternative |
| --- | --- | --- |
| Product-owned enabled flag | Infrastructure `Features/Applications/ApplicationPolicy.cs` has revision/credential times but no lifecycle state | Do not remove native permissions or overload `Editable`; native metadata and product status have different ownership. |
| Advance existing Revision on both transitions | `ManagedCacheSession`, `ManagedHumanBinding`, `ManagedPrincipal` and `ManagedTokenBinding` bind application revision | Prevent resurrection without synchronously scanning all old requests/grants. Never reset a revision or update old interaction bindings. |
| Reuse governance serialization | `ManagedRegistration.LockAsync`, `ProtocolAdmissionOwnership`, `ApplicationMutationOperation` | Preserve established lock order and current checks; a second independent lifecycle lock would miss issuer races. |
| Explicit action/permission dispatch | `ApplicationMutationWriter.UpdateAsync` defaults non-Replace to edit; `ApiMutationAuthority` defaults non-Replace to write permission | Merely extending the enum would risk field loss and wrong authorization. Closed action switches must reject unknown values. |
| Finite private audit reason | `SecurityAuditIntent` has action/actor/target/time/correlation but no reason | Add an application-owned one-to-one audit detail, atomically linked and verified; avoid arbitrary text in general logs. |
| Lifecycle independent of credentials | `ManagedRegistration.ReadAsync` already rejects expired credentials | Re-enable does not extend expiry; repair/edit/replacement remain separate permissions and cannot enable a disabled application. |
| Bulk revocation separate in recommendation | Approved API contracts require durable 202 jobs for bulk work | A synchronous authorization/token loop violates that contract. The user preference is pending, not inferred approval. |

## Read-only independent review

The architecture reviewer confirmed revision reuse and governance serialization, identified the
two unsafe default dispatch branches, and required reason persistence before implementation.
The test reviewer required populated upgrades, genuine issuer continuation/re-enable scenarios,
actual provider wait observations, audit INSERT denial, both issuance orderings and native demo
coverage. These findings are incorporated into this packet. Neither reviewer ran tests or claimed
new product acceptance. Final packet review is recorded in [review.md](review.md).

## Boundaries retained

Feature019 was delivered in PR120 and documented in PR121; its implementation is not reopened.
Managed access tokens have no management audience. Their rejection by Admin API is not evidence
that disable enforcement works. Test genuine managed issuance directly. Refresh, other profiles,
bulk grant revocation, clone/delete and external-session controls remain unbuilt.

The ordinary local-demo walkthrough is already documented and previously passed hosted tests
on both providers. This planning task does not claim a new local browser execution or an actual
Windows Docker Desktop manual pass. No SQL experiment, runtime mutation or full application CI
was needed to inspect the current code and prepare this packet.
