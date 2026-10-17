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
    private const string Scope = "identity.admin";
    private const string Subject = "durable-consumption-subject";
    private const string Redirect = "https://consumption.example.test/callback";
    private const string Fingerprint = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const int LifetimeSeconds = 60;

    /// <summary>Creates fixture registration in one manager-owned transaction; production initialization is a separate task.</summary>
    internal static Task InitializeAsync(IServiceProvider root, DatabaseStore store, CancellationToken token) =>
        new ProtocolStorageRequest(root.GetRequiredService<TimeProvider>(), token).RunAsync(root.GetRequiredService<IServiceScopeFactory>(), async (scope, cancellation) =>
        {
            if (store == DatabaseStore.Sessions)
            {
                return true;
            }
            await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
            var applications = scope.Services.GetRequiredService<IOpenIddictApplicationManager>();
            var application = await applications.CreateAsync(new OpenIddictApplicationDescriptor { ClientId = Client, ClientType = OpenIddictConstants.ClientTypes.Public }, cancellation);
            var scopes = scope.Services.GetRequiredService<IOpenIddictScopeManager>();
            var registrationScope = await scopes.CreateAsync(new OpenIddictScopeDescriptor { Name = Scope }, cancellation);
            scope.Owner.Database.Add(new AdministratorProtocolProfile
            {
                ApplicationId = (await applications.GetIdAsync(application, cancellation))!,
                ScopeId = (await scopes.GetIdAsync(registrationScope, cancellation))!,
                RedirectUri = Redirect,
                PublicKeyFingerprint = Fingerprint,
                Revision = 1,
                Enabled = true
            });
            await scope.Owner.Database.SaveChangesAsync(cancellation);
            await transaction.CommitAsync(cancellation);
            return true;
        });

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
