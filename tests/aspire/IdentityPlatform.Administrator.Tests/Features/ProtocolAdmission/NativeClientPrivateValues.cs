using System.Net;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Observes actual synthetic native credentials in memory without substituting any generation, validation or transport handler.</summary>
internal sealed class NativeClientPrivateValues
{
    private readonly HashSet<string> values = new(StringComparer.Ordinal);
    private int states;
    private int protectedStates;
    private int assertions;
    private int authentications;
    private const int ExpectedAssertions = 2;
    private const int CookiePairParts = 2;

    /// <summary>Adds observation-only handlers after native generation and successful authentication completion.</summary>
    internal void Configure(IServiceCollection services)
    {
        var client = services.AddOpenIddict().AddClient();
        client.AddEventHandler<OpenIddictClientEvents.GenerateTokenContext>(builder => builder
            .SetOrder(OpenIddictClientHandlers.Protection.GenerateIdentityModelToken.Descriptor.Order + 1).UseInlineHandler(context =>
            {
                if (context.TokenType == OpenIddictConstants.TokenTypeIdentifiers.Private.StateToken)
                {
                    Add(context.Token);
                    protectedStates++;
                }
                return ValueTask.CompletedTask;
            }));
        client.AddEventHandler<OpenIddictClientEvents.GenerateTokenContext>(builder => builder
            .SetOrder(OpenIddictClientHandlers.Protection.AttachTokenPayload.Descriptor.Order + 1).UseInlineHandler(context =>
            {
                Add(context.Token);
                if (context.TokenType == OpenIddictConstants.TokenTypeIdentifiers.Private.StateToken)
                {
                    states++;
                    Add(context.Principal.GetClaim(OpenIddictConstants.Claims.Private.Nonce));
                    Add(context.Principal.GetClaim(OpenIddictConstants.Claims.Private.CodeVerifier));
                    Add(context.Principal.GetClaim(OpenIddictConstants.Claims.RequestForgeryProtection));
                }
                else if (context.TokenType == OpenIddictConstants.TokenTypeIdentifiers.Private.ClientAssertion)
                {
                    assertions++;
                }
                return ValueTask.CompletedTask;
            }));
        client.AddEventHandler<OpenIddictClientEvents.ProcessAuthenticationContext>(builder => builder
            .SetOrder(OpenIddictClientHandlers.PopulateMergedPrincipal.Descriptor.Order + 1).UseInlineHandler(context =>
            {
                Add(context.AuthorizationCode);
                Add(context.BackchannelAccessToken);
                Add(context.BackchannelIdentityToken);
                Add(context.StateToken);
                authentications++;
                return ValueTask.CompletedTask;
            }));
    }

    /// <summary>Includes the actual native correlation-cookie value and PAR handle without retaining full request URLs in evidence.</summary>
    internal void ObserveChallenge(NativeClientHttpResult challenge)
    {
        Assert.True(challenge.Cookies.Length == 1);
        var cookie = challenge.Cookies[0];
        Add(cookie.Split(';')[0].Split('=', CookiePairParts)[1]);
        var query = QueryHelpers.ParseQuery(new Uri(challenge.Location).Query);
        Add(query[OpenIddictConstants.Parameters.RequestUri].Single());
    }

    /// <summary>Requires that every intended native credential generation and successful authentication was actually reached before privacy checks.</summary>
    internal IEnumerable<string> RequireComplete()
    {
        Assert.Equal(1, states);
        Assert.Equal(1, protectedStates);
        Assert.Equal(ExpectedAssertions, assertions);
        Assert.Equal(1, authentications);
        return values;
    }

    /// <summary>Rejects absent observations and includes URI/form encodings so transport formatting does not conceal the exact synthetic value.</summary>
    private void Add(string? value)
    {
        Assert.False(string.IsNullOrEmpty(value));
        values.Add(value!);
        values.Add(Uri.EscapeDataString(value!));
        values.Add(WebUtility.UrlEncode(value!));
    }
}
