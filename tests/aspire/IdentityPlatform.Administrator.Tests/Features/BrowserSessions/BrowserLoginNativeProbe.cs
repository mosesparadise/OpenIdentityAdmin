using System.Security.Claims;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Observes native output without replacing preparation, signing or persistence; state is never printed.</summary>
internal sealed class BrowserLoginNativeProbe
{
    private const int AfterPar = 1000;
    private string? state;

    /// <summary>Captures only the actual native reference after the complete PAR boundary has passed.</summary>
    internal void Configure(IServiceCollection services) => services.AddOpenIddict().AddClient()
        .AddEventHandler<OpenIddictClientEvents.ProcessChallengeContext>(handler => handler
            .SetOrder(OpenIddictClientHandlers.SendPushedAuthorizationRequest.Descriptor.Order + AfterPar).UseInlineHandler(context =>
            {
                state = context.StateToken;
                return ValueTask.CompletedTask;
            }));

    /// <summary>Independently authenticates/decrypts the reference through native validation and compares the entire reserved durable tuple.</summary>
    internal Task<bool> RequireAsync(BrowserLoginFixture fixture, BrowserLoginHost host, BrowserLoginIntent intent, string rawIntent, string completion) =>
        new ProtocolStorageRequest(fixture.Issuer.Account.Journey.Clock, TestContext.Current.CancellationToken)
            .RunAsync(host.Services.GetRequiredService<IServiceScopeFactory>(), async (scope, _) =>
            {
                Assert.NotNull(state);
                var transaction = await scope.Services.GetRequiredService<IOpenIddictClientFactory>().CreateTransactionAsync();
                transaction.BaseUri = fixture.Profile.ClientUri;
                var context = new OpenIddictClientEvents.ValidateTokenContext(transaction) { Token = state };
                context.ValidTokenTypes.Add(OpenIddictConstants.TokenTypeIdentifiers.Private.StateToken);
                await scope.Services.GetRequiredService<IOpenIddictClientDispatcher>().DispatchAsync(context);
                Assert.False(context.IsRejected);
                Assert.True(context.TokenValidationResult?.IsValid == true && context.IsReferenceToken);
                var principal = Assert.IsType<ClaimsPrincipal>(context.Principal);
                Assert.True(principal.GetClaim(BrowserSessionIntent.ClaimType) == intent.Digest);
                Assert.True(principal.GetClaim(BrowserLoginReservation.BrowserClaim) == intent.BrowserDigest);
                Assert.True(principal.GetClaim(BrowserLoginReservation.ReservationClaim) == intent.NativeStateDigest);
                Assert.True(principal.GetClaim(BrowserLoginReservation.GenerationClaim) == intent.Generation.ToString(System.Globalization.CultureInfo.InvariantCulture));
                Assert.True(principal.GetExpirationDate()!.Value.UtcTicks <= intent.ExpiresAt);
                Assert.Null(principal.GetClaim(OpenIddictConstants.Claims.TargetLinkUri));
                Assert.DoesNotContain(principal.Claims, claim => claim.Value.Contains(rawIntent, StringComparison.Ordinal) || claim.Value.Contains(completion, StringComparison.Ordinal));
                return true;
            });
}
