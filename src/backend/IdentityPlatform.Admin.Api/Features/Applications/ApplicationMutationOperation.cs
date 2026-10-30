using IdentityPlatform.Admin.Api.Features.Authentication;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.ProtocolAdmission;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;

namespace IdentityPlatform.Admin.Api.Features.Applications;

/// <summary>Owns current management authority, native writes, audit, commit and actual cleanup before releasing one safe result.</summary>
internal static class ApplicationMutationOperation
{
    /// <summary>Retains the caller's original API budget across every child and never retries a failed or ambiguous mutation.</summary>
    internal static async Task<ApplicationMutationResult> ExecuteAsync(IServiceScopeFactory factory, ApiIdentity identity,
        ApplicationMutationCommand command, ApiResourceProfile profile, TimeProvider clock, ApplicationReadBudget budget, Guid correlation)
    {
        budget.RequireCurrent();
        var request = new ProtocolStorageRequest(clock, budget.Token);
        try
        {
            var completed = await request.RunAsync(factory, (scope, token) => ExecuteOwnedAsync(scope, identity, command, profile, budget, correlation, token));
            completed.Authority.RequirePublication(clock, budget);
            var details = completed.State.Details(completed.Authority.Deadline);
            return new(command.Kind == ApplicationMutationKind.Create ? StatusCodes.Status201Created : StatusCodes.Status200OK,
                details, completed.State.Version, completed.State.Credential(details));
        }
        catch (ApiMutationDenied denied) { return new(denied.Status, null, null, null); }
        catch (ApplicationMutationRejected rejected) { return new(Status(rejected.Failure), null, null, null); }
        finally { await request.DrainAsync(); }
    }

    /// <summary>Runs exactly one transaction and action receipt; generic infrastructure helpers cannot publish or independently commit its result.</summary>
    private static async ValueTask<Completed> ExecuteOwnedAsync(ProtocolStorageScope scope, ApiIdentity identity,
        ApplicationMutationCommand command, ApiResourceProfile profile, ApplicationReadBudget budget, Guid correlation, CancellationToken token)
    {
        scope.Services.GetRequiredService<ProtocolStorageSelection>().Select(ProtocolStorageMode.Owned);
        await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
        var ownership = await ProtocolAdmissionOwnership.AcquireAsync(scope.Owner, identity.Subject, token);
        var authority = await ApiMutationAuthority.AcquireAsync(scope, ownership, identity, command, profile.Callback, token);
        budget.RequireCurrent();
        var state = await ApplicationMutationWriter.StageAsync(scope, command, token);
        await authority.VerifyAsync(scope, command, token);
        var audit = await ApplicationMutationWriter.AuditAsync(scope, command, state, identity.Subject, correlation, token);
        await ApplicationMutationWriter.VerifyAuditAsync(scope, command, state, identity.Subject, audit, token);
        await state.VerifyAsync(scope, token);
        await authority.VerifyAsync(scope, command, token);
        ApplicationMutationWriter.Now(scope, command);
        budget.RequireCurrent();
        authority.Consume(scope, command, token);
        await transaction.CommitAsync(token);
        return new(state, authority);
    }

    /// <summary>Maps only finite domain rejections; native failures propagate to the endpoint's sanitized unavailable response.</summary>
    private static int Status(ApplicationMutationFailure failure) => failure switch
    {
        ApplicationMutationFailure.Forbidden => StatusCodes.Status403Forbidden,
        ApplicationMutationFailure.Missing => StatusCodes.Status404NotFound,
        ApplicationMutationFailure.Duplicate => StatusCodes.Status409Conflict,
        ApplicationMutationFailure.Stale => StatusCodes.Status412PreconditionFailed,
        ApplicationMutationFailure.Expired => StatusCodes.Status400BadRequest,
        ApplicationMutationFailure.Unsupported => StatusCodes.Status409Conflict,
        _ => throw ProtocolStorageOwner.Unavailable()
    };

    /// <summary>Retains only frozen output and private publication bounds until the owned child has actually disposed.</summary>
    private sealed record Completed(ApplicationWriteState State, ApiMutationAuthority Authority);
}

/// <summary>Contains only a safe projection or immediate one-time credential response; no plaintext record formatting is generated.</summary>
internal sealed record ApplicationMutationResult(int Status, ApplicationDetails? Application, string? Version, ApplicationCredentialOutput? Credential);
