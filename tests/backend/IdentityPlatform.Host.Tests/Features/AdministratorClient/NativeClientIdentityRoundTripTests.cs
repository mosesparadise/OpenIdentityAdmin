using System.Security.Claims;
using IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace IdentityPlatform.Host.Tests.Features.AdministratorClient;

/// <summary>Checks strict identity lifetime against actual native validation output rather than fabricated internal date claims.</summary>
public sealed class NativeClientIdentityRoundTripTests(NativeClientCredentialFixture fixture) : IClassFixture<NativeClientCredentialFixture>
{
    private const string Subject = "synthetic-identity-time-subject";
    private const int LifetimeSeconds = 60;

    /// <summary>Native ID validation retains public NumericDates; forged private dates cannot replace missing or expired public dates.</summary>
    [Fact]
    public async Task Given_ActualNativeIdentityValidation_When_StrictTimeGuardRuns_Then_PublicNumericDatesGovernAcceptance()
    {
        using var credentials = NativeClientProfileTests.Load(fixture);
        await using var services = NativeClientCompositionTests.Create(credentials);
        var options = services.GetRequiredService<IOptionsMonitor<OpenIddictClientOptions>>().CurrentValue;
        var context = await ValidateAsync(options, services.GetRequiredService<NativeClientProfile>());
        var principal = Assert.IsType<ClaimsPrincipal>(context.Principal);
        Assert.Null(principal.GetCreationDate());
        Assert.Null(principal.GetExpirationDate());
        Assert.NotNull(principal.GetClaim(OpenIddictConstants.Claims.IssuedAt));
        Assert.NotNull(principal.GetClaim(OpenIddictConstants.Claims.ExpiresAt));
        await new NativeClientTokenTime().HandleAsync(context);
        Assert.False(context.IsRejected);
        RequirePublicDates(principal, options.TimeProvider!);
    }

    /// <summary>Signs a real identity token and passes it through the installed native cryptographic and internal-mapping handlers.</summary>
    private static async Task<OpenIddictClientEvents.ValidateTokenContext> ValidateAsync(OpenIddictClientOptions options, NativeClientProfile profile)
    {
        var registration = Assert.Single(options.Registrations);
        var signing = Assert.Single(registration.SigningCredentials);
        var now = DateTimeOffset.UtcNow;
        var token = options.JsonWebTokenHandler.CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new(OpenIddictConstants.Claims.Subject, Subject)]),
            Issuer = NativeClientProfileTests.Issuer,
            Audience = registration.ClientId,
            IssuedAt = now.UtcDateTime,
            Expires = now.AddSeconds(LifetimeSeconds).UtcDateTime,
            SigningCredentials = signing,
            TokenType = OpenIddictConstants.JsonWebTokenTypes.GenericJsonWebToken
        });
        var configuration = new OpenIddictConfiguration { Issuer = registration.Issuer };
        configuration.SigningKeys.Add(signing.Key);
        var context = new OpenIddictClientEvents.ValidateTokenContext(new OpenIddictClientTransaction
        {
            Options = options,
            Registration = registration,
            Configuration = configuration,
            Logger = NullLogger.Instance
        })
        {
            Token = token
        };
        context.ValidTokenTypes.Add(OpenIddictConstants.TokenTypeIdentifiers.IdentityToken);
        await new OpenIddictClientHandlers.Protection.ResolveTokenValidationParameters().HandleAsync(context);
        await new NativeClientValidationParameters(profile).HandleAsync(context);
        await new OpenIddictClientHandlers.Protection.ValidateIdentityModelToken().HandleAsync(context);
        Assert.False(context.IsRejected);
        await new OpenIddictClientHandlers.Protection.MapInternalClaims().HandleAsync(context);
        return context;
    }

    /// <summary>Private date injection cannot rescue a missing public lifetime or expiration at equality.</summary>
    private static void RequirePublicDates(ClaimsPrincipal principal, TimeProvider clock)
    {
        foreach (var claim in new[] { OpenIddictConstants.Claims.IssuedAt, OpenIddictConstants.Claims.ExpiresAt })
        {
            var changed = principal.Clone().SetClaim(claim, (string?)null)
                .SetCreationDate(clock.GetUtcNow()).SetExpirationDate(clock.GetUtcNow().AddSeconds(LifetimeSeconds));
            Assert.False(NativeClientTokenTime.Current(changed, clock));
        }
        var expired = principal.Clone().SetClaim(OpenIddictConstants.Claims.ExpiresAt, clock.GetUtcNow().ToUnixTimeSeconds())
            .SetExpirationDate(clock.GetUtcNow().AddSeconds(LifetimeSeconds));
        Assert.False(NativeClientTokenTime.Current(expired, clock));
    }
}
