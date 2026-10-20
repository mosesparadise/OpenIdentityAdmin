using System.Globalization;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.AuthServer.Features.Protocol.Administrator;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.ProtocolAdmission;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Exercises three precise B2 security boundaries with valid controls and behavioral mutation markers.</summary>
[Collection("administrator-bootstrap")]
public sealed class NativeProtocolMutationTests(BootstrapFixture fixture)
{
    private const string AuthorityBypass = "protocol-current-authority-bypass";
    private const string ReplayBypass = "protocol-assertion-replay-bypass";
    private const string ProfileBypass = "protocol-profile-binding-bypass";

    /// <summary>Final proof and claim projection must deny persisted permission loss inside the original owned epoch, then recover after rollback.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    [Trait("CriticalDecision", "protocol-current-authority")]
    public async Task Given_CurrentProof_When_PersistedPermissionIsRemovedBeforeFinalRead_Then_ProofAndClaimsDeny(string provider)
    {
        await using var scenario = await AdmissionScenario.CreateAsync(fixture, provider);
        var bypass = await scenario.RunAsync(async (scope, result, token) =>
        {
            Assert.Equal(ProtocolAdmissionOutcome.Allowed, result.Outcome);
            Assert.NotNull(result.Proof);
            var clock = scope.Services.GetRequiredService<CeremonyClock>();
            Assert.True(await result.Proof.IsCurrentAsync(clock, token));
            Assert.NotNull(await result.Proof.DescribeAsync(clock, token));
            await scope.Owner.RunAsync(async cancellation =>
            {
                var database = (PlatformDbContext)scope.Owner.Database;
                Assert.True(await database.UserRoles.Where(row => row.UserId == scenario.Account.ActorId).ExecuteDeleteAsync(cancellation) > 0);
                Assert.False(await database.UserRoles.AsNoTracking().AnyAsync(row => row.UserId == scenario.Account.ActorId, cancellation));
            }, token, write: true);
            var current = await result.Proof.IsCurrentAsync(clock, token);
            var claims = await result.Proof.DescribeAsync(clock, token);
            Assert.Equal(current, claims is not null);
            return current && claims is not null;
        });
        Assert.True(await scenario.RunAsync(async (scope, result, token) => result.Outcome == ProtocolAdmissionOutcome.Allowed &&
            result.Proof is not null && await result.Proof.IsCurrentAsync(scope.Services.GetRequiredService<CeremonyClock>(), token)));
        Assert.False(bypass, AuthorityBypass);
    }

    /// <summary>An otherwise valid exchange cannot reuse the exact signed assertion already consumed by PAR on another issuer root.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    [Trait("CriticalDecision", "protocol-assertion-replay")]
    public async Task Given_ConsumedParAssertion_When_RealCodeExchangeReusesIt_Then_NoCredentialIsIssued(string provider)
    {
        await using var scenario = await AdministratorProtocolScenario.CreateAsync(fixture, provider);
        await using var peer = scenario.CreatePeer(provider);
        var fields = NativeAccountFlow.PushFields(scenario);
        var assertion = fields[OpenIddictConstants.Parameters.ClientAssertion];
        var reference = NativeAccountFlow.ReadReference(await NativeAccountHttp.InvokeAsync(scenario, AdministratorProtocolScenario.ParPath, fields));
        var digests = await NativeMutationState.AssertionDigestsAsync(scenario);
        Assert.True(digests.Length == 1);
        var code = NativeAccountFlow.ReadCode(scenario, await NativeAccountFlow.AuthorizeAsync(scenario, reference, peer.Services));
        var principal = await NativeAccountFlow.ValidateAsync(scenario, code, OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode);
        var before = await NativeAccountPersistence.CountAsync(scenario);
        var response = await NativeAccountHttp.InvokeAsync(scenario, AdministratorProtocolScenario.TokenPath,
            NativeAccountFlow.ExchangeFields(scenario, code, assertion), services: peer.Services);
        var retained = await NativeMutationState.AssertionDigestsAsync(scenario);
        Assert.True(digests.SequenceEqual(retained, StringComparer.Ordinal));
        var bypass = response.Status == StatusCodes.Status200OK;
        if (bypass)
        {
            await NativeMutationGrant.RequireAsync(scenario, response, principal, before);
        }
        else
        {
            NativeAccountDenialTests.RequireJson(response, OpenIddictConstants.Errors.InvalidClient);
            Assert.Equal(before, await NativeAccountPersistence.CountAsync(scenario));
            Assert.Equal(OpenIddictConstants.Statuses.Valid, await NativeAccountPersistence.StatusAsync(scenario, principal.GetTokenId()!));
            await NativeMutationGrant.RequireAsync(scenario, await NativeAccountFlow.ExchangeAsync(scenario, code, peer.Services), principal, before);
        }
        Assert.False(bypass, ReplayBypass);
    }

    /// <summary>A genuine code cannot issue tokens after its original profile revision changes, even when the current registration otherwise matches.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    [Trait("CriticalDecision", "protocol-profile-binding")]
    public async Task Given_CodeBoundToOriginalProfile_When_RevisionChangesBeforeExchange_Then_NoCredentialIsIssued(string provider)
    {
        await using var scenario = await AdministratorProtocolScenario.CreateAsync(fixture, provider);
        await using var peer = scenario.CreatePeer(provider);
        var reference = await NativeAccountFlow.PushAsync(scenario);
        var code = NativeAccountFlow.ReadCode(scenario, await NativeAccountFlow.AuthorizeAsync(scenario, reference, peer.Services));
        var principal = await NativeAccountFlow.ValidateAsync(scenario, code, OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode);
        var revisions = await NativeMutationState.AdvanceRevisionAsync(scenario);
        Assert.True(principal.GetClaim(AdministratorPrincipal.ProfileRevision) == revisions.Before.ToString(CultureInfo.InvariantCulture));
        var before = await NativeAccountPersistence.CountAsync(scenario);
        var response = await NativeAccountFlow.ExchangeAsync(scenario, code, peer.Services);
        var bypass = response.Status == StatusCodes.Status200OK;
        if (bypass)
        {
            await NativeMutationGrant.RequireAsync(scenario, response, principal, before);
        }
        else
        {
            NativeAccountDenialTests.RequireJson(response, OpenIddictConstants.Errors.InvalidGrant);
            Assert.Equal(before, await NativeAccountPersistence.CountAsync(scenario));
            Assert.True(await NativeAccountPersistence.IsRedeemedAsync(scenario, principal.GetTokenId()!));
        }
        var freshReference = await NativeAccountFlow.PushAsync(scenario, peer.Services);
        var freshCode = NativeAccountFlow.ReadCode(scenario, await NativeAccountFlow.AuthorizeAsync(scenario, freshReference));
        var freshPrincipal = await NativeAccountFlow.ValidateAsync(scenario, freshCode, OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode);
        Assert.True(freshPrincipal.GetClaim(AdministratorPrincipal.ProfileRevision) == revisions.After.ToString(CultureInfo.InvariantCulture));
        var freshBefore = await NativeAccountPersistence.CountAsync(scenario);
        await NativeMutationGrant.RequireAsync(scenario, await NativeAccountFlow.ExchangeAsync(scenario, freshCode), freshPrincipal, freshBefore);
        Assert.False(bypass, ProfileBypass);
    }
}
