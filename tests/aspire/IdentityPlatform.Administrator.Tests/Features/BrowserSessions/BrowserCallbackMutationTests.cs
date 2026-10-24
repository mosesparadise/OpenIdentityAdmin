using System.Globalization;
using System.Net;
using IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;
using IdentityPlatform.Administrator.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Ensures late native identity mutation cannot replace the signed lifetime retained by the product callback.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserCallbackMutationTests(BootstrapFixture fixture)
{
    private const int AfterCapture = 1000;

    /// <summary>Shifting public issued/expiry dates together within native bounds after validated capture preserves other bindings but denies ticket establishment.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_ValidatedIdentity_When_LaterNativeHandlerExtendsPrincipalExpiry_Then_OriginalSignedIdentityCannotBeReplaced(string provider)
    {
        var changed = 0;
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider, services => services.AddOpenIddict().AddClient()
            .AddEventHandler<OpenIddictClientEvents.ProcessAuthenticationContext>(handler => handler
                .SetOrder(OpenIddictClientHandlers.PopulateMergedPrincipal.Descriptor.Order + AfterCapture).UseInlineHandler(context =>
                {
                    var identity = context.BackchannelIdentityTokenPrincipal!;
                    identity.SetClaim(OpenIddictConstants.Claims.IssuedAt, long.Parse(identity.GetClaim(OpenIddictConstants.Claims.IssuedAt)!, CultureInfo.InvariantCulture) + 1);
                    identity.SetClaim(OpenIddictConstants.Claims.ExpiresAt, long.Parse(identity.GetClaim(OpenIddictConstants.Claims.ExpiresAt)!, CultureInfo.InvariantCulture) + 1);
                    Assert.True(NativeClientTokenTime.Current(identity, context.Options.TimeProvider!));
                    Assert.True(NativeBrowserIdentity.Matches(context));
                    changed++;
                    return ValueTask.CompletedTask;
                })));
        var callback = await scenario.CallbackAsync();
        using var response = await scenario.Browser.GetAsync(callback, TestContext.Current.CancellationToken);
        BrowserLoginTests.RequireDenied(response, HttpStatusCode.ServiceUnavailable);
        Assert.Equal(1, changed);
        await BrowserCallbackTests.RequireNoTicketAsync(scenario, sends: 1);
    }
}
