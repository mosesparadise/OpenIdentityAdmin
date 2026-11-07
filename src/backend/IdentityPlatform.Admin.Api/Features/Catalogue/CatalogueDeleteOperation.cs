using IdentityPlatform.Admin.Api.Features.Applications;
using IdentityPlatform.Admin.Api.Features.Authentication;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Core.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Identity.ProtocolAdmission;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;

namespace IdentityPlatform.Admin.Api.Features.Catalogue;

/// <summary>Admits a durable catalogue deletion using original current authority and returns status only after actual commit and cleanup.</summary>
internal static class CatalogueDeleteOperation
{
    /// <summary>Preserves one original request budget and independently checks publication authority after disposing all native children.</summary>
    internal static async Task<CatalogueDeleteResult> ExecuteAsync(IServiceScopeFactory factory, ApiIdentity identity,
        CatalogueDeleteIntent intent, ApiResourceProfile profile, TimeProvider clock, ApplicationReadBudget budget, Guid correlation)
    {
        budget.RequireCurrent();
        var request = new ProtocolStorageRequest(clock, budget.Token);
        try
        {
            var completed = await request.RunAsync(factory, (scope, token) => OwnedAsync(scope, identity, intent, profile, budget, correlation, token));
            completed.Authority.RequirePublication(clock, budget);
            return new(StatusCodes.Status202Accepted, completed.Job.Details(completed.Authority.Deadline));
        }
        catch (ApiMutationDenied denied) { return new(denied.Status, null); }
        catch (CatalogueRejected rejected) { return new(CatalogueMutationOperation.Status(rejected.Failure), null); }
        finally { await request.DrainAsync(); }
    }

    /// <summary>Binds durable approval to the exact original action receipt; replay does not extend recorded approval or add an admission audit.</summary>
    private static async ValueTask<Completed> OwnedAsync(ProtocolStorageScope scope, ApiIdentity identity, CatalogueDeleteIntent intent,
        ApiResourceProfile profile, ApplicationReadBudget budget, Guid correlation, CancellationToken token)
    {
        scope.Services.GetRequiredService<ProtocolStorageSelection>().Select(ProtocolStorageMode.Owned);
        await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
        var ownership = await ProtocolAdmissionOwnership.AcquireAsync(scope.Owner, identity.Subject, token);
        var authority = await ApiMutationAuthority.AcquireCatalogueDeleteAsync(scope, ownership, identity, intent, profile.Callback, token);
        var admission = await CatalogueDeletionAdmission.StageAsync(scope, intent, authority.DeletionApproval(scope, intent, token), correlation, token);
        var job = admission.Job;
        await job.VerifyAsync(scope, token);
        await authority.VerifyAsync(scope, intent, token);
        if (admission.Admitted && job.ApprovalUntil > authority.Deadline.ToUnixTimeSeconds())
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        budget.RequireCurrent();
        authority.Consume(scope, intent, token);
        await transaction.CommitAsync(token);
        return new(job, authority);
    }

    /// <summary>Retains immutable durable output and private publication proof until owned cleanup actually completes.</summary>
    private sealed record Completed(CatalogueDeletionSnapshot Job, ApiMutationAuthority Authority);
}

/// <summary>Reports durable admission independently of eventual deletion and never exposes original approval material.</summary>
internal sealed record CatalogueDeleteResult(int Status, CatalogueDeletionDetails? Job);
