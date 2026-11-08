using IdentityPlatform.Admin.Api.Features.Applications;
using IdentityPlatform.Admin.Api.Features.Authentication;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Identity.ProtocolAdmission;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;

namespace IdentityPlatform.Admin.Api.Features.Catalogue;

/// <summary>Owns current catalogue authority, atomic native/product/audit writes and actual cleanup before publishing any success.</summary>
internal static class CatalogueMutationOperation
{
    /// <summary>Retains one original management budget and consumes one private action receipt without retrying ambiguous mutations.</summary>
    internal static async Task<CatalogueMutationResult> ExecuteAsync(IServiceScopeFactory factory, ApiIdentity identity,
        CatalogueIntent intent, ApiResourceProfile profile, TimeProvider clock, ApplicationReadBudget budget, Guid correlation)
    {
        budget.RequireCurrent();
        var request = new ProtocolStorageRequest(clock, budget.Token);
        try
        {
            var completed = await request.RunAsync(factory, (scope, token) => ExecuteOwnedAsync(scope, identity, intent, profile, budget, correlation, token));
            completed.Authority.RequirePublication(clock, budget);
            return new(intent.Action == CatalogueAction.Create ? StatusCodes.Status201Created : StatusCodes.Status200OK,
                completed.Audience, completed.Scope, completed.Authority.Deadline, completed.Referenced);
        }
        catch (ApiMutationDenied denied) { return new(denied.Status, null, null); }
        catch (CatalogueRejected rejected) { return new(Status(rejected.Failure), null, null); }
        finally { await request.DrainAsync(); }
    }

    /// <summary>Orders governance and account locks before reads; all manager saves and audit share the same original native transaction.</summary>
    private static async ValueTask<Completed> ExecuteOwnedAsync(ProtocolStorageScope scope, ApiIdentity identity,
        CatalogueIntent intent, ApiResourceProfile profile, ApplicationReadBudget budget, Guid correlation, CancellationToken token)
    {
        scope.Services.GetRequiredService<ProtocolStorageSelection>().Select(ProtocolStorageMode.Owned);
        await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
        var ownership = await ProtocolAdmissionOwnership.AcquireAsync(scope.Owner, identity.Subject, token);
        var authority = await ApiMutationAuthority.AcquireCatalogueAsync(scope, ownership, identity, intent, profile.Callback, token);
        budget.RequireCurrent();
        var audience = intent.Resource == CatalogueResource.Audience ? await CatalogueAudienceWriter.StageAsync(scope, intent, token) : null;
        var native = intent.Resource == CatalogueResource.Scope ? await CatalogueScopeWriter.StageAsync(scope, intent, token) : null;
        var target = audience?.Id ?? native?.Id ?? throw ProtocolStorageOwner.Unavailable();
        await authority.VerifyAsync(scope, intent, token);
        var audit = await CatalogueAudit.StageAsync(scope, intent, target, identity.Subject, correlation, token);
        await audit.VerifyAsync(scope, token);
        if (audience is not null)
        {
            await audience.VerifyAsync(scope, token);
        }
        if (native is not null)
        {
            await native.VerifyAsync(scope, token);
        }
        var referenced = native is not null ? await CatalogueNativeReferences.ScopeAsync(scope.Services, native.Name, token) :
            await CatalogueNativeReferences.AudienceAsync(scope.Services, audience!.Resource, token);
        await authority.VerifyAsync(scope, intent, token);
        budget.RequireCurrent();
        authority.Consume(scope, intent, token);
        await transaction.CommitAsync(token);
        return new(audience, native, authority, referenced);
    }

    /// <summary>Maps expected finite catalogue failures; unknown native/storage failures propagate to the sanitized HTTP boundary.</summary>
    internal static int Status(CatalogueFailure failure) => failure switch
    {
        CatalogueFailure.Missing => StatusCodes.Status404NotFound,
        CatalogueFailure.Stale => StatusCodes.Status412PreconditionFailed,
        CatalogueFailure.Duplicate or CatalogueFailure.Immutable or CatalogueFailure.Unsupported or CatalogueFailure.Referenced => StatusCodes.Status409Conflict,
        _ => throw ProtocolStorageOwner.Unavailable()
    };

    /// <summary>Keeps only frozen policy and private authority until the owned native child has actually finished disposal.</summary>
    private sealed record Completed(CatalogueAudience? Audience, CatalogueScope? Scope, ApiMutationAuthority Authority, bool Referenced);
}

/// <summary>Retains internal fixed state for later bounded HTTP projection; no native entities or arbitrary properties escape.</summary>
internal sealed record CatalogueMutationResult(int Status, CatalogueAudience? Audience, CatalogueScope? Scope, DateTimeOffset? ExpiresAt = null, bool Referenced = false);
