using IdentityPlatform.Admin.Api.Features.Authentication;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.ProtocolAdmission;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;

namespace IdentityPlatform.Admin.Api.Features.Applications;

/// <summary>Owns dual-permission fresh-MFA retirement through one native transaction, real cleanup and bounded publication.</summary>
internal static class ApplicationRetirementOperation
{
    /// <summary>Retains the original request budget and never retries retirement or creates another authority epoch after an ambiguous outcome.</summary>
    internal static async Task<ApplicationRetirementAcceptance> ExecuteAsync(IServiceScopeFactory factory, ApiIdentity identity,
        ApplicationRetirementIntent intent, ApiResourceProfile profile, TimeProvider clock, ApplicationReadBudget budget, Guid correlation)
    {
        var request = new ProtocolStorageRequest(clock, budget.Token);
        try
        {
            var completed = await request.RunAsync(factory, (scope, token) => OwnedAsync(scope, identity, intent, profile, budget, correlation, token));
            completed.Authority.RequirePublication(clock, budget);
            if (completed.View.Job.ExpiresAt <= clock.GetUtcNow())
            {
                throw new ApplicationMutationRejected(ApplicationMutationFailure.Duplicate);
            }
            return completed.View;
        }
        catch (ApiMutationDenied denied) { throw new ApplicationHttpRejected(denied.Status); }
        catch (ApplicationMutationRejected denied) { throw new ApplicationHttpRejected(Status(denied.Failure)); }
        finally { await request.DrainAsync(); }
    }

    /// <summary>Rechecks both original permissions after all policy/approval/receipt saves before consuming the single receipt and committing once.</summary>
    private static async ValueTask<Completed> OwnedAsync(ProtocolStorageScope scope, ApiIdentity identity, ApplicationRetirementIntent intent,
        ApiResourceProfile profile, ApplicationReadBudget budget, Guid correlation, CancellationToken token)
    {
        scope.Services.GetRequiredService<ProtocolStorageSelection>().Select(ProtocolStorageMode.Owned);
        await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
        var ownership = await ProtocolAdmissionOwnership.AcquireAsync(scope.Owner, identity.Subject, token);
        var authority = await ApiMutationAuthority.AcquireRetirementAsync(scope, ownership, identity, intent, profile.Callback, token);
        var state = await ApplicationRetirementAdmission.StageAsync(scope, intent, identity.Subject, authority.FactorVerifiedAt, correlation, token);
        await state.VerifyAsync(scope, token);
        await authority.VerifyAsync(scope, intent, token);
        state.RequireCurrent(scope.Owner.Clock);
        var view = state.View(authority.Deadline);
        budget.RequireCurrent();
        authority.Consume(scope, intent, token);
        await transaction.CommitAsync(token);
        return new(view, authority);
    }

    /// <summary>Uses the finite existing application precondition/conflict vocabulary without leaking native validation details.</summary>
    private static int Status(ApplicationMutationFailure failure) => failure switch
    {
        ApplicationMutationFailure.Forbidden => StatusCodes.Status403Forbidden,
        ApplicationMutationFailure.Missing => StatusCodes.Status404NotFound,
        ApplicationMutationFailure.Stale => StatusCodes.Status412PreconditionFailed,
        ApplicationMutationFailure.Expired => StatusCodes.Status400BadRequest,
        ApplicationMutationFailure.Duplicate or ApplicationMutationFailure.Unsupported or ApplicationMutationFailure.SameState => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status503ServiceUnavailable
    };

    /// <summary>Retains only immutable public metadata and private original publication bounds after the native owner disposes.</summary>
    private sealed record Completed(ApplicationRetirementAcceptance View, ApiMutationAuthority Authority);
}
