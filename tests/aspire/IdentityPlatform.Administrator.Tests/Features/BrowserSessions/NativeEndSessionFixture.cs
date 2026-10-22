using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Characterizes installed native validation over committed test-only metadata, without activating a product logout route.</summary>
internal static class NativeEndSessionFixture
{
    internal const string ReturnUri = "https://native-assertion.example.test/bff/logout/return";
    internal const string LegacyPermission = "ept:logout";
    private const string EndSessionPath = "connect/end-session";

    /// <summary>Changes disposable native metadata under profile ownership; this is not the audited B2-to-B3 upgrade.</summary>
    internal static Task<bool> ConfigureAsync(AdministratorProtocolScenario scenario, string? permission) =>
        RunAsync(scenario, async (scope, token) =>
        {
            await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
            await AdministratorProtocolLocks.AcquireProfileAsync(scope.Owner, token);
            var manager = scope.Services.GetRequiredService<IOpenIddictApplicationManager>();
            var application = await manager.FindByClientIdAsync(AdministratorProtocolIdentity.ClientId, token);
            Assert.NotNull(application);
            var descriptor = new OpenIddictApplicationDescriptor();
            await manager.PopulateAsync(descriptor, application, token);
            descriptor.ApplicationType = OpenIddictConstants.ApplicationTypes.Web;
            descriptor.PostLogoutRedirectUris.Clear();
            descriptor.PostLogoutRedirectUris.Add(new Uri(ReturnUri));
            descriptor.Permissions.Remove(OpenIddictConstants.Permissions.Endpoints.EndSession);
            descriptor.Permissions.Remove(LegacyPermission);
            if (permission is not null)
            {
                descriptor.Permissions.Add(permission);
            }
            await manager.UpdateAsync(application, descriptor, token);
            await transaction.CommitAsync(token);
            return true;
        });

    /// <summary>Runs every installed native validation handler in a fresh owned scope; no synthetic authentication result is supplied.</summary>
    internal static Task<OpenIddictServerEvents.ValidateEndSessionRequestContext> ValidateAsync(
        AdministratorProtocolScenario scenario, OpenIddictRequest request) => RunAsync(scenario, async (scope, token) =>
        {
            var transaction = await scope.Services.GetRequiredService<IOpenIddictServerFactory>().CreateTransactionAsync();
            transaction.BaseUri = scenario.Issuer;
            transaction.RequestUri = new Uri(scenario.Issuer, EndSessionPath);
            transaction.EndpointType = OpenIddictServerEndpointType.EndSession;
            transaction.Request = request;
            var context = new OpenIddictServerEvents.ValidateEndSessionRequestContext(transaction) { CancellationToken = token };
            await scope.Services.GetRequiredService<IOpenIddictServerDispatcher>().DispatchAsync(context);
            Assert.False(context.IsRequestHandled);
            Assert.False(context.IsRequestSkipped);
            return context;
        });

    /// <summary>Requires successful native validation and nested authentication, without any identity-token hint or principal.</summary>
    internal static void RequireAccepted(OpenIddictServerEvents.ValidateEndSessionRequestContext context)
    {
        Assert.False(context.IsRejected);
        Assert.Equal(ReturnUri, context.PostLogoutRedirectUri);
        Assert.Null(context.Request.IdTokenHint);
        Assert.Null(context.IdentityTokenHintPrincipal);
        var authentication = context.Transaction.GetProperty<OpenIddictServerEvents.ProcessAuthenticationContext>(
            typeof(OpenIddictServerEvents.ProcessAuthenticationContext).FullName!);
        Assert.NotNull(authentication);
        Assert.False(authentication.IsRejected);
        Assert.False(authentication.IsRequestHandled);
        Assert.False(authentication.IsRequestSkipped);
        Assert.Null(authentication.IdentityToken);
        Assert.Null(authentication.IdentityTokenPrincipal);
    }

    /// <summary>Supplies only public fixed-client metadata; the absent token hint is intentional.</summary>
    internal static OpenIddictRequest Request() => new()
    {
        ClientId = AdministratorProtocolIdentity.ClientId,
        PostLogoutRedirectUri = ReturnUri
    };

    /// <summary>Retains the original bounded owner and native manager lifetimes for each independent operation.</summary>
    private static Task<T> RunAsync<T>(AdministratorProtocolScenario scenario, Func<ProtocolStorageScope, CancellationToken, ValueTask<T>> action) =>
        new ProtocolStorageRequest(scenario.Account.Journey.Clock, TestContext.Current.CancellationToken)
            .RunAsync(scenario.Services.GetRequiredService<IServiceScopeFactory>(), (scope, token) =>
            {
                scope.Services.GetRequiredService<ProtocolStorageSelection>().Select(ProtocolStorageMode.Owned);
                return action(scope, token);
            });
}
