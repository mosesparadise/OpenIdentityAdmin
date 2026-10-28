using IdentityPlatform.Administrator.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Administrator.Tests.Features.SessionRefresh;

/// <summary>Characterizes native issuance preparation and expiry independently of required stricter product guards.</summary>
[Collection("administrator-bootstrap")]
public sealed class NativeRefreshLifetimeTests(BootstrapFixture fixture)
{
    private const string AdministratorScope = "identity.admin";
    private const int ExpectedSlidingHours = 8;
    private const int ExpectedShortMinutes = 20;
    private const int ElapsedMinutes = 10;
    private const int ConfiguredLifetimeHours = 8;
    private const int ShortLifetimeMinutes = 20;
    private static readonly bool[] OfflineChoices = [false, true];
    private static readonly bool[] SlidingChoices = [true, false];
    /// <summary>Native offline-scope selection changes generated-token flags but supplies no product permission or current-authority decision.</summary>
    [Theory, MemberData(nameof(Providers))]
    public async Task Given_ValidatedPrincipal_When_OfflineScopeChanges_Then_NativeRefreshSelectionFollowsThatScope(string provider)
    {
        await using var scenario = await NativeRefreshFixture.CreateAsync(fixture, provider);
        var family = await NativeRefreshRows.CreateAsync(scenario);
        await scenario.RunAsync(async (scope, _) =>
        {
            foreach (var offline in OfflineChoices)
            {
                var principal = family.Predecessor.Clone(claim => true).SetScopes(offline
                    ? [OpenIddictConstants.Scopes.OpenId, AdministratorScope, OpenIddictConstants.Scopes.OfflineAccess]
                    : [OpenIddictConstants.Scopes.OpenId, AdministratorScope]);
                var context = await NativeRefreshStages.SignInAsync(scope, principal);
                context.Request.GrantType = OpenIddictConstants.GrantTypes.AuthorizationCode;
                await new OpenIddictServerHandlers.EvaluateGeneratedTokens().HandleAsync(context);
                Assert.Equal(offline, context.GenerateRefreshToken);
                Assert.Equal(offline, context.IncludeRefreshToken);
                Assert.True(context.GenerateAccessToken);
            }
            return true;
        });
    }

    /// <summary>Disabling native sliding expiry preserves the predecessor cap; native default sliding would extend it.</summary>
    [Theory, MemberData(nameof(Providers))]
    public async Task Given_OriginalRefreshDeadline_When_SlidingPolicyChanges_Then_OnlyFixedExpiryPreservesTheOriginalCap(string provider)
    {
        await using var scenario = await NativeRefreshFixture.CreateAsync(fixture, provider);
        var family = await NativeRefreshRows.CreateAsync(scenario);
        var original = family.Predecessor.GetExpirationDate();
        scenario.Clock.Now = scenario.Clock.Now.AddMinutes(ElapsedMinutes);
        await scenario.RunAsync(async (scope, _) =>
        {
            foreach (var fixedExpiry in SlidingChoices)
            {
                var context = await NativeRefreshStages.SignInAsync(scope, family.Predecessor);
                context.Options.DisableSlidingRefreshTokenExpiration = fixedExpiry;
                context.Options.RefreshTokenLifetime = TimeSpan.FromHours(ConfiguredLifetimeHours);
                await new OpenIddictServerHandlers.PrepareRefreshTokenPrincipal(scope.Services.GetRequiredService<IOpenIddictApplicationManager>()).HandleAsync(context);
                Assert.Equal(fixedExpiry ? original : scenario.Clock.Now.AddHours(ExpectedSlidingHours), context.RefreshTokenPrincipal!.GetExpirationDate());
                Assert.Equal(family.Authorization, context.RefreshTokenPrincipal!.GetAuthorizationId());
                Assert.Null(context.RefreshTokenPrincipal!.GetTokenId());
                Assert.Equal(family.Predecessor.GetClaim(OpenIddictConstants.Claims.Subject), context.RefreshTokenPrincipal!.GetClaim(OpenIddictConstants.Claims.Subject));
            }
            return true;
        });
    }

    /// <summary>A deliberately shorter principal lifetime overrides native defaults for initial refresh preparation.</summary>
    [Theory, MemberData(nameof(Providers))]
    public async Task Given_InitialRefreshPrincipal_When_ShorterLifetimeIsAttached_Then_NativePreparationUsesTheShorterBound(string provider)
    {
        await using var scenario = await NativeRefreshFixture.CreateAsync(fixture, provider);
        var family = await NativeRefreshRows.CreateAsync(scenario);
        await scenario.RunAsync(async (scope, _) =>
        {
            var context = await NativeRefreshStages.SignInAsync(scope, family.Predecessor.Clone(claim => true).SetRefreshTokenLifetime(TimeSpan.FromMinutes(ShortLifetimeMinutes)));
            context.Request.GrantType = OpenIddictConstants.GrantTypes.AuthorizationCode;
            await new OpenIddictServerHandlers.PrepareRefreshTokenPrincipal(scope.Services.GetRequiredService<IOpenIddictApplicationManager>()).HandleAsync(context);
            Assert.Equal(scenario.Clock.Now.AddMinutes(ExpectedShortMinutes), context.RefreshTokenPrincipal!.GetExpirationDate());
            Assert.Equal(family.Authorization, context.RefreshTokenPrincipal!.GetAuthorizationId());
            return true;
        });
    }

    /// <summary>Native expiry allows equality with zero skew, proving why product expiry-at-equality needs its independent strict guard.</summary>
    [Theory, MemberData(nameof(Providers))]
    public async Task Given_RefreshExpiry_When_BeforeAtAndAfterAreValidated_Then_NativeEqualityIsNotProductStrictExpiry(string provider)
    {
        await using var scenario = await NativeRefreshFixture.CreateAsync(fixture, provider);
        var family = await NativeRefreshRows.CreateAsync(scenario);
        var expiry = family.Predecessor.GetExpirationDate()!.Value;
        foreach (var offset in new[] { -1, 0, 1 })
        {
            scenario.Clock.Now = expiry.AddSeconds(offset);
            await scenario.RunAsync(async (scope, _) =>
            {
                var context = new OpenIddictServerEvents.ValidateTokenContext(await NativeRefreshStages.TransactionAsync(scope))
                {
                    Principal = family.Predecessor
                };
                context.TokenValidationParameters.ClockSkew = TimeSpan.Zero;
                await new OpenIddictServerHandlers.Protection.ValidateExpirationDate().HandleAsync(context);
                Assert.Equal(offset > 0, context.IsRejected);
                Assert.Equal(offset > 0 ? OpenIddictConstants.Errors.InvalidToken : null, context.Error);
                return true;
            });
        }
    }

    /// <summary>Retains exact per-provider discovery for every native lifetime boundary.</summary>
    public static IEnumerable<TheoryDataRow<string>> Providers => NativeRefreshProviders.Rows;
}
