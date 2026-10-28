using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Administrator.Tests.Features.SessionRefresh;

/// <summary>Links a retained intent to the genuinely issued authorization in the same transaction as native code creation.</summary>
internal sealed class NativeCleanupBinding(IOpenIddictTokenManager tokens, IOpenIddictApplicationManager applications,
    IOpenIddictAuthorizationManager authorizations, TimeProvider clock, NativeCleanupFaults faults) : IOpenIddictServerHandler<OpenIddictServerEvents.ProcessSignInContext>
{
    /// <summary>Creates metadata with no payload; duplicate intent binding fails the entire issuance transaction.</summary>
    public async ValueTask HandleAsync(OpenIddictServerEvents.ProcessSignInContext context)
    {
        if (context.EndpointType != OpenIddictServerEndpointType.Authorization)
        {
            return;
        }
        var intent = (string?)context.Request[NativeCleanupProfile.Intent];
        Assert.True(NativeCleanupProfile.IsRandom(intent));
        Assert.NotNull(context.Principal);
        var authorization = context.Principal.GetAuthorizationId();
        Assert.False(string.IsNullOrEmpty(authorization));
        var reference = NativeCleanupProfile.Reference(context.ClientId!, intent!);
        if (await tokens.FindByReferenceIdAsync(reference, context.CancellationToken) is not null)
        {
            faults.ThrowDuplicate();
        }
        var application = await applications.FindByClientIdAsync(context.ClientId!, context.CancellationToken);
        Assert.NotNull(application);
        var family = await authorizations.FindByIdAsync(authorization!, context.CancellationToken);
        Assert.NotNull(family);
        var applicationId = await applications.GetIdAsync(application, context.CancellationToken);
        Assert.Equal(applicationId, await authorizations.GetApplicationIdAsync(family, context.CancellationToken));
        await tokens.CreateAsync(new OpenIddictTokenDescriptor
        {
            ApplicationId = applicationId,
            AuthorizationId = authorization,
            CreationDate = clock.GetUtcNow(),
            ReferenceId = reference,
            Status = OpenIddictConstants.Statuses.Valid,
            Type = NativeCleanupProfile.SelectorType
        }, context.CancellationToken);
    }
}
