using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.Governance.Policy;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.AuthServer.Features.Protocol.Administrator;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Runs real enrollment, named-cookie authentication, native PAR, authorize and code exchange on each relational provider.</summary>
[Collection("administrator-bootstrap")]
public sealed class NativeAccountIssuanceTests(BootstrapFixture fixture)
{
    private const int CodeSeconds = 60;
    private const int ProofSeconds = 300;
    private const int TokenSeconds = 300;
    private static readonly string[] AuthenticationMethods = ["pwd", "otp"];
    private static readonly string[] ForbiddenClaims = ["role", "roles", "permission", "permissions", "email", "name",
        "oi_admin_factor_time", "oi_admin_source_expiry", "oi_admin_profile_revision", "oi_admin_maximum_age"];
    /// <summary>Each approved current bundle obtains only fixed credentials retaining the original full source proof and native bindings.</summary>
    [Theory, MemberData(nameof(AdmissionCases.Bundles), MemberType = typeof(AdmissionCases))]
    public async Task Given_RealAdministratorCookie_When_NativeCodeFlowCompletes_Then_OriginalProofAndFixedAudienceSurvive(string provider, string bundle)
    {
        await using var scenario = await AdministratorProtocolScenario.CreateAsync(fixture, provider);
        await GrantState.SetMembershipsAsync(scenario.Account, bundle);
        var session = await scenario.Account.Journey.Database.Set<AdministratorSession>().AsNoTracking()
            .SingleAsync(row => row.PublicId == scenario.Account.Session.SessionId, TestContext.Current.CancellationToken);
        var reference = await NativeAccountFlow.PushAsync(scenario);
        var authorizedAt = scenario.Account.Journey.Clock.Now.ToUnixTimeSeconds();
        var code = NativeAccountFlow.ReadCode(scenario, await NativeAccountFlow.AuthorizeAsync(scenario, reference));
        var protectedCode = await NativeAccountFlow.ValidateAsync(scenario, code, OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode);
        Assert.True(AdministratorDecisionPolicy.IsCode(protectedCode));
        Assert.True(protectedCode.GetExpirationDate()?.ToUnixTimeSeconds() <= Math.Min(Math.Min(authorizedAt + CodeSeconds, session.ExpiresAt),
            Math.Min(session.PasswordVerifiedAt + ProofSeconds, session.FactorVerifiedAt + ProofSeconds)));
        Assert.True(protectedCode.GetClaim(AdministratorPrincipal.FactorTime) == session.FactorVerifiedAt.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var exchangedAt = scenario.Account.Journey.Clock.Now.ToUnixTimeSeconds();
        var response = await NativeAccountFlow.ExchangeAsync(scenario, code);
        NativeAccountFlow.RequireSuccess(response, StatusCodes.Status200OK);
        Assert.Equal(2, scenario.Services.GetRequiredService<NativeAccountClockAdvance>().Observed);
        Assert.Equal(scenario.Account.Journey.Clock.Now.ToUnixTimeSeconds(), await NativeAccountPersistence.ObservedAtAsync(scenario));
        using var json = JsonDocument.Parse(response.Body);
        Assert.False(json.RootElement.TryGetProperty(OpenIddictConstants.Parameters.RefreshToken, out _));
        var access = await NativeAccountFlow.ValidateAsync(scenario, json.RootElement.GetProperty(OpenIddictConstants.Parameters.AccessToken).GetString()!,
            OpenIddictConstants.TokenTypeIdentifiers.AccessToken);
        var identity = await NativeIdentityTokenOracle.ValidateAsync(scenario, json.RootElement.GetProperty(OpenIddictConstants.Parameters.IdToken).GetString()!,
            json.RootElement.GetProperty(OpenIddictConstants.Parameters.AccessToken).GetString()!);
        Assert.True(access.GetAudiences().SequenceEqual([AdministratorProtocolIdentity.Resource]));
        Assert.True(identity.GetClaims(OpenIddictConstants.Claims.Audience).SequenceEqual([AdministratorProtocolIdentity.ClientId]));
        Assert.True(identity.GetClaim(OpenIddictConstants.Claims.Nonce) == NativeAccountFlow.Nonce);
        Assert.True(identity.GetClaim(OpenIddictConstants.Claims.AuthenticationTime) == session.PasswordVerifiedAt.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.True(identity.GetClaims(OpenIddictConstants.Claims.AuthenticationMethodReference).ToHashSet(StringComparer.Ordinal).SetEquals(AuthenticationMethods));
        foreach (var principal in new[] { access, identity })
        {
            Assert.True(principal.GetClaim(OpenIddictConstants.Claims.Subject) == session.UserId);
            Assert.True(principal.GetClaim(AdministratorPrincipal.SessionId) == session.PublicId);
            Assert.True(principal.GetAuthorizationId() == protectedCode.GetAuthorizationId());
            Assert.DoesNotContain(principal.Claims.Select(claim => claim.Type), type => ForbiddenClaims.Contains(type, StringComparer.Ordinal));
        }
        Assert.InRange(access.GetExpirationDate()!.Value.ToUnixTimeSeconds(), exchangedAt + 1, Math.Min(exchangedAt + TokenSeconds, session.ExpiresAt));
        Assert.True(long.TryParse(identity.GetClaim(OpenIddictConstants.Claims.ExpiresAt), System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture, out var identityExpiry));
        Assert.InRange(identityExpiry, exchangedAt + 1, Math.Min(exchangedAt + TokenSeconds, session.ExpiresAt));
    }
}
