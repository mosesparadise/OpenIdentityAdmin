using IdentityPlatform.Admin.Api.Features.Applications;
using IdentityPlatform.Admin.Api.Features.Authentication;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.ProtocolAdmission;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;

namespace IdentityPlatform.Admin.Api.Features.ApplicationGrants;

/// <summary>Owns fresh independently authorized durable admission through commit, native cleanup and original publication bounds.</summary>
internal static class GrantAdmissionOperation
{
    /// <summary>Accepts one explicit intent without automatic retries; duplicate recovery still requires current account/grant/MFA authority.</summary>
    internal static async Task<GrantJobView> ExecuteAsync(IServiceScopeFactory factory, ApiIdentity identity, GrantIntent intent,
        ApiResourceProfile profile, TimeProvider clock, ApplicationReadBudget budget, Guid correlation)
    {
        var request = new ProtocolStorageRequest(clock, budget.Token);
        try
        {
            var result = await request.RunAsync(factory, (scope, token) => OwnedAsync(scope, identity, intent, profile, budget, correlation, token));
            result.Authority.RequirePublication(clock, budget);
            return result.View;
        }
        catch (ApiMutationDenied denied) { throw new ApplicationHttpRejected(denied.Status); }
        catch (ApplicationMutationRejected denied) { throw new ApplicationHttpRejected(Status(denied.Failure)); }
        finally { await request.DrainAsync(); }
    }

    /// <summary>Binds the immutable intent to one current API receipt; the worker never receives or reuses that live receipt.</summary>
    private static async ValueTask<Completed> OwnedAsync(ProtocolStorageScope scope, ApiIdentity identity, GrantIntent intent,
        ApiResourceProfile profile, ApplicationReadBudget budget, Guid correlation, CancellationToken token)
    {
        scope.Services.GetRequiredService<ProtocolStorageSelection>().Select(ProtocolStorageMode.Owned);
        await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
        var ownership = await ProtocolAdmissionOwnership.AcquireAsync(scope.Owner, identity.Subject, token);
        var authority = await ApiMutationAuthority.AcquireGrantAsync(scope, ownership, identity, intent, profile.Callback, token);
        var job = await GrantAdmission.StageAsync(scope, intent, identity.Subject, authority.FactorVerifiedAt, correlation, token);
        await authority.VerifyAsync(scope, intent, token);
        var view = GrantProjection.Create(job, authority.Deadline);
        budget.RequireCurrent();
        authority.Consume(scope, intent, token);
        await transaction.CommitAsync(token);
        return new(view, authority);
    }

    /// <summary>Uses the existing finite application conflict/precondition vocabulary without exposing native validation details.</summary>
    private static int Status(ApplicationMutationFailure failure) => failure switch
    {
        ApplicationMutationFailure.Forbidden => StatusCodes.Status403Forbidden,
        ApplicationMutationFailure.Missing => StatusCodes.Status404NotFound,
        ApplicationMutationFailure.Stale => StatusCodes.Status412PreconditionFailed,
        ApplicationMutationFailure.Expired => StatusCodes.Status400BadRequest,
        ApplicationMutationFailure.Duplicate or ApplicationMutationFailure.Unsupported or ApplicationMutationFailure.SameState => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status503ServiceUnavailable
    };

    /// <summary>Retains only a safe frozen response and private expiry receipt until real owned disposal completes.</summary>
    private sealed record Completed(GrantJobView View, ApiMutationAuthority Authority);
}
