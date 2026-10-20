using System.Security.Claims;
using IdentityPlatform.Database;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Creates disposable native rows and an explicit fixed-profile fixture without issuing or logging credentials.</summary>
internal static class NativeConsumptionSeed
{
    private const string Client = "identity-admin-bff";
    private const string Subject = "durable-consumption-subject";
    private const int LifetimeSeconds = 60;

    /// <summary>Uses the actual explicit initializer and verifies its exact repeat is a no-op; consumption principals remain synthetic.</summary>
    internal static async Task InitializeAsync(IServiceProvider root, DatabaseStore store, CancellationToken token)
    {
        if (store == DatabaseStore.Sessions)
        {
            return;
        }
        await NativeRegistrationInitialization.VerifyAsync(root, token);
    }

    /// <summary>Uses native manager creation and records native identifiers in an already-validated test principal.</summary>
    internal static Task<ClaimsPrincipal> TokenAsync(IServiceProvider root, string type, CancellationToken token) =>
        new ProtocolStorageRequest(root.GetRequiredService<TimeProvider>(), token).RunAsync(root.GetRequiredService<IServiceScopeFactory>(), async (scope, cancellation) =>
        {
            await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
            var state = type == OpenIddictConstants.TokenTypeIdentifiers.Private.StateToken;
            if (state)
            {
                await AdministratorProtocolLocks.AcquireClientStateAsync(scope.Owner, cancellation);
            }
            else
            {
                await AdministratorProtocolLocks.AcquireProfileAsync(scope.Owner, cancellation);
            }
            var principal = await PrincipalAsync(scope.Services, type, scope.Owner.Clock, cancellation);
            await NativeConsumptionDispatch.CreateAsync(scope, principal);
            await transaction.CommitAsync(cancellation);
            return principal;
        });

    /// <summary>Retains code authorization binding, exact pushed-request subtype and native early state registration binding.</summary>
    private static async Task<ClaimsPrincipal> PrincipalAsync(IServiceProvider services, string type, TimeProvider clock, CancellationToken token)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity()).SetTokenType(type).SetExpirationDate(clock.GetUtcNow().AddSeconds(LifetimeSeconds));
        if (type == OpenIddictConstants.TokenTypeIdentifiers.Private.StateToken)
        {
            return principal.SetClaim(OpenIddictConstants.Claims.Private.RegistrationId, Client)
                .SetClaim(OpenIddictConstants.Claims.Private.EndpointType, nameof(OpenIddict.Client.OpenIddictClientEndpointType.Redirection))
                .SetClaim(OpenIddictConstants.Claims.Subject, Subject);
        }
        principal.SetPresenters(Client);
        if (type == OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken)
        {
            return principal.SetClaim(OpenIddictConstants.Claims.Private.RequestTokenType, OpenIddictConstants.RequestTokenTypes.Private.PushedAuthorizationRequest);
        }
        principal.SetClaim(OpenIddictConstants.Claims.Subject, Subject);
        var applications = services.GetRequiredService<IOpenIddictApplicationManager>();
        var application = await applications.FindByClientIdAsync(Client, token);
        var authorizations = services.GetRequiredService<IOpenIddictAuthorizationManager>();
        var authorization = await authorizations.CreateAsync(new OpenIddictAuthorizationDescriptor
        {
            ApplicationId = await applications.GetIdAsync(application!, token),
            Subject = Subject,
            Status = OpenIddictConstants.Statuses.Valid,
            Type = OpenIddictConstants.AuthorizationTypes.AdHoc
        }, token);
        return principal.SetAuthorizationId(await authorizations.GetIdAsync(authorization, token));
    }
}
