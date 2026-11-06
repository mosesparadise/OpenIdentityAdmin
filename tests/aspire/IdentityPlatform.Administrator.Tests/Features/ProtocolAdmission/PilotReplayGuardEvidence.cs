using System.Security.Claims;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Features.ManagedApplications;
using IdentityPlatform.AuthServer.Features.Protocol.PilotReplay;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Tests cleanup binding guards at an explicitly isolated component seam using a genuine previously validated code principal; this does not claim cryptographic acceptance of altered principals.</summary>
internal static class PilotReplayGuardEvidence
{
    private const string Pilot = "identityplatform-pilot";
    private const string Other = "unrelated-replay-binding";
    private static readonly Action<OpenIddictServerEvents.ValidateTokenContext>[] Invalid =
    [
        context => context.Transaction.Request!.ClientId = Other,
        context => context.Transaction.EndpointType = OpenIddictServerEndpointType.Authorization,
        context => context.Transaction.Request!.GrantType = OpenIddictConstants.GrantTypes.RefreshToken,
        context => context.Principal!.SetTokenType(OpenIddictConstants.TokenTypeIdentifiers.AccessToken),
        context => context.Principal!.SetPresenters(Other),
        context => context.TokenId = Other,
        context => context.AuthorizationId = Other,
        context => context.Principal!.SetClaim(OpenIddictConstants.Claims.Subject, (string?)null),
        context => context.Principal!.SetClaim(OpenIddictConstants.Claims.Subject, Other),
        context => context.Principal = null,
        context =>
        {
            context.TokenId = Other;
            context.Principal!.SetTokenId(Other);
        },
        context =>
        {
            context.AuthorizationId = Other;
            context.Principal!.SetAuthorizationId(Other);
        }
    ];

    /// <summary>Each independent invalid binding leaves every native scalar/association unchanged and cannot alter the native response decision.</summary>
    internal static async Task RequireAsync(BrowserHttpsFixture environment, ClaimsPrincipal principal)
    {
        var before = await SnapshotAsync(environment);
        foreach (var change in Invalid)
        {
            await using var scope = environment.IssuerServices.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<ProtocolStorageSelection>().Select(ProtocolStorageMode.LegacyPilot);
            var context = new OpenIddictServerEvents.ValidateTokenContext(new OpenIddictServerTransaction
            {
                EndpointType = OpenIddictServerEndpointType.Token,
                Request = new OpenIddictRequest { ClientId = Pilot, GrantType = OpenIddictConstants.GrantTypes.AuthorizationCode },
                Logger = NullLogger.Instance,
                Options = new OpenIddictServerOptions()
            })
            {
                Principal = principal.Clone(),
                TokenId = principal.GetTokenId(),
                AuthorizationId = principal.GetAuthorizationId(),
                CancellationToken = TestContext.Current.CancellationToken
            };
            change(context);
            await scope.ServiceProvider.GetRequiredService<PilotReplayCleanup>().HandleAsync(context);
            Assert.False(context.IsRejected);
            Assert.Equal(before, await SnapshotAsync(environment));
        }
    }

    /// <summary>Uses a fresh native observer for complete token and authorization digests without exposing any identifiers or payloads.</summary>
    private static async Task<string> SnapshotAsync(BrowserHttpsFixture environment)
    {
        await using var scope = environment.IssuerServices.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ProtocolStorageSelection>().Select(ProtocolStorageMode.LegacyPilot);
        return await ManagedJourneyEvidence.NativeAsync(scope.ServiceProvider.GetRequiredService<PlatformDbContext>());
    }
}
